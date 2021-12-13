using System;
using System.Collections.Generic;

namespace Easy.State
{
    public interface IState<T>
    {
        ICollection<T> Keys { get; }

        event Action<T, object> OnChange;

        int Add(T resource, int value);
        int Get(T resource);
        R Get<R>(T resource);
        void Set(T resource, object value);
    }
}