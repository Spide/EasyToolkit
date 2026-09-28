using System;
using System.Collections.Generic;
using System.Reflection;

namespace Easy.Control
{
    public abstract class ControlContext : IControlContext
    {
        private readonly Dictionary<string, MethodInfo> events = new();
        private bool isActive;

        protected ControlContext(int priority = 0)
        {
            Priority = priority;
            Autobind();
        }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        public int Priority { get; }
        public bool IsActive => isActive;

        public virtual void Activate()
        {
            if (isActive)
                return;

            isActive = true;
            OnActiveStatusChanged?.Invoke(this, true);
        }

        public virtual void Deactivate()
        {
            if (!isActive)
                return;

            isActive = false;
            OnActiveStatusChanged?.Invoke(this, false);
        }

        protected void SetEvent(string eventName, MethodInfo action)
        {
            events[eventName] = action;
        }

        public bool On(string eventName, params object[] parameters)
        {
            if (!events.TryGetValue(eventName, out MethodInfo method))
                return false;

            int parameterCount = method.GetParameters().Length;
            if (parameters.Length > parameterCount)
            {
                var realParameters = new object[parameterCount];
                Array.Copy(parameters, realParameters, parameterCount);
                method.Invoke(this, realParameters);
            }
            else
            {
                method.Invoke(this, parameters);
            }

            return true;
        }

        private void Autobind()
        {
            MethodInfo[] methods = GetType().GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                object[] attributes = method.GetCustomAttributes(typeof(ControlEvent), true);
                for (int attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
                {
                    var attribute = (ControlEvent)attributes[attributeIndex];
                    SetEvent(attribute.Name, method);
                }
            }
        }
    }
}
