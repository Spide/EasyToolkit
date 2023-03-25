using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.DI
{
    public class BaseContext : MonoBehaviour
    {
        protected DIContainer container;

        protected void CallBinders(params ContextBinder[] binders)
        {
            foreach (var binder in binders)
                binder.Bind(container);
            DIContext.ContextBinded(container.Name);
        }

        protected void CallBinders(ICollection<ContextBinder> binders)
        {
            foreach (var binder in binders)
                binder.Bind(container);
            DIContext.ContextBinded(container.Name);
        }

        private void OnDestroy()
        {
            DIContext.Clear(container.Name);
        }
    }
}