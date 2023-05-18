using System;
using Easy.Logging;
using UnityEngine;

namespace Easy.Control
{
    public class ControlContextGroup : IControlContext
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(ControlContextGroup), Color.Lerp(Color.white, Color.blue, 0.5f), "[ControlContextGroup]");

        private int priority = 0;
        public int Priority { get { return priority; } }

        protected bool isActive = false;
        public bool IsActive { get => isActive; }

        public event Action<IControlContext, bool> OnActiveStatusChanged;

        private readonly ControlContextEventHandler controlContextHandler = new ControlContextEventHandler();

        public ControlContextGroup(int priority, params IControlContext[] contexts)
        {
            this.priority = priority;
            controlContextHandler.AddContext(contexts);
        }

        public void AddContext(params IControlContext[] contexts)
        {
            controlContextHandler.AddContext(contexts);
        }
        public void RemoveContext(params IControlContext[] contexts)
        {
            controlContextHandler.RemoveContext(contexts);
        }

        public void Activate()
        {
            isActive = true;
            OnActiveStatusChanged?.Invoke(this, IsActive);
        }

        public void Deactivate()
        {
            isActive = false;
            OnActiveStatusChanged?.Invoke(this, IsActive);
        }

        public bool On(string eventName, params object[] parameters)
        {
            return controlContextHandler.TriggerEvent(eventName, parameters);
        }
    }
}