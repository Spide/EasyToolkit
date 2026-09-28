using System;
using System.Threading;
using System.Threading.Tasks;

namespace Easy.SceneFlow
{
    public readonly struct LevelLoadRequest
    {
        public const string DefaultEntryPointId = "default";

        public LevelLoadRequest(string levelKey, string entryPointId = null, bool forceReload = false)
        {
            LevelKey = levelKey;
            EntryPointId = string.IsNullOrWhiteSpace(entryPointId) ? DefaultEntryPointId : entryPointId;
            ForceReload = forceReload;
        }

        public string LevelKey { get; }
        public string EntryPointId { get; }

        /// <summary>Reload even when the level is already active (loads a second scene instance first).</summary>
        public bool ForceReload { get; }
    }

    public enum LevelLoadStatus
    {
        Succeeded = 0,
        AlreadyActive = 1,
        Busy = 2,
        NotInitialized = 3,
        UnknownLevel = 4,
        SceneUnavailable = 5,
        ContextNotReady = 6,
        PreparationFailed = 7,
        EntryPointMissing = 8,
        ActivationFailed = 9,
    }

    public readonly struct LevelLoadResult
    {
        public LevelLoadResult(LevelLoadStatus status, string levelKey, string message = null)
        {
            Status = status;
            LevelKey = levelKey;
            Message = message ?? string.Empty;
        }

        public LevelLoadStatus Status { get; }
        public string LevelKey { get; }
        public string Message { get; }

        public bool Succeeded =>
            Status == LevelLoadStatus.Succeeded || Status == LevelLoadStatus.AlreadyActive;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Message)
                ? $"{Status} '{LevelKey}'"
                : $"{Status} '{LevelKey}': {Message}";
        }
    }

    /// <summary>Published once a level is promoted and the loading scene is gone.</summary>
    public readonly struct LevelActivation
    {
        public LevelActivation(
            LevelDefinition definition,
            LevelContext context,
            LevelEntryPoint entryPoint,
            bool forcedReload)
        {
            Definition = definition;
            Context = context;
            EntryPoint = entryPoint;
            ForcedReload = forcedReload;
        }

        public LevelDefinition Definition { get; }
        public LevelContext Context { get; }
        public LevelEntryPoint EntryPoint { get; }
        public bool ForcedReload { get; }
        public string LevelKey => Definition != null ? Definition.LevelKey : string.Empty;
    }

    /// <summary>
    /// Persistent systems that must follow level changes (player/party placement,
    /// input leases, audio snapshots, save boundaries). Register through
    /// <see cref="ILevelLoader.RegisterParticipant"/>. Calls happen on the main
    /// thread in this order: Started, then either Aborted or Entering and
    /// Committed. A failed Entering rolls the transition back and calls Aborted.
    /// </summary>
    public interface ILevelTransitionParticipant
    {
        void OnTransitionStarted(LevelLoadRequest request);

        /// <summary>Called after the candidate level context is active but before it is committed.</summary>
        bool TryEnterLevel(LevelActivation activation, out string error);

        void OnTransitionCommitted(LevelActivation activation);

        /// <summary>The previous level (if any) was resumed; restore the pre-transition state.</summary>
        void OnTransitionAborted(LevelLoadResult result);
    }

    public interface ILevelLoader
    {
        /// <summary>Raised after a level was promoted and the loading scene was unloaded.</summary>
        event Action<LevelActivation> LevelActivated;

        /// <summary>Raised for every rejected or failed request, including Busy.</summary>
        event Action<LevelLoadResult> LevelLoadFailed;

        bool IsInitialized { get; }
        bool IsTransitioning { get; }
        string ActiveLevelKey { get; }
        LevelContext ActiveLevel { get; }
        LevelCatalog Catalog { get; }
        LoadingProgress Progress { get; }

        Task<LevelLoadResult> LoadLevelAsync(LevelLoadRequest request);

        void RegisterParticipant(ILevelTransitionParticipant participant);
        void UnregisterParticipant(ILevelTransitionParticipant participant);
    }

    public readonly struct LoadingProgressState
    {
        public LoadingProgressState(string currentStep, int completedSteps, int totalSteps, bool isComplete)
        {
            CurrentStep = currentStep ?? string.Empty;
            CompletedSteps = completedSteps;
            TotalSteps = totalSteps;
            IsComplete = isComplete;
        }

        public string CurrentStep { get; }
        public int CompletedSteps { get; }
        public int TotalSteps { get; }
        public bool IsComplete { get; }

        public float Normalized =>
            TotalSteps <= 0 ? (IsComplete ? 1f : 0f) : Math.Clamp((float)CompletedSteps / TotalSteps, 0f, 1f);
    }

    /// <summary>Presentation-neutral progress published in GLOBAL for loading views.</summary>
    public sealed class LoadingProgress
    {
        public event Action<LoadingProgressState> Changed;

        public LoadingProgressState Current { get; private set; }

        public void Report(string step, int completed, int total)
        {
            Current = new LoadingProgressState(step, completed, total, false);
            Changed?.Invoke(Current);
        }

        public void Complete(string step = "Ready")
        {
            int total = Current.TotalSteps;
            Current = new LoadingProgressState(step, total, total, true);
            Changed?.Invoke(Current);
        }
    }

    /// <summary>Single-entry gate that rejects overlapping transitions.</summary>
    public sealed class TransitionGate
    {
        private int held;

        public bool IsHeld => Volatile.Read(ref held) != 0;

        public bool TryEnter()
        {
            return Interlocked.CompareExchange(ref held, 1, 0) == 0;
        }

        public void Exit()
        {
            Volatile.Write(ref held, 0);
        }
    }
}
