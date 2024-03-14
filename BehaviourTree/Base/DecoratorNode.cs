namespace Easy.BehaviourTree
{
    public abstract class DecoratorNode<T, V> : Node<T, V> where T : IBlackboard<V>
    {
        public DecoratorNode() : base()
        {
        }

        protected DecoratorNode(INode<T, V> child) : base()
        {
            Child = child;
        }

        public INode<T,V> Child { get; set; }

        public override void Initialize(T blackboard)
        {
            base.Initialize(blackboard);
            Child.Initialize(blackboard);
        }


    }

}