using System.Collections.Generic;
namespace Easy.State
{
    public class StateBuilder : StateBuilder<string>{}
    public class StateBuilder<K>
    {
        private readonly Dictionary<K, object> state;

        public StateBuilder (Dictionary<K, object> state = null){
            this.state = state ?? new Dictionary<K, object>();
        }

        public static StateBuilder<K> Create()
        {
            return new StateBuilder<K>();
        }

        public StateBuilder<K> Plus(K resource, int value)
        {
            state.Add(resource, value);
            return this;
        }

        public StateBuilder<K> Minus(K resource, int value)
        {
            state.Add(resource, -value);
            return this;
        }

        public StateBuilder<K> Set(K resource, object value)
        {
            state[resource] = value;
            return this;
        }

        public T Build<T>() where T : IState<K>, new()
        {
            var builder = new T();
            foreach (var pair in state)
                builder.Add(pair.Key, pair.Value);

            return  builder;
        }

        public GenericState<K> Build()
        {
            return Build<GenericState<K>>();
        }
    }
}