namespace Easy.BehaviourTree
{
    public class RepeatDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        private readonly int initialCount;

        public int Counter { get; private set; }

        public RepeatDecorator(int counter)
        {
            initialCount = counter < 0 ? 0 : counter;
            Counter = initialCount;
        }

        public override Result Run()
        {
            if (Counter <= 0)
                return Result.SUCCESS;

            for (int i = 0; i < Counter; i++)
            {
                var run = Child.Run();
                if (run == Result.FAILED)
                    return Result.FAILED;

                if (run == Result.RUNNING)
                    return Result.RUNNING;

                Counter--;
                i--;
            }

            return Counter <= 0 ? Result.SUCCESS : Result.RUNNING;
        }

        public override void Stop()
        {
            Counter = initialCount;
            Child?.Stop();
        }
    }
}
