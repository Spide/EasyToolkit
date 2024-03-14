using Easy.BehaviourTree;

namespace Easy.BehaviourTree
{
    public class NoteProxy<T, V> : ProxyDecorator<T, V> where T : IBlackboard<V>
    {
        public string Name { get; set; }
        public NoteProxy(string name) : base(null)
        {
            Name = name;
        }

    }

}

