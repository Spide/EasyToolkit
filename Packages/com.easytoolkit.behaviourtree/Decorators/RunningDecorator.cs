namespace Easy.BehaviourTree
{
    public class RunningDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        public RunningDecorator()
        {
        }

        public override Result Run()
        {
            Child.Run();
            return Result.RUNNING;
        }

    }
}