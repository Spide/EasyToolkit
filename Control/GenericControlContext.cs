using System;

namespace Easy.Control
{
    public class GenericControlContext : ControlContext
    {
        public GenericControlContext(int priority, params EventAction[] eventActions)
        {
            Array.ForEach(eventActions, e => SetEvent(e.EventName, e.Action));
        }
    }
}
