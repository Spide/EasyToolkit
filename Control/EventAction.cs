using System;

namespace Easy.Control
{
    public class EventAction
    {
        private string eventName;
        private Action<object[]> action;

        public EventAction(string eventName, Action<object[]> action)
        {
            this.EventName = eventName;
            this.Action = action;
        }

        public string EventName { get => eventName; set => eventName = value; }
        public Action<object[]> Action { get => action; set => action = value; }
    }
}