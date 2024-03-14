namespace Easy.BehaviourTree
{
    public interface IBlackboard : IBlackboard<string>{}
    public interface IBlackboard<V>
    {
        T GetVariable<T>(V key);
        void SetVariable(V key, object value);
    }
}