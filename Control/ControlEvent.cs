using System;

namespace Easy.Control
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class ControlEvent : Attribute
    {
        public ControlEvent(string name, string schema = null)
        {
            Name = name;
            Schema = schema;
        }

        public string Name { get; }
        public string Schema { get; }
    }
}
