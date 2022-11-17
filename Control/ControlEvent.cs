using System;

namespace Easy.Control
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ControlEvent : Attribute
    {
        public string Name { get; }

        public ControlEvent(string names)
        {
            Name = names;
        }
    }
}