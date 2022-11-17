using System;
using System.Collections.Generic;
using System.Reflection;
using Easy.Logging;
using UnityEngine;

namespace Easy.Control
{
    public abstract class ControlContext : IControlContext
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(ControlContext), Color.Lerp(Color.gray, Color.blue, 0.5f), "[ControlContext]");

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        protected int priority = 0;
        public int Priority { get { return priority; } }

        protected bool isActive = false;
        public bool IsActive { get => isActive; }

        protected Dictionary<string,MethodInfo> events = new Dictionary<string, MethodInfo>();

        public virtual void Activate()
        {
            isActive = true;
            OnActiveStatusChanged?.Invoke(this, isActive);
            LOGGER.Log("Activated {0} {1} ", ToString(), isActive);
        }

        public virtual void Deactivate()
        {
            isActive = false;
            OnActiveStatusChanged?.Invoke(this, isActive);

            LOGGER.Log("Activated {0} {1} ", ToString(), isActive);
        }

        protected ControlContext()
        {
            Autobind();
        }

        protected void SafeMethodInvoke(MethodInfo method, object[] parameters)
        {
            int paramLength = method.GetParameters().Length;
            if (parameters.Length > paramLength)
            {
                var realParams = new object[paramLength];
                Array.Copy(parameters, realParams, paramLength);
                method.Invoke(this, realParams);
            }
            else
            {
                method.Invoke(this, parameters);
            }
        }

        protected void Autobind()
        {
            LOGGER.Log("AutoDetection events for [{0}] ", this.ToString());
            foreach (var method in this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = method.GetCustomAttributes(typeof(ControlEvent), true);
                if (attributes.Length > 0)
                {
                    LOGGER.Log("Method check {0} attributes length {1} ", method.Name, attributes.Length);

                    foreach (ControlEvent attribute in attributes)
                    {
                        LOGGER.Log("Added {0} -> {1}", attribute.Name, method.Name);
                        SetEvent(attribute.Name, method);
                    }
                }
            }
        }

        protected void SetEvent(string eventName, Action<object[]> action)
        {
            events[eventName] = action.Method;
        }

        protected void SetEvent(string eventName, MethodInfo action)
        {
            events[eventName] = action;
        }

        public bool On(string eventName, params object[] parameters)
        {
            LOGGER.Log("Control event \"{0}\" triggered", eventName);

            if (events.TryGetValue(eventName, out MethodInfo thisEvent))
            {
                SafeMethodInvoke(thisEvent, parameters);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}