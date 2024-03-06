using System;

namespace Easy.BehaviourTree
{
    public class ConditionDecorator<T> : DecoratorNode<T> where T : IBlackboard
    {
        Func<T, bool> rule;

        public Func<T, bool> Rule { get => rule; set => rule = value; }

        public DecoratorNode<T> Initialize(T blackboard, INode<T> child, Func<T, bool> rule)
        {
            this.Rule = rule;
            return base.Initialize(blackboard, child);
        }
        public override Result Run()
        {
            return Rule.Invoke(blackboard) ? Child.Run() : Result.FAILED;

        }
    }
}