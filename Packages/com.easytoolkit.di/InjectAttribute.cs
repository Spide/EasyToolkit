using System;

namespace Easy.DI
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    public class InjectAttribute : Attribute
    {
    }
}
