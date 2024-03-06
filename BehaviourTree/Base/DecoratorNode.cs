namespace Easy.BehaviourTree
{
    public abstract class DecoratorNode<T> : Node<T> where T : IBlackboard
    {
        public INode<T> Child { get; set; }

        public virtual DecoratorNode<T> Initialize(T blackboard, INode<T> child)
        {
            base.Initialize(blackboard);
            Child = child;
            return this;
        }


    }

}