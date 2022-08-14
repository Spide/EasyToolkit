using System;
using UnityEngine.InputSystem;

namespace Easy.Control
{
    public class SingleEventControlContext : IControlContext
    {
        private readonly string eventName;
        private readonly Action<InputAction.CallbackContext> action;

        public int Priority { get; } = 0;
        public bool IsActive { get; protected set; }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        public SingleEventControlContext(int priority, string eventName, Action<InputAction.CallbackContext> action)
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
                action.Invoke((InputAction.CallbackContext)parameters[0]);
                return true;
            }

            return false;
        }
    }
}