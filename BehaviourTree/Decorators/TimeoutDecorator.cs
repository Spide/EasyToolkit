using UnityEngine;

namespace Easy.BehaviourTree
{
    public class TimeoutDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        private readonly float initialTimer;

        public float Timer { get; private set; }

        public TimeoutDecorator(float timer)
        {
            initialTimer = timer < 0 ? 0 : timer;
            Timer = initialTimer;
        }

        public override Result Run()
        {
            if (Timer <= 0f)
                return Result.FAILED;

            var result = Child.Run();
            if (result == Result.SUCCESS || result == Result.FAILED)
                return result;

            Timer -= Time.deltaTime;
            return Timer > 0f ? Result.RUNNING : Result.FAILED;
        }

        public override void Stop()
        {
            Timer = initialTimer;
            Child?.Stop();
        }
    }
}
