using System;
using UnityEngine.InputSystem;

namespace Easy.Control
{
    public sealed class SingleEventControlContext : IControlContext
    {
        private readonly string eventName;
        private readonly Action<InputAction.CallbackContext> action;

        public SingleEventControlContext(
            int priority,
            string eventName,
            Action<InputAction.CallbackContext> action)
        {
            Priority = priority;
            this.eventName = eventName;
            this.action = action;
        }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        public int Priority { get; }
        public bool IsActive { get; private set; }

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

        public bool On(string triggeredEventName, params object[] parameters)
        {
            if (!eventName.Equals(triggeredEventName, StringComparison.Ordinal)
                || parameters.Length == 0
                || parameters[0] is not InputAction.CallbackContext callbackContext)
            {
                return false;
            }

            action.Invoke(callbackContext);
            return true;
        }
    }
}
