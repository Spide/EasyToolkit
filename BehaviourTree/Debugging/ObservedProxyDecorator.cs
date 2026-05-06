using System;

namespace Easy.BehaviourTree
{
    public class ObservedProxyDecorator<T, V> : DecoratorNode<T, V> where T : IBlackboard<V>
    {
        private readonly object treeId;

        public ObservedProxyDecorator(INode<T, V> child)
        {
            treeId = this;
            StartsTreeRun = true;
            Child = child;
        }

        internal ObservedProxyDecorator(INode<T, V> child, object treeId, bool startsTreeRun)
        {
            this.treeId = treeId;
            StartsTreeRun = startsTreeRun;
            Child = child;
        }

        internal bool StartsTreeRun { get; set; }

        public override Result Run()
        {
            BehaviourTreeDebugRegistry.RecordStarted(Child, treeId, StartsTreeRun);

            try
            {
                var result = Child.Run();
                return BehaviourTreeDebugRegistry.RecordFinished(Child, result);
            }
            catch (Exception exception)
            {
                BehaviourTreeDebugRegistry.RecordException(Child, exception);
                throw;
            }
        }

        public override void Stop()
        {
            Child?.Stop();
        }
    }
}
