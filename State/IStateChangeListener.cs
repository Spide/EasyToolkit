using System;

namespace Easy.State
{
    public interface IStateListener<K>
    {
        Action<object> On(K resource, Action<object> action);
        Action<object> On<T>(K resource, Action<T> action);
        void Off(K resource, Action<object> action);

        void AddState(IState<K> state);
        void RemoveState(IState<K> state);
    }
}