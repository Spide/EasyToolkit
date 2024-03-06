namespace Easy.BehaviourTree
{
    public class SuccessDecorator<T> : DecoratorNode<T> where T : IBlackboard
    {
        public override Result Run()
        {
            return Result.SUCCESS;
        }

    }
}