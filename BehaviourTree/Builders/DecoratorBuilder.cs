using System;

namespace Easy.BehaviourTree
{
    public class DecoratorBuilder<T, V> : Builder<T, V, DecoratorNode<T, V>> where T : IBlackboard<V>
    {
        protected INode<T, V> ChildNode { get; set; }
        protected DecoratorNode<T, V> CurrentNode { get; set; }

        public DecoratorBuilder(INode<T, V> node) : base()
        {
            ChildNode = node;
        }

        private INode<T, V> TargetNode => CurrentNode == null ? ChildNode : CurrentNode;

        public DecoratorBuilder<T, V> Running()
        {
            var node = new RunningDecorator<T, V>
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Success()
        {
            var node = new SuccessDecorator<T, V>
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Failing()
        {
            var node = new FailureDecorator<T, V>
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Rule(Func<T, bool> rule)
        {
            var node = new ConditionDecorator<T, V>(rule)
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Proxy(Func<INode<T, V>, T, Result, Result> proxy)
        {
            var node = new ProxyDecorator<T, V>(proxy)
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Repeat(int count)
        {
            var node = new RepeatDecorator<T, V>(count)
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public DecoratorBuilder<T, V> Timeout(float timer)
        {
            var node = new TimeoutDecorator<T, V>(timer)
            {
                Child = TargetNode
            };
            CurrentNode = node;
            return this;
        }

        public override DecoratorNode<T, V> Build()
        {
            return CurrentNode;
        }
    }
}
