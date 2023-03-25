using UnityEngine;

namespace Easy.DI
{
    [DefaultExecutionOrder(-1000)]
    public class SceneContext : BaseContext
    {
        private void Awake()
        {
            container = DIContext.CreateContainer(gameObject.scene.name, MainContext.NAME);
            CallBinders(GetComponents<ContextBinder>());
        }
    }
}