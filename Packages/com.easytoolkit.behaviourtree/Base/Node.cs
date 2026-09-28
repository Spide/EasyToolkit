namespace Easy.BehaviourTree
{

    public abstract class Node<T, V> : INode<T, V> where T : IBlackboard<V>
    {
        protected T blackboard;

        public Node()
        {
        }

        protected virtual R GetInput<R>(V key) => blackboard.GetVariable<R>(key);

        public virtual void Initialize(T blackboard) { this.blackboard = blackboard; }
        public abstract Result Run();

        public virtual void Setup(object[] args) {}

        public virtual void Stop() {}
    }

}