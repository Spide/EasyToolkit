using Easy.DI;
using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Default Easy DI container names used by scene flow. Persistent role
    /// scenes may use any name configured in <see cref="SceneFlowProfile"/>;
    /// these constants only document the conventional split.
    /// </summary>
    public static class SceneFlowContextNames
    {
        public const string Global = MainContext.NAME;
        public const string Systems = "SYSTEMS";
        public const string UI = "UI";
        public const string Debug = "DEBUG";
        public const string Level = "LEVEL";
    }

    public static class SceneFlowContexts
    {
        /// <summary>
        /// Creates the persistent GLOBAL context when no initializer or scene
        /// has created it yet. Safe to call repeatedly.
        /// </summary>
        public static void EnsureGlobalContext()
        {
            if (DIContext.TryResolve(SceneFlowContextNames.Global, out MainContext _))
                return;

            if (!Application.isPlaying)
            {
                // EditMode tests share the static container registry between runs.
                if (!DIContext.TryResolve(SceneFlowContextNames.Global, out SceneFlowGlobalMarker _))
                {
                    DIContainer container = DIContext.CreateContainer(SceneFlowContextNames.Global);
                    container.Bind(new SceneFlowGlobalMarker());
                }

                return;
            }

            var contextObject = new GameObject("GLOBAL_CONTEXT");
            contextObject.AddComponent<MainContext>();
        }

        /// <summary>Binds an instance into GLOBAL unless its type is already bound.</summary>
        public static void BindGlobalIfMissing<T>(T instance) where T : class
        {
            if (instance == null || DIContext.TryResolve(SceneFlowContextNames.Global, out T _))
                return;

            DIContext.Bind(SceneFlowContextNames.Global, instance);
        }

        public static bool TryResolveGlobal<T>(out T service)
        {
            return DIContext.TryResolve(SceneFlowContextNames.Global, out service);
        }

        internal sealed class SceneFlowGlobalMarker
        {
        }
    }
}
