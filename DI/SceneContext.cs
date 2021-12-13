using System;
using UnityEngine;

namespace Easy.DI
{
    [DefaultExecutionOrder(-1000)]
    public class SceneContext : MonoBehaviour
    {
        public string contextName;

        private DIContainer container;

        private void Awake()
        {
            contextName = string.IsNullOrEmpty(contextName) ? gameObject.scene.name : contextName;
            container = DIContext.CreateContainer(contextName,MainContext.CONTEXT_NAME);
        }

        private void Start()
        {
            Array.ForEach(GetComponents<ContextBinder>(), binder => binder.Bind(container));
        }

        private void OnDestroy()
        {
            DIContext.Clear(contextName);
        }
    }
}