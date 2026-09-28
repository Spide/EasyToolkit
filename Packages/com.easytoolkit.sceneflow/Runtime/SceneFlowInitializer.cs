using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Single persistent orchestration authority for scene composition.
    ///
    /// Startup: GLOBAL context, loading scene, persistent role scenes in profile
    /// order (each must publish a ready <see cref="SceneRoleContext"/>), then the
    /// startup level; finally the loading and initialization scenes unload.
    ///
    /// Level switch: reject overlap, load the candidate privately, prepare and
    /// validate it, deactivate the old level, activate the candidate as LEVEL,
    /// let participants enter it, commit, and unload the old scene. Any failure
    /// before commit unloads the candidate and restores the previous level.
    ///
    /// Published in GLOBAL as <see cref="ILevelLoader"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class SceneFlowInitializer : MonoBehaviour, ILevelLoader
    {
        private static SceneFlowInitializer activeInstance;
        private static bool hasQueuedStartup;
        private static LevelLoadRequest queuedStartup;

        [SerializeField, Tooltip("Optional. Defaults to Resources/" + SceneFlowProfile.ResourcePath + ".")]
        private SceneFlowProfile profile;

        [SerializeField]
        private bool initializeOnStart = true;

        private readonly LoadingProgress progress = new();
        private readonly TransitionGate transitionGate = new();
        private readonly SceneLoadService sceneLoadService = new();
        private readonly List<ILevelTransitionParticipant> participants = new();
        private readonly List<ILevelTransitionParticipant> participantSnapshot = new();

        private Scene bootstrapScene;
        private Task initializationTask;
        private bool hasPendingActivation;
        private LevelActivation pendingActivation;

        public event Action<LevelActivation> LevelActivated;
        public event Action<LevelLoadResult> LevelLoadFailed;
        public event Action Initialized;

        public bool IsInitialized { get; private set; }
        public bool IsTransitioning => transitionGate.IsHeld;
        public string ActiveLevelKey { get; private set; }
        public LevelContext ActiveLevel { get; private set; }
        public LevelCatalog Catalog => profile != null ? profile.LevelCatalog : null;
        public LoadingProgress Progress => progress;
        public SceneFlowProfile Profile => profile;

        /// <summary>Queues the level the next initializer starts with (used by direct level Play Mode).</summary>
        public static void QueueStartup(LevelLoadRequest request)
        {
            queuedStartup = request;
            hasQueuedStartup = true;
        }

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                SceneFlowLog.Logger.LogWarning("Duplicate SceneFlowInitializer in '{0}' destroyed.", gameObject.scene.name);
                Destroy(gameObject);
                return;
            }

            activeInstance = this;
            bootstrapScene = gameObject.scene;
            DontDestroyOnLoad(gameObject);
            if (profile == null)
                profile = Resources.Load<SceneFlowProfile>(SceneFlowProfile.ResourcePath);
            PublishGlobalServices();
        }

        private async void Start()
        {
            if (!initializeOnStart)
                return;

            try
            {
                await InitializeAsync();
            }
            catch (Exception exception)
            {
                SceneFlowLog.Logger.LogException(exception, "Scene flow startup failed.");
            }
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
                activeInstance = null;
        }

        public Task InitializeAsync()
        {
            initializationTask ??= InitializeInternalAsync();
            return initializationTask;
        }

        public async Task<LevelLoadResult> LoadLevelAsync(LevelLoadRequest request)
        {
            if (!IsInitialized)
                return Fail(new LevelLoadResult(LevelLoadStatus.NotInitialized, request.LevelKey, "Scene flow has not finished startup."));

            if (IsTransitioning)
                return Fail(new LevelLoadResult(LevelLoadStatus.Busy, request.LevelKey, "Another transition is running."));

            if (!request.ForceReload && string.Equals(ActiveLevelKey, request.LevelKey, StringComparison.Ordinal))
                return new LevelLoadResult(LevelLoadStatus.AlreadyActive, request.LevelKey);

            LevelLoadResult result = await LoadLevelInternalAsync(request, 0, 1, manageLoadingScene: true);
            return result.Succeeded ? result : Fail(result);
        }

        public void RegisterParticipant(ILevelTransitionParticipant participant)
        {
            if (participant != null && !participants.Contains(participant))
                participants.Add(participant);
        }

        public void UnregisterParticipant(ILevelTransitionParticipant participant)
        {
            participants.Remove(participant);
        }

        private async Task InitializeInternalAsync()
        {
            PublishGlobalServices();
            if (profile == null)
            {
                SceneFlowLog.Logger.LogError("Missing scene flow profile at Resources/{0}.", SceneFlowProfile.ResourcePath);
                return;
            }

            if (!profile.TryValidate(out string profileError))
            {
                SceneFlowLog.Logger.LogError(profileError);
                return;
            }

            int total = 1;
            IReadOnlyList<PersistentSceneEntry> persistent = profile.PersistentScenes;
            for (int i = 0; i < persistent.Count; i++)
            {
                if (persistent[i].ShouldLoad)
                    total++;
            }

            if (!transitionGate.TryEnter())
                return;

            int completed = 0;
            try
            {
                progress.Report("Loading", completed, total);
                await sceneLoadService.LoadAdditiveIfMissingAsync(profile.LoadingScene);

                for (int i = 0; i < persistent.Count; i++)
                {
                    PersistentSceneEntry entry = persistent[i];
                    if (!entry.ShouldLoad)
                        continue;

                    progress.Report($"Loading {entry.SceneName}", completed, total);
                    bool ready = await LoadRoleSceneAsync(entry);
                    if (!ready && entry.Required)
                    {
                        SceneFlowLog.Logger.LogError(
                            "Required scene '{0}' did not publish a ready '{1}' SceneRoleContext.",
                            entry.SceneName,
                            entry.ContextName);
                        return;
                    }

                    completed++;
                }
            }
            finally
            {
                transitionGate.Exit();
            }

            LevelLoadRequest request = ResolveStartupRequest();
            LevelLoadResult result = await LoadLevelInternalAsync(request, completed, total, manageLoadingScene: false);
            if (!result.Succeeded && request.LevelKey != profile.DefaultLevelKey)
            {
                SceneFlowLog.Logger.LogWarning("Startup level failed ({0}); falling back to '{1}'.", result, profile.DefaultLevelKey);
                result = await LoadLevelInternalAsync(new LevelLoadRequest(profile.DefaultLevelKey), completed, total, manageLoadingScene: false);
            }

            if (!result.Succeeded)
            {
                SceneFlowLog.Logger.LogError("Startup level failed: {0}", result);
                LevelLoadFailed?.Invoke(result);
                return;
            }

            progress.Complete();
            await sceneLoadService.UnloadIfLoadedAsync(profile.LoadingScene);
            if (bootstrapScene.IsValid()
                && bootstrapScene.isLoaded
                && bootstrapScene.name == profile.InitializationScene)
            {
                await sceneLoadService.UnloadAsync(bootstrapScene);
            }

            // Initialized only once the temporary scenes are gone, so callers
            // never observe a half-finished composition.
            IsInitialized = true;
            Initialized?.Invoke();
            PublishPendingActivation();
        }

        private async Task<LevelLoadResult> LoadLevelInternalAsync(
            LevelLoadRequest request,
            int completed,
            int total,
            bool manageLoadingScene)
        {
            LevelCatalog catalog = Catalog;
            if (catalog == null || !catalog.TryGetByKey(request.LevelKey, out LevelDefinition level))
                return new LevelLoadResult(LevelLoadStatus.UnknownLevel, request.LevelKey, $"Unknown level key '{request.LevelKey}'.");

            if (!sceneLoadService.CanLoadScene(level.SceneName))
                return new LevelLoadResult(LevelLoadStatus.SceneUnavailable, request.LevelKey, $"Scene '{level.SceneName}' is not in Build Settings.");

            if (!transitionGate.TryEnter())
                return new LevelLoadResult(LevelLoadStatus.Busy, request.LevelKey, "Another transition is running.");

            LevelContext previous = ActiveLevel;
            bool loadingWasLoaded = sceneLoadService.IsSceneLoaded(profile.LoadingScene);
            bool forceAdditionalInstance = request.ForceReload
                && previous != null
                && previous.gameObject.scene.name == level.SceneName;
            bool ownsCandidateScene = forceAdditionalInstance || !sceneLoadService.IsSceneLoaded(level.SceneName);
            Scene candidateScene = default;
            LevelContext candidate = null;
            bool committed = false;
            LevelActivation activation = default;
            SnapshotParticipants();

            try
            {
                if (manageLoadingScene)
                {
                    progress.Report("Loading", completed, total);
                    await sceneLoadService.LoadAdditiveIfMissingAsync(profile.LoadingScene);
                }

                previous?.Suspend();
                for (int i = 0; i < participantSnapshot.Count; i++)
                    participantSnapshot[i].OnTransitionStarted(request);

                progress.Report($"Loading {level.DisplayName}", completed, total);
                if (forceAdditionalInstance)
                {
                    candidateScene = await sceneLoadService.LoadAdditionalInstanceAsync(level.SceneName);
                }
                else if (await sceneLoadService.LoadAdditiveIfMissingAsync(level.SceneName))
                {
                    candidateScene = SceneManager.GetSceneByName(level.SceneName);
                }

                if (!candidateScene.IsValid() || !candidateScene.isLoaded)
                    return await AbortAsync(LevelLoadStatus.SceneUnavailable, $"Could not load '{level.SceneName}'.");

                candidate = await WaitForLevelContextAsync(candidateScene);
                if (candidate == null)
                    return await AbortAsync(LevelLoadStatus.ContextNotReady, $"Scene '{level.SceneName}' has no LevelContext.");

                LevelPreparationResult preparation = await candidate.PrepareAsync(level);
                if (!preparation.Succeeded)
                    return await AbortAsync(LevelLoadStatus.PreparationFailed, preparation.Error);

                if (!candidate.TryGetEntryPoint(request.EntryPointId, out LevelEntryPoint entryPoint))
                    return await AbortAsync(LevelLoadStatus.EntryPointMissing, $"Level '{level.LevelKey}' has no entry point '{request.EntryPointId}'.");

                previous?.Deactivate();
                if (!candidate.Activate(out string activationError))
                    return await AbortAsync(LevelLoadStatus.ActivationFailed, activationError);

                activation = new LevelActivation(level, candidate, entryPoint, request.ForceReload);
                for (int i = 0; i < participantSnapshot.Count; i++)
                {
                    if (!participantSnapshot[i].TryEnterLevel(activation, out string participantError))
                    {
                        candidate.Deactivate();
                        return await AbortAsync(LevelLoadStatus.ActivationFailed, participantError);
                    }
                }

                ActiveLevel = candidate;
                ActiveLevelKey = level.LevelKey;
                SceneManager.SetActiveScene(candidateScene);
                committed = true;
                for (int i = 0; i < participantSnapshot.Count; i++)
                    participantSnapshot[i].OnTransitionCommitted(activation);

                if (previous != null && previous.gameObject.scene != candidateScene)
                {
                    if (!await sceneLoadService.UnloadAsync(previous.gameObject.scene))
                        SceneFlowLog.Logger.LogWarning("Level '{0}' is active but the previous scene did not unload.", level.LevelKey);
                }

                progress.Report($"Loaded {level.DisplayName}", total, total);
                return new LevelLoadResult(LevelLoadStatus.Succeeded, request.LevelKey);
            }
            finally
            {
                transitionGate.Exit();
                if (manageLoadingScene && !loadingWasLoaded)
                    await sceneLoadService.UnloadIfLoadedAsync(profile.LoadingScene);

                if (committed)
                {
                    pendingActivation = activation;
                    hasPendingActivation = true;
                    if (IsInitialized)
                        PublishPendingActivation();
                }
            }

            async Task<LevelLoadResult> AbortAsync(LevelLoadStatus status, string message)
            {
                var failure = new LevelLoadResult(status, request.LevelKey, message);
                if (ownsCandidateScene && candidateScene.IsValid() && candidateScene.isLoaded)
                    await sceneLoadService.UnloadAsync(candidateScene);

                if (previous != null)
                {
                    if (!previous.IsActiveLevel && !previous.Activate(out string restoreError))
                        SceneFlowLog.Logger.LogError("Could not restore previous level: {0}", restoreError);
                    previous.Resume();
                }

                for (int i = 0; i < participantSnapshot.Count; i++)
                    participantSnapshot[i].OnTransitionAborted(failure);
                return failure;
            }
        }

        private async Task<bool> LoadRoleSceneAsync(PersistentSceneEntry entry)
        {
            if (!await sceneLoadService.LoadAdditiveIfMissingAsync(entry.SceneName))
                return false;

            Scene scene = SceneManager.GetSceneByName(entry.SceneName);
            float timeoutAt = Time.realtimeSinceStartup + profile.ContextReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                if (SceneRoleContext.TryGetForScene(scene, out SceneRoleContext context))
                {
                    if (!string.Equals(context.ContextName, entry.ContextName, StringComparison.Ordinal))
                    {
                        SceneFlowLog.Logger.LogError(
                            "Scene '{0}' publishes context '{1}' but the profile expects '{2}'.",
                            entry.SceneName,
                            context.ContextName,
                            entry.ContextName);
                        return false;
                    }

                    if (context.IsReady)
                        return true;
                }

                await Task.Yield();
            }

            return false;
        }

        private async Task<LevelContext> WaitForLevelContextAsync(Scene scene)
        {
            float timeoutAt = Time.realtimeSinceStartup + profile.ContextReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                if (LevelContext.TryGetForScene(scene, out LevelContext context))
                    return context;
                await Task.Yield();
            }

            return null;
        }

        private LevelLoadRequest ResolveStartupRequest()
        {
            if (hasQueuedStartup)
            {
                hasQueuedStartup = false;
                return queuedStartup;
            }

            if (SceneFlowEditorOverrides.TryGetStartupRequest(out LevelLoadRequest editorRequest))
                return editorRequest;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && Catalog.TryGetBySceneName(scene.name, out LevelDefinition loaded))
                    return new LevelLoadRequest(loaded.LevelKey);
            }

            return new LevelLoadRequest(profile.DefaultLevelKey);
        }

        private void SnapshotParticipants()
        {
            participantSnapshot.Clear();
            participantSnapshot.AddRange(participants);
        }

        private void PublishPendingActivation()
        {
            if (!hasPendingActivation)
                return;

            LevelActivation activation = pendingActivation;
            hasPendingActivation = false;
            pendingActivation = default;
            LevelActivated?.Invoke(activation);
        }

        private LevelLoadResult Fail(LevelLoadResult result)
        {
            LevelLoadFailed?.Invoke(result);
            return result;
        }

        private void PublishGlobalServices()
        {
            SceneFlowContexts.EnsureGlobalContext();
            SceneFlowContexts.BindGlobalIfMissing<ILevelLoader>(this);
            SceneFlowContexts.BindGlobalIfMissing(progress);
            SceneFlowContexts.BindGlobalIfMissing(sceneLoadService);
            SceneFlowContexts.BindGlobalIfMissing(profile);
            SceneFlowContexts.BindGlobalIfMissing(Catalog);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeInstance = null;
            hasQueuedStartup = false;
            queuedStartup = default;
        }
    }
}
