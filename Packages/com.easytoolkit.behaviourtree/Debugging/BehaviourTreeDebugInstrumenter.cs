using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public static class BehaviourTreeDebugInstrumenter
    {
        public static INode<T, V> Wrap<T, V>(INode<T, V> root) where T : IBlackboard<V>
        {
            var treeId = new object();
            return Wrap(root, treeId, true, new Dictionary<INode<T, V>, INode<T, V>>());
        }

        private static INode<T, V> Wrap<T, V>(
            INode<T, V> node,
            object treeId,
            bool startsTreeRun,
            Dictionary<INode<T, V>, INode<T, V>> wrapped) where T : IBlackboard<V>
        {
            if (node == null || node is ObservedProxyDecorator<T, V>)
                return node;

            if (wrapped.TryGetValue(node, out var existing))
                return existing;

            var proxy = new ObservedProxyDecorator<T, V>(node, treeId, startsTreeRun);
            wrapped.Add(node, proxy);

            if (node is CompositeNode<T, V> composite && composite.Childs != null)
            {
                for (int i = 0; i < composite.Childs.Count; i++)
                    composite.Childs[i] = Wrap(composite.Childs[i], treeId, false, wrapped);
            }
            else if (node is DecoratorNode<T, V> decorator)
            {
                decorator.Child = Wrap(decorator.Child, treeId, false, wrapped);
            }

            return proxy;
        }
    }
}
