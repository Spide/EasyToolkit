using System;

namespace Easy.DI
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = true)]
    public class ResolveBy : Attribute
    {
        public ResolveBy(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}