using System;

namespace Easy.BehaviourTree
{
    public class ProxyDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {

        public Func<INode<T,V>, T, Result, Result> Proxy { get; set; }

        public ProxyDecorator(Func<INode<T,V>, T, Result, Result> proxy)
        {
            Proxy = proxy;
        }

        public override Result Run()
        {
            return Proxy == null ? Child.Run() : Proxy.Invoke(Child, blackboard, Child.Run());
        }

    }
}