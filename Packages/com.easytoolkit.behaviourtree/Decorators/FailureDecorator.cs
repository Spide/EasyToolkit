namespace Easy.BehaviourTree
{
    public class FailureDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        public override Result Run()
        {
            Child.Run();
            return Result.FAILED;
        }

    }
}