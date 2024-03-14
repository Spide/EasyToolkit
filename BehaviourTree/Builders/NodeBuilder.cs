using System;
using UnityEngine;

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
            if (baseType.IsAssignableFrom(typeof(N)))
            {
                return (N)Activator.CreateInstance(typeof(N));
            }

            throw new Exception("Not valid type for node");
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
            if (baseType.IsAssignableFrom(nodeType))
            {
                return (R)Activator.CreateInstance(nodeType, args);
            }

            throw new Exception("Not valid type for node");
        }

    }

}