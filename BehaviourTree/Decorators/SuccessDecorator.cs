namespace Easy.BehaviourTree
{
    public class SuccessDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        public override Result Run()
        {
            Child.Run();
            return Result.SUCCESS;
        }

    }
}