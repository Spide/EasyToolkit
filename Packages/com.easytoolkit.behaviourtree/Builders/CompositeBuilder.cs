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

        public CompositeBuilder<T, V> AddChild(INode<T, V> node, string name)
        {
            return AddChild(node, name, null);
        }

        public CompositeBuilder<T, V> AddChild(INode<T, V> node, string name, string description)
        {
            CurrentNode.addChild(TreeBuilder<T, V>.Note(node, name, description));
            return this;
        }

        public CompositeBuilder<T, V> AddChild(INode<T, V> node, string key, string name, string description)
        {
            CurrentNode.addChild(TreeBuilder<T, V>.Note(node, key, name, description));
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

        public CompositeBuilder<T, V> AddChildNamed(Type node, string name, string description, params object[] args)
        {
            CurrentNode.addChild(TreeBuilder<T, V>.Note(TreeBuilder<T, V>.Node(node, args), name, description));
            return this;
        }

        public CompositeBuilder<T, V> AddChildKeyed(Type node, string key, string name, string description, params object[] args)
        {
            CurrentNode.addChild(TreeBuilder<T, V>.Note(TreeBuilder<T, V>.Node(node, args), key, name, description));
            return this;
        }

        public CompositeBuilder<T, V> AddChildNamed(Type node, string name, params object[] args)
        {
            return AddChildNamed(node, name, null, args);
        }

        public CompositeBuilder<T, V> AddChildKeyed(Type node, string key, string name, params object[] args)
        {
            return AddChildKeyed(node, key, name, null, args);
        }

        public override CompositeNode<T, V> Build() => CurrentNode;

    }
}
