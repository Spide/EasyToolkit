namespace Easy.BehaviourTree
{
    public interface IBehaviourTreeDebugNote
    {
        string Key { get; }
        string Name { get; }
        string Description { get; }
    }

    public class NoteProxy<T, V> : ProxyDecorator<T, V>, IBehaviourTreeDebugNote where T : IBlackboard<V>
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public NoteProxy(string name) : this(name, null)
        {
        }

        public NoteProxy(string name, string description) : this(null, name, description)
        {
        }

        public NoteProxy(string key, string name, string description) : base(null)
        {
            Key = string.IsNullOrEmpty(key) ? name : key;
            Name = string.IsNullOrEmpty(name) ? Key : name;
            Description = description;
        }
    }
}
