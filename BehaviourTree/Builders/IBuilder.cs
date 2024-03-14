namespace Easy.BehaviourTree
{
    
        public interface IBuilder<T, V> : IBuilder<T, V, INode<T, V> > where T : IBlackboard<V> 
    {

    }
    public interface IBuilder<T, V, N> where T : IBlackboard<V> where N : INode<T, V>
    {
        N Build();
        N Build(T blackboard);
    }

}