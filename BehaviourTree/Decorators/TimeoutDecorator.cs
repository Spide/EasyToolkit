using System;
using UnityEngine;

namespace Easy.BehaviourTree
{
    public class TimeoutDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {

        public float Timer {get; set;} = 0;

        public TimeoutDecorator(float timer)
        {
            Timer = timer;
        }

        public override Result Run()
        {
            if(Timer <= 0)
                return Result.SUCCESS;

            Timer = Timer - Time.deltaTime;

            Child.Run();

            return Timer > 0 ? Result.RUNNING : Result.SUCCESS;

        }
    }
}