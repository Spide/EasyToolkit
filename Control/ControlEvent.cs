using System;

namespace Easy.Control
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class ControlEvent : Attribute
    {
        public string Name { get; }
        public string Schema { get; }

        public ControlEvent(string name, string schema = null)
        {
            Name = name;
            Schema = schema;
        }
    }
}