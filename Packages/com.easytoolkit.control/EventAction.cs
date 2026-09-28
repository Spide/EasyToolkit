using System;

namespace Easy.Control
{
    public sealed class EventAction
    {
        public EventAction(string eventName, Action<object[]> action)
        {
            EventName = eventName;
            Action = action;
        }

        public string EventName { get; set; }
        public Action<object[]> Action { get; set; }
    }
}
