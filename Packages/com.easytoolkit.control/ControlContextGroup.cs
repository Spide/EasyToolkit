using System;

namespace Easy.Control
{
    public sealed class ControlContextGroup : IControlContext
    {
        private readonly ControlContextEventHandler eventHandler = new();

        public ControlContextGroup(int priority, params IControlContext[] contexts)
        {
            Priority = priority;
            eventHandler.AddContext(contexts);
        }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        public int Priority { get; }
        public bool IsActive { get; private set; }

        public void AddContext(params IControlContext[] contexts)
        {
            eventHandler.AddContext(contexts);
        }

        public void RemoveContext(params IControlContext[] contexts)
        {
            eventHandler.RemoveContext(contexts);
        }

        public void Activate()
        {
            if (IsActive)
                return;

            IsActive = true;
            OnActiveStatusChanged?.Invoke(this, true);
        }

        public void Deactivate()
        {
            if (!IsActive)
                return;

            IsActive = false;
            OnActiveStatusChanged?.Invoke(this, false);
        }

        public bool On(string eventName, params object[] parameters)
        {
            return eventHandler.TriggerEvent(eventName, parameters);
        }
    }
}
