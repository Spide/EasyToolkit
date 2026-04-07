using System;
using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public abstract class CompositeNode<T, V> : Node<T, V> where T : IBlackboard<V>
    {
        public CompositeNode() : base()
        {
            Childs = new List<INode<T, V>>();
        }

        public CompositeNode(List<INode<T, V>> childs) : base()
        {
            Childs = childs ?? new List<INode<T, V>>();
        }

        public List<INode<T, V>> Childs { get; set; }

        public override void Initialize(T blackboard)
        {
            base.Initialize(blackboard);

            foreach (var item in Childs)
            {
                if (item == null)
                    throw new InvalidOperationException($"Composite '{GetType().Name}' contains a null child.");

                item.Initialize(blackboard);
            }
        }

        public void addChild(INode<T, V> child)
        {
            AddChild(child);
        }

        public void AddChild(INode<T, V> child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (Childs == null)
                Childs = new List<INode<T, V>>();

            Childs.Add(child);
        }
    }
}
