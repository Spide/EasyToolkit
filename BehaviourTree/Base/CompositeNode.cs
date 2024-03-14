

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
            Childs = childs;
        }

        public List<INode<T, V>> Childs { get; set; }

        public override void Initialize(T blackboard)
        {
            base.Initialize(blackboard);

            foreach (var item in Childs)
                item.Initialize(blackboard);
            
        }

        public void addChild(INode<T, V> child)
        {
            if (Childs == null)
                Childs = new List<INode<T, V>>();

            Childs.Add(child);
        }

    }

}