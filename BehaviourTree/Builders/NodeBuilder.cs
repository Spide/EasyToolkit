using System;

namespace Easy.BehaviourTree
{
    public class TreeBuilder<T, V> : TreeBuilder<T, V, INode<T, V>> where T : IBlackboard<V>
    {
    }

    public class TreeBuilder<T, V, N> where T : IBlackboard<V> where N : INode<T, V>
    {
        public static Type baseType = typeof(N);

        public static DecoratorBuilder<T, V> Decorate(INode<T, V> node)
        {
            return new DecoratorBuilder<T, V>(node);
        }

        public static DecoratorBuilder<T, V> Decorate(Type node)
        {
            return new DecoratorBuilder<T, V>(Node(node));
        }

        public static DecoratorBuilder<T, V> Decorate(Type node, params object[] args)
        {
            return new DecoratorBuilder<T, V>(Node(node, args));
        }

        public static CompositeBuilder<T, V> Selector()
        {
            return new CompositeBuilder<T, V>(new Selector<T, V>());
        }

        public static CompositeBuilder<T, V> Sequence()
        {
            return new CompositeBuilder<T, V>(new Sequence<T, V>());
        }

        public static CompositeBuilder<T, V> Parallel()
        {
            return new CompositeBuilder<T, V>(new Parallel<T, V>());
        }

        public static UtilitySelectorBuilder<T, V> Utility()
        {
            return new UtilitySelectorBuilder<T, V>(new UtilitySelector<T, V>());
        }

        public static N Node()
        {
            return Activator.CreateInstance<N>();
        }

        public static INode<T, V> Node(Type nodeType)
        {
            return Node<INode<T, V>>(nodeType);
        }

        public static INode<T, V> Node(Type nodeType, params object[] args)
        {
            return Node<INode<T, V>>(nodeType, args);
        }

        public static R Node<R>(params object[] args)
        {
            return Node<R>(typeof(R), args);
        }

        public static R Node<R>(Type nodeType, params object[] args)
        {
            if (nodeType == null)
                throw new ArgumentNullException(nameof(nodeType));

            if (!typeof(INode<T, V>).IsAssignableFrom(nodeType))
                throw new Exception($"Type '{nodeType}' does not implement INode<{typeof(T).Name}, {typeof(V).Name}>.");

            object instance = Activator.CreateInstance(nodeType, args);
            if (instance is R typed)
                return typed;

            throw new Exception($"Type '{nodeType}' cannot be cast to '{typeof(R)}'.");
        }
    }
}
