using System;
using UnityEngine;

namespace Easy.DI
{
    [DefaultExecutionOrder(-1001)]
    public class CustomContext : BaseContext
    {
        public String contextName;

        private void Awake()
        {
            container = DIContext.CreateContainer(contextName, MainContext.NAME);
            CallBinders(GetComponents<ContextBinder>());
        }
    }
}