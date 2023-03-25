using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.DI
{
    [DefaultExecutionOrder(-1001)]
    public class MainContext : BaseContext
    {
        public const string NAME = "GLOBAL";
        public List<ContextBinder> binders = new List<ContextBinder>();

        private void Awake()
        {
            container = DIContext.CreateContainer(NAME);
            container.Bind(this);

            CallBinders(binders);
            DontDestroyOnLoad(gameObject);
        }
    }
}
