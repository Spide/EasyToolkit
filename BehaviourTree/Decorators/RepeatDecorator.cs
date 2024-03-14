using System;
using UnityEditor;
using UnityEngine;

namespace Easy.BehaviourTree
{
    public class RepeatDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        private String guid = GUID.Generate().ToString();
        public int Counter { get; set; } = 0;

        public RepeatDecorator(int counter)
        {
            Counter = counter;
        }

        public override Result Run()
        {
            Debug.LogFormat("run repeat {0} - {1} ", Counter, guid);

            for (int i = Counter; i > 0; i--)
            {
                var run = Child.Run();
                Debug.LogFormat("run with {0} - {1} {2} ", Counter, guid, run.ToString());
                if (run == Result.FAILED)
                    return Result.FAILED;

                if (run == Result.RUNNING)
                    continue;

                Counter--;

            } 

            Debug.LogFormat("end reapeater with {0} - {1}", Counter, guid);

            return Counter <= 0 ? Result.SUCCESS : Result.RUNNING;
        }
    }
}