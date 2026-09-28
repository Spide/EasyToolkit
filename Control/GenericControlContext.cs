using System;

namespace Easy.Control
{
    public sealed class GenericControlContext : ControlContext
    {
        public GenericControlContext(int priority, params EventAction[] eventActions)
            : base(priority)
        {
            for (int i = 0; i < eventActions.Length; i++)
            {
                EventAction eventAction = eventActions[i];
                SetEvent(eventAction.EventName, eventAction.Action.Method);
            }
        }
    }
}
