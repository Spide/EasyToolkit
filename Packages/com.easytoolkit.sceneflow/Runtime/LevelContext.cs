using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Easy.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow
{
    public readonly struct LevelPreparationResult
    {
        private LevelPreparationResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }

        public static LevelPreparationResult Success => new(true, null);

        public static LevelPreparationResult Failed(string error)
        {
            return new LevelPreparationResult(false, error);
        }
    }

    /// <summary>
    /// Root of one level scene. The level loads with its content inactive and
    /// private; only after preparation and validation does the loader promote it
    /// to the single LEVEL Easy DI context and activate its content.
    ///
    /// Content roots default to the direct children of the context GameObject,
    /// so the usual layout is one root "Level" object with this component and
    /// all level content beneath it. Objects outside that hierarchy are not
    /// managed. Subclass only for special preparation or level-scoped services.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public class LevelContext : BaseContext
    {
        private static readonly Dictionary<Scene, LevelContext> ContextsByScene = new();

        [SerializeField, Tooltip("Optional explicit content roots. Empty = direct children of this GameObject.")]
        private GameObject[] contentRoots = Array.Empty<GameObject>();

        [SerializeField, Tooltip("Optional explicit entry points. Empty = every LevelEntryPoint in the scene.")]
        private LevelEntryPoint[] entryPoints = Array.Empty<LevelEntryPoint>();

        private readonly List<GameObject> resolvedContentRoots = new();
        private readonly List<LevelEntryPoint> resolvedEntryPoints = new();

        public bool IsPrepared { get; private set; }
        public bool IsActiveLevel { get; private set; }
        public bool IsSuspended { get; private set; }
        public LevelDefinition Definition { get; private set; }
        public IReadOnlyList<LevelEntryPoint> EntryPoints => resolvedEntryPoints;
        protected DIContainer Container => container;

        public static bool TryGetForScene(Scene scene, out LevelContext context)
        {
            context = null;
            return scene.IsValid() && ContextsByScene.TryGetValue(scene, out context) && context != null;
        }

        protected virtual void Awake()
        {
            ContextsByScene[gameObject.scene] = this;
            ResolveAuthoring();
            SetContentActive(false);
        }

        public async Task<LevelPreparationResult> PrepareAsync(LevelDefinition definition)
        {
            if (IsPrepared)
                return LevelPreparationResult.Success;

            Definition = definition;
            ResolveAuthoring();
            if (!TryValidateAuthoring(out string authoringError))
                return LevelPreparationResult.Failed(authoringError);

            try
            {
                LevelPreparationResult result = await OnPrepareAsync();
                if (!result.Succeeded)
                    return result;
            }
            catch (Exception exception)
            {
                SceneFlowLog.Logger.LogException(exception, "Level '{0}' preparation threw.", gameObject.scene.name);
                return LevelPreparationResult.Failed(exception.Message);
            }

            IsPrepared = true;
            return LevelPreparationResult.Success;
        }

        public bool TryGetEntryPoint(string entryPointId, out LevelEntryPoint entryPoint)
        {
            string id = string.IsNullOrWhiteSpace(entryPointId) ? LevelLoadRequest.DefaultEntryPointId : entryPointId;
            for (int i = 0; i < resolvedEntryPoints.Count; i++)
            {
                LevelEntryPoint candidate = resolvedEntryPoints[i];
                if (candidate != null && string.Equals(candidate.EntryPointId, id, StringComparison.Ordinal))
                {
                    entryPoint = candidate;
                    return true;
                }
            }

            entryPoint = null;
            return false;
        }

        /// <summary>Publishes the LEVEL context and activates content.</summary>
        public bool Activate(out string error)
        {
            if (IsActiveLevel)
            {
                error = string.Empty;
                return true;
            }

            if (!IsPrepared)
            {
                error = $"Level '{gameObject.scene.name}' must be prepared before activation.";
                return false;
            }

            if (DIContext.TryResolve(SceneFlowContextNames.Level, out LevelContext other) && other != this)
            {
                error = $"Another level context '{other.gameObject.scene.name}' still owns {SceneFlowContextNames.Level}.";
                return false;
            }

            try
            {
                SceneFlowContexts.EnsureGlobalContext();
                container = DIContext.CreateContainer(SceneFlowContextNames.Level, SceneFlowContextNames.Global);
                container.Bind(this);
                BindLevelServices();
                CallBinders(GetComponents<ContextBinder>());
                SetContentActive(true);
                IsActiveLevel = true;
                IsSuspended = false;
                OnActivated();
            }
            catch (Exception exception)
            {
                SceneFlowLog.Logger.LogException(exception, "Level '{0}' activation threw.", gameObject.scene.name);
                Deactivate();
                error = exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>Hides content and withdraws the LEVEL context. The scene stays loaded.</summary>
        public void Deactivate()
        {
            if (IsActiveLevel)
                OnDeactivating();

            IsActiveLevel = false;
            SetContentActive(false);
            ClearOwnedContainer();
        }

        /// <summary>Pauses the active level while a transition prepares a candidate.</summary>
        public void Suspend()
        {
            if (IsSuspended)
                return;

            IsSuspended = true;
            OnSuspended();
        }

        public void Resume()
        {
            if (!IsSuspended)
                return;

            IsSuspended = false;
            OnResumed();
        }

        /// <summary>Async preparation hook: streaming, navigation checks, spawning. Content is still inactive.</summary>
        protected virtual Task<LevelPreparationResult> OnPrepareAsync()
        {
            return Task.FromResult(LevelPreparationResult.Success);
        }

        protected virtual bool TryValidateAuthoring(out string error)
        {
            if (!TryGetEntryPoint(LevelLoadRequest.DefaultEntryPointId, out _))
            {
                error = $"Level '{gameObject.scene.name}' has no '{LevelLoadRequest.DefaultEntryPointId}' LevelEntryPoint.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>Bind level-owned services into <see cref="Container"/> before content activates.</summary>
        protected virtual void BindLevelServices()
        {
        }

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivating()
        {
        }

        protected virtual void OnSuspended()
        {
        }

        protected virtual void OnResumed()
        {
        }

        protected virtual void OnDestroy()
        {
            if (ContextsByScene.TryGetValue(gameObject.scene, out LevelContext registered) && registered == this)
                ContextsByScene.Remove(gameObject.scene);
            ClearOwnedContainer();
        }

        private void ClearOwnedContainer()
        {
            if (container == null)
                return;

            if (DIContext.TryResolve(SceneFlowContextNames.Level, out LevelContext owner) && owner == this)
                DIContext.Clear(SceneFlowContextNames.Level);
            container = null;
        }

        private void ResolveAuthoring()
        {
            resolvedContentRoots.Clear();
            if (contentRoots != null && contentRoots.Length > 0)
            {
                for (int i = 0; i < contentRoots.Length; i++)
                {
                    if (contentRoots[i] != null && contentRoots[i] != gameObject)
                        resolvedContentRoots.Add(contentRoots[i]);
                }
            }
            else
            {
                Transform root = transform;
                for (int i = 0; i < root.childCount; i++)
                    resolvedContentRoots.Add(root.GetChild(i).gameObject);
            }

            resolvedEntryPoints.Clear();
            if (entryPoints != null && entryPoints.Length > 0)
            {
                for (int i = 0; i < entryPoints.Length; i++)
                {
                    if (entryPoints[i] != null)
                        resolvedEntryPoints.Add(entryPoints[i]);
                }

                return;
            }

            GetComponentsInChildren(true, resolvedEntryPoints);
        }

        private void SetContentActive(bool active)
        {
            for (int i = 0; i < resolvedContentRoots.Count; i++)
            {
                GameObject root = resolvedContentRoots[i];
                if (root != null && root.activeSelf != active)
                    root.SetActive(active);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ContextsByScene.Clear();
        }
    }
}
