namespace Easy.BehaviourTree
{
    public class FailureDecorator<T> : DecoratorNode<T> where T : IBlackboard
    {
        public override Result Run()
        {
            return Result.FAILED;
        }

    }
}