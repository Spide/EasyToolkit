using System;
using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public readonly struct BehaviourTreeNodeVisit<T, V> where T : IBlackboard<V>
    {
        public BehaviourTreeNodeVisit(INode<T, V> node, INode<T, V> parent, int depth)
        {
            Node = node;
            Parent = parent;
            Depth = depth;
        }

        public INode<T, V> Node { get; }
        public INode<T, V> Parent { get; }
        public int Depth { get; }
    }

    public static class BehaviourTreeTraversal
    {
        public static IEnumerable<BehaviourTreeNodeVisit<T, V>> Traverse<T, V>(INode<T, V> root) where T : IBlackboard<V>
        {
            return Traverse(root, null, 0, new HashSet<INode<T, V>>());
        }

        public static INode<T, V> FindByKey<T, V>(INode<T, V> root, string key) where T : IBlackboard<V>
        {
            foreach (var visit in Traverse(root))
            {
                if (HasKey(visit.Node, key))
                    return visit.Node;
            }

            return null;
        }

        public static bool TryFindByKey<T, V>(INode<T, V> root, string key, out INode<T, V> node) where T : IBlackboard<V>
        {
            node = FindByKey(root, key);
            return node != null;
        }

        public static IEnumerable<INode<T, V>> FindAllByKey<T, V>(INode<T, V> root, string key) where T : IBlackboard<V>
        {
            foreach (var visit in Traverse(root))
            {
                if (HasKey(visit.Node, key))
                    yield return visit.Node;
            }
        }

        public static IEnumerable<INode<T, V>> FindNotes<T, V>(INode<T, V> root) where T : IBlackboard<V>
        {
            foreach (var visit in Traverse(root))
            {
                if (visit.Node is IBehaviourTreeDebugNote)
                    yield return visit.Node;
            }
        }

        public static bool TryGetNote<T, V>(INode<T, V> node, out IBehaviourTreeDebugNote note) where T : IBlackboard<V>
        {
            note = node as IBehaviourTreeDebugNote;
            return note != null;
        }

        private static bool HasKey<T, V>(INode<T, V> node, string key) where T : IBlackboard<V>
        {
            return node is IBehaviourTreeDebugNote note
                && string.Equals(note.Key, key, StringComparison.Ordinal);
        }

        private static IEnumerable<BehaviourTreeNodeVisit<T, V>> Traverse<T, V>(
            INode<T, V> node,
            INode<T, V> parent,
            int depth,
            HashSet<INode<T, V>> visited) where T : IBlackboard<V>
        {
            if (node == null || !visited.Add(node))
                yield break;

            yield return new BehaviourTreeNodeVisit<T, V>(node, parent, depth);

            if (node is DecoratorNode<T, V> decorator)
            {
                foreach (var visit in Traverse(decorator.Child, node, depth + 1, visited))
                    yield return visit;
            }

            if (node is CompositeNode<T, V> composite && composite.Childs != null)
            {
                foreach (var child in composite.Childs)
                {
                    foreach (var visit in Traverse(child, node, depth + 1, visited))
                        yield return visit;
                }
            }
        }
    }
}
