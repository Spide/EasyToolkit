namespace Easy.BehaviourTree
{
    public interface INode<T> where T : IBlackboard
    {
        Result Run();
        void Stop() { }
    }

}