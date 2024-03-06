namespace Easy.BehaviourTree
{

    public abstract class Node<T> : INode<T> where T : IBlackboard
    {
        protected T blackboard;
        public virtual Node<T> Initialize(T blackboard) { this.blackboard = blackboard; return this; }
        public abstract Result Run();
        public virtual void Stop() { }
    }

}