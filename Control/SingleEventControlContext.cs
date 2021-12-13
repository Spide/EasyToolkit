using System;

namespace Easy.Control
{
    public class SingleEventControlContext : IControlContext
    {
        private readonly string eventName;
        private readonly Action<object[]> action;

        public int Priority { get; } = 0;

        public bool IsActive { get; protected set; }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        public SingleEventControlContext(int priority, string eventName, Action<object[]> action)
        {
            this.eventName = eventName;
            this.action = action;
            this.Priority = priority;
        }

        public void Activate()
        {
            IsActive = true;
            OnActiveStatusChanged?.Invoke(this, IsActive);
        }

        public void Deactivate()
        {
            IsActive = false;
            OnActiveStatusChanged?.Invoke(this, IsActive);
        }

        public bool On(string eventName, params object[] parameters)
        {
            if (this.eventName.Equals(eventName))
            {
                this.action.Invoke(parameters);
                return true;
            }

            return false;
        }
    }
}