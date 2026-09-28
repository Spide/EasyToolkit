using System;

namespace Easy.BehaviourTree
{
    public abstract class Builder<T, V> : Builder<T, V, INode<T,V>> where T : IBlackboard<V> 
    {
        
    }

    public abstract class Builder<T, V, N> : IBuilder<T, V, N> where T : IBlackboard<V> where N : INode<T, V>
    {
        public virtual N Build(T blackboard)
        {
            var node = Build();
            node.Initialize(blackboard);
            return node;
        }

        public abstract N Build();


    }
}