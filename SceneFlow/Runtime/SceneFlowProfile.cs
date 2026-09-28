using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>One persistent additive scene and the role context it must publish.</summary>
    [Serializable]
    public sealed class PersistentSceneEntry
    {
        [SerializeField, Tooltip("Scene name as registered in Build Settings.")]
        private string sceneName;

        [SerializeField, Tooltip("Easy DI container name published by the scene's SceneRoleContext.")]
        private string contextName;

        [SerializeField, Tooltip("Load only in the Editor and development builds.")]
        private bool developmentOnly;

        [SerializeField, Tooltip("A missing or unready required scene aborts startup.")]
        private bool required = true;

        public PersistentSceneEntry()
        {
        }

        public PersistentSceneEntry(
            string sceneName,
            string contextName,
            bool developmentOnly = false,
            bool required = true)
        {
            this.sceneName = sceneName;
            this.contextName = contextName;
            this.developmentOnly = developmentOnly;
            this.required = required;
        }

        public string SceneName => sceneName;
        public string ContextName => contextName;
        public bool DevelopmentOnly => developmentOnly;
        public bool Required => required;

        public bool ShouldLoad
        {
            get
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                    return false;
                if (!developmentOnly)
                    return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
    }

    /// <summary>
    /// Describes the application composition: which persistent scenes exist,
    /// in which order they load, which scene shows loading progress, and which
    /// catalog provides levels. Loaded from <see cref="ResourcePath"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Easy Toolkit/Scene Flow/Scene Flow Profile", fileName = "SceneFlowProfile")]
    public sealed class SceneFlowProfile : ScriptableObject
    {
        public const string ResourcePath = "SceneFlow/SceneFlowProfile";

        [Header("Bootstrap")]
        [SerializeField, Tooltip("Entry scene that owns the SceneFlowInitializer. It is unloaded after startup.")]
        private string initializationScene = "Initialization";

        [SerializeField, Tooltip("Temporary scene shown while startup or a level transition runs. Optional.")]
        private string loadingScene = "Loading";

        [Header("Persistent Composition (loaded in order)")]
        [SerializeField]
        private List<PersistentSceneEntry> persistentScenes = new();

        [Header("Levels")]
        [SerializeField]
        private LevelCatalog levelCatalog;

        [SerializeField]
        private string defaultLevelKey;

        [SerializeField, Min(1f)]
        private float contextReadyTimeoutSeconds = 30f;

        public string InitializationScene => initializationScene;
        public string LoadingScene => loadingScene;
        public IReadOnlyList<PersistentSceneEntry> PersistentScenes => persistentScenes;
        public LevelCatalog LevelCatalog => levelCatalog;
        public string DefaultLevelKey => defaultLevelKey;
        public float ContextReadyTimeoutSeconds => contextReadyTimeoutSeconds;

        public bool TryValidate(out string error)
        {
            if (levelCatalog == null)
            {
                error = $"Scene flow profile '{name}' has no level catalog.";
                return false;
            }

            if (!levelCatalog.TryValidate(out error))
                return false;

            if (!levelCatalog.TryGetByKey(defaultLevelKey, out _))
            {
                error = $"Scene flow profile '{name}' default level '{defaultLevelKey}' is not in the catalog.";
                return false;
            }

            var contextNames = new HashSet<string>(StringComparer.Ordinal)
            {
                SceneFlowContextNames.Global,
                SceneFlowContextNames.Level,
            };
            for (int i = 0; i < persistentScenes.Count; i++)
            {
                PersistentSceneEntry entry = persistentScenes[i];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.SceneName)
                    || string.IsNullOrWhiteSpace(entry.ContextName))
                {
                    error = $"Scene flow profile '{name}' persistent scene {i} needs a scene and context name.";
                    return false;
                }

                if (!contextNames.Add(entry.ContextName))
                {
                    error = $"Scene flow profile '{name}' reuses or reserves context name '{entry.ContextName}'.";
                    return false;
                }

                if (levelCatalog.TryGetBySceneName(entry.SceneName, out _))
                {
                    error = $"Scene '{entry.SceneName}' cannot be both persistent and a level.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(
            LevelCatalog catalog,
            string defaultKey,
            string initialization,
            string loading,
            params PersistentSceneEntry[] persistent)
        {
            levelCatalog = catalog;
            defaultLevelKey = defaultKey;
            initializationScene = initialization;
            loadingScene = loading;
            persistentScenes.Clear();
            if (persistent != null)
                persistentScenes.AddRange(persistent);
        }
    }
}
