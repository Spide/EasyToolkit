using System;
using System.Collections.Generic;

namespace Easy.State
{
    public interface IState : IState<object> {}
    public interface IState<T> : IDictionary<T, object>
    {
        event Action<T, object> OnChange;

        int Plus(T resource, int value);
        int Get(T resource);
        R Get<R>(T resource);
        void Set(T resource, object value);
    }
}