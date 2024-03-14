using System;

namespace Easy.BehaviourTree
{
    public class CompositeBuilder<T, V> : Builder<T, V, CompositeNode<T, V>> where T : IBlackboard<V> 
    {
        protected CompositeNode<T, V> CurrentNode {get; set;}

        public CompositeBuilder(CompositeNode<T, V> current) : base()
        {
            CurrentNode = current;
        }

        public CompositeBuilder<T, V> AddChild(INode<T, V> node)
        {
            CurrentNode.addChild(node);
            return this;
        }

        public CompositeBuilder<T, V> AddChild(Type node)
        {
            CurrentNode.addChild(TreeBuilder<T,V>.Node(node));
            return this;
        }

        public CompositeBuilder<T, V> AddChild(Type node, params object[] args)
        {
            CurrentNode.addChild(TreeBuilder<T,V>.Node(node, args));
            return this;
        }

        public override CompositeNode<T, V> Build() => CurrentNode;

    }
}