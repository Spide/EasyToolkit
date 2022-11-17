using System;
using System.Collections.Generic;
using Easy.Logging;
using UnityEngine;

namespace Easy.Control
{
    public class ControlContextEventHandler
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(ControlContextEventHandler), Color.white, "[ControlContextEventHandler]");

        private readonly List<IControlContext> contexts = new List<IControlContext>();
        private IControlContext[] activeContexts = null;

        Predicate<IControlContext> filterActive = c => c.IsActive;

        private IControlContext[] GetActiveContexts()
        {
            if (activeContexts == null) 
                activeContexts = contexts.FindAll(filterActive).ToArray();

            return activeContexts;
        }

        public void AddContext(params IControlContext[] context)
        {
            activeContexts = null;
            contexts.AddRange(context);
            Array.ForEach(context, c => c.OnActiveStatusChanged += OnActivatedOrDeactivated);
            contexts.Sort((c1, c2) => c2.Priority - c1.Priority);
        }

        public void RemoveContext(params IControlContext[] context)
        {
            activeContexts = null;
            Array.ForEach(context, c => { contexts.Remove(c); c.OnActiveStatusChanged -= OnActivatedOrDeactivated; });
            contexts.Sort((c1, c2) => c2.Priority - c1.Priority);
        }

        public void OnActivatedOrDeactivated(IControlContext context, bool status)
        {
            activeContexts = null;
        }

        public bool  TriggerEvent(string eventName, params object[] parameters)
        {
            LOGGER.Log("Control event \"{0}\" triggered", eventName);

            foreach (var context in GetActiveContexts())
            {
                var stopPropagation = context.On(eventName, parameters);

                if (stopPropagation)
                    return true;
            }

            return false;
        }
    }
}