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

        public static NoteProxy<T, V> Note(INode<T, V> node, string name)
        {
            return Note(node, name, null);
        }

        public static NoteProxy<T, V> Note(INode<T, V> node, string name, string description)
        {
            return Note(node, null, name, description);
        }

        public static NoteProxy<T, V> Note(INode<T, V> node, string key, string name, string description)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return new NoteProxy<T, V>(key, name, description)
            {
                Child = node
            };
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

        public static INode<T, V> NamedNode(Type nodeType, string name, string description, params object[] args)
        {
            return Note(Node(nodeType, args), name, description);
        }

        public static INode<T, V> KeyedNode(Type nodeType, string key, string name, string description, params object[] args)
        {
            return Note(Node(nodeType, args), key, name, description);
        }

        public static INode<T, V> NamedNode(Type nodeType, string name, params object[] args)
        {
            return NamedNode(nodeType, name, null, args);
        }

        public static INode<T, V> KeyedNode(Type nodeType, string key, string name, params object[] args)
        {
            return KeyedNode(nodeType, key, name, null, args);
        }

        public static R Node<R>(params object[] args)
        {
            return Node<R>(typeof(R), args);
        }

        public static INode<T, V> NamedNode<R>(string name, string description, params object[] args) where R : INode<T, V>
        {
            return Note(Node<R>(args), name, description);
        }

        public static INode<T, V> KeyedNode<R>(string key, string name, string description, params object[] args) where R : INode<T, V>
        {
            return Note(Node<R>(args), key, name, description);
        }

        public static INode<T, V> NamedNode<R>(string name, params object[] args) where R : INode<T, V>
        {
            return NamedNode<R>(name, null, args);
        }

        public static INode<T, V> KeyedNode<R>(string key, string name, params object[] args) where R : INode<T, V>
        {
            return KeyedNode<R>(key, name, null, args);
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
