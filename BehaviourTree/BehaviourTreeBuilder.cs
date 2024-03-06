using System;
using System.Runtime.CompilerServices;

namespace Easy.BehaviourTree
{
    public interface IBuilder<T> where T : IBlackboard
    {
        INode<T> TargetNode {get; set; }
        INode<T> Build ();
    }

    public class SelectorBuilder : BehaviourTreeBuilder
    {
        Selector<IBlackboard> currentSelector = new Selector<IBlackboard>();

        public INode<IBlackboard> TargetNode { get => currentSelector.Childs[currentSelector.Childs.Count == 0 ? 0 : currentSelector.Childs.Count -1  ] ;}
        public SelectorBuilder(BehaviourTreeBuilder parent)
        {
            ParentBuilder = parent;
        }

        public SelectorBuilder AddChild(INode<IBlackboard> node){
            currentSelector.addChild(node);
            return this;
        }

        public DecoratorBuilder DecoratedChild(INode<IBlackboard> node){
            return new DecoratorBuilder(this, node );
        }

        public INode<IBlackboard> Build() => currentSelector;

    }

        public class DecoratorBuilder : BehaviourTreeBuilder
    {
        INode<IBlackboard> child;
        DecoratorNode<IBlackboard> decoratorNode;
        public DecoratorBuilder(BehaviourTreeBuilder parent, INode<IBlackboard> node)
        {
            ParentBuilder = parent;
            child = node;
        }

        public DecoratorBuilder rule(Func<IBlackboard, bool> rule){
            var node = new ConditionDecorator<IBlackboard>();
            node.Child = child;
            node.Rule = rule;

            decoratorNode = node;
            return this;
        }

        public INode<IBlackboard> Build() => decoratorNode;

    }

    public class BehaviourTreeBuilder : IBuilder<IBlackboard>
    {

        public BehaviourTreeBuilder ParentBuilder { get; set; }
        public INode<IBlackboard> TargetNode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public INode<IBlackboard> Build()
        {
            return TargetNode;
        }

        public static BehaviourTreeBuilder Create()
        {
            return new BehaviourTreeBuilder();
        }

        public SelectorBuilder Selector()
        {
            var builder = new SelectorBuilder(this);
            ParentBuilder = ParentBuilder;
            return builder;
        }


        void test (){
            BehaviourTreeBuilder.Create()
                .Selector()
                    .AddChild(null)
                    .AddChild(null)
                    .DecoratedChild(null)
                .Build();
        }
    }
}