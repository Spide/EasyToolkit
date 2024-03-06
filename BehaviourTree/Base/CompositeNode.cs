

using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public abstract class CompositeNode<T> : Node<T> where T : IBlackboard
    {
        public List<INode<T>> Childs { get; protected set; }

        public void addChild(INode<T> child)
        {
            if (Childs == null)
                Childs = new List<INode<T>>();

            Childs.Add(child);
        }

    }

}