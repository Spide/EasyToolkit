namespace Easy.BehaviourTree
{
    public interface INode<T, V> where T : IBlackboard<V>
    {
        void Initialize(T blackboard);
        void Setup(object[] args);
        Result Run();
        void Stop();
    }

}