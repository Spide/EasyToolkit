using System;
using System.Collections.Generic;

namespace Easy.Control
{
    public sealed class ControlContextEventHandler
    {
        private readonly List<IControlContext> contexts = new();
        private readonly Predicate<IControlContext> filterActive = context => context.IsActive;
        private IControlContext[] activeContexts;

        public void AddContext(params IControlContext[] addedContexts)
        {
            activeContexts = null;
            contexts.AddRange(addedContexts);
            for (int i = 0; i < addedContexts.Length; i++)
                addedContexts[i].OnActiveStatusChanged += OnActiveStatusChanged;
            contexts.Sort(ComparePriority);
        }

        public void RemoveContext(params IControlContext[] removedContexts)
        {
            activeContexts = null;
            for (int i = 0; i < removedContexts.Length; i++)
            {
                IControlContext context = removedContexts[i];
                contexts.Remove(context);
                context.OnActiveStatusChanged -= OnActiveStatusChanged;
            }

            contexts.Sort(ComparePriority);
        }

        public bool TriggerEvent(string eventName, params object[] parameters)
        {
            IControlContext[] active = GetActiveContexts();
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i].On(eventName, parameters))
                    return true;
            }

            return false;
        }

        private IControlContext[] GetActiveContexts()
        {
            activeContexts ??= contexts.FindAll(filterActive).ToArray();
            return activeContexts;
        }

        private void OnActiveStatusChanged(IControlContext context, bool isActive)
        {
            activeContexts = null;
        }

        private static int ComparePriority(IControlContext left, IControlContext right)
        {
            return right.Priority.CompareTo(left.Priority);
        }
    }
}
