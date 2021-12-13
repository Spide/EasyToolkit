using System;
using System.Collections.Generic;

namespace Easy.State
{
    public class GenrericStateListener<K> : IStateListener<K>
    {
        Dictionary<K, List<Action<object>>> onChangeListeners = new Dictionary<K, List<Action<object>>>();

        public GenrericStateListener(params IState<K>[] states)
        {
            Array.ForEach(states, (state) => AddState(state));
        }

        public void OnValueChangeHandler(K resource, object value)
        {
            if (onChangeListeners.TryGetValue(resource, out var listeners))
            {
                for (int i = 0; i < listeners.Count; i++)
                {
                    listeners[i].Invoke(value);
                }
            }
        }

        public Action<object> On(K resource, Action<object> action)
        {
            if (onChangeListeners.TryGetValue(resource, out var listeners))
            {
                listeners.Add(action);
            }
            else
            {
                listeners = new List<Action<object>>();
                onChangeListeners.Add(resource, listeners);
                listeners.Add(action);
            }

            return action;
        }

        public Action<object> On<T>(K resource, Action<T> action)
        {
            return On(resource, (value) => action.Invoke((T)value));
        }

        public void Off(K resource, Action<object> action)
        {
            if (onChangeListeners.TryGetValue(resource, out var listeners))
                listeners.Remove(action);
        }

        public void AddState(IState<K> state)
        {
            state.OnChange += OnValueChangeHandler;
        }

        public void RemoveState(IState<K> state)
        {
            state.OnChange -= OnValueChangeHandler;
        }
    }
}