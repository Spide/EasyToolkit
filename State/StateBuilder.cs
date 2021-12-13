using System;

namespace Easy.State
{
    public class StateBuilder<K, S>
    {
        private readonly IState<K> state;

        public StateBuilder (IState<K> state){
            this.state = state;
        }

        public static StateBuilder<string, GenericState<string>> Create()
        {
            return new StateBuilder<string, GenericState<string>>(new GenericState<string>());
        }

        public static StateBuilder<K,S> Create<T>()  where T : IState<K>, new()
        {
            return new StateBuilder<K,S>(new T());
        }

        public StateBuilder<K, S> Plus(K resource, int value)
        {
            state.Add(resource, value);
            return this;
        }

        public StateBuilder<K, S> Minus(K resource, int value)
        {
            state.Add(resource, -value);
            return this;
        }

        public StateBuilder<K, S> Set(K resource, object value)
        {
            state.Set(resource, value);
            return this;
        }

        public S Build()
        {
            return (S) state;
        }
    }
}