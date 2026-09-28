using System.Collections.Generic;
using Easy.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Publishes one named Easy DI container (child of GLOBAL) for the lifetime
    /// of a persistent role scene such as SYSTEMS, UI or DEBUG. Add one per role
    /// scene; bind scene services through <see cref="ContextBinder"/> components
    /// on the same GameObject or by overriding <see cref="BindSceneServices"/>.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    [DisallowMultipleComponent]
    public class SceneRoleContext : BaseContext
    {
        private static readonly Dictionary<Scene, SceneRoleContext> ContextsByScene = new();

        [SerializeField, Tooltip("Must match the context name configured in the SceneFlowProfile.")]
        private string contextName = SceneFlowContextNames.Systems;

        public string ContextName => contextName;
        public bool IsReady { get; private set; }
        protected DIContainer Container => container;

        public static bool TryGetForScene(Scene scene, out SceneRoleContext context)
        {
            context = null;
            return scene.IsValid() && ContextsByScene.TryGetValue(scene, out context) && context != null;
        }

        protected virtual void Awake()
        {
            ContextsByScene[gameObject.scene] = this;
            if (string.IsNullOrWhiteSpace(contextName))
            {
                SceneFlowLog.Logger.LogError("Scene '{0}' has a SceneRoleContext without a context name.", gameObject.scene.name);
                return;
            }

            SceneFlowContexts.EnsureGlobalContext();
            try
            {
                container = DIContext.CreateContainer(contextName, SceneFlowContextNames.Global);
            }
            catch (System.Exception exception)
            {
                SceneFlowLog.Logger.LogException(exception, "Could not create context '{0}' for scene '{1}'.", contextName, gameObject.scene.name);
                container = null;
                return;
            }

            container.Bind(this);
            BindSceneServices();
            CallBinders(GetComponents<ContextBinder>());
            IsReady = true;
        }

        /// <summary>Bind scene-owned services into <see cref="Container"/>.</summary>
        protected virtual void BindSceneServices()
        {
        }

        protected virtual void OnDestroy()
        {
            IsReady = false;
            if (ContextsByScene.TryGetValue(gameObject.scene, out SceneRoleContext registered) && registered == this)
                ContextsByScene.Remove(gameObject.scene);

            if (container == null)
                return;

            if (DIContext.TryResolve(contextName, out SceneRoleContext owner) && owner == this)
                DIContext.Clear(contextName);
            container = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ContextsByScene.Clear();
        }
    }
}
