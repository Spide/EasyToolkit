namespace Easy.BehaviourTree
{
    public class RunningDecorator<T> : DecoratorNode<T> where T : IBlackboard
    {
        public RunningDecorator()
        {
        }

        public override Result Run()
        {
            return Result.RUNNING;
        }

    }
}