using System;

namespace Easy.Control
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ControlEvent : Attribute
    {
        public string[] Names { get; }

        // Constructor 

        public ControlEvent(params string[] names)
        {
            Names = names;
        }

    }
}