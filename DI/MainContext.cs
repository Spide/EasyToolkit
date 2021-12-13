using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.DI
{
    public class MainContext : MonoBehaviour
    {
        public const string CONTEXT_NAME = "GLOBAL";
        public List<ContextBinder> binders = new List<ContextBinder>();
        private DIContainer container;

        private void Awake()
        {
            container = DIContext.CreateContainer(CONTEXT_NAME);
            binders.ForEach(binder => binder.Bind(container));
            DontDestroyOnLoad(gameObject);
        }

    }
}