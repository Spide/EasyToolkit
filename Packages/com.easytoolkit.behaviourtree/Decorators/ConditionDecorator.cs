using System;

namespace Easy.BehaviourTree
{
    public class ConditionDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {

        public Func<T, bool> Rule { get; set; }

        public ConditionDecorator(Func<T, bool> rule)
        {
            Rule = rule;
        }

        public override Result Run()
        {
            return Rule.Invoke(blackboard) ? Child.Run() : Result.FAILED;

        }
    }
}