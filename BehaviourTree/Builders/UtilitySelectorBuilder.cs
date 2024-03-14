using System;

namespace Easy.BehaviourTree
{
    public class UtilitySelectorBuilder<T, V> : Builder<T, V, UtilitySelector<T, V>> where T : IBlackboard<V> 
    {

        UtilitySelector<T,V> Selector;
        public UtilitySelectorBuilder(UtilitySelector<T, V> current) : base()
        {
            Selector = current;

            if(Selector.Childs == null)
                Selector.Childs = new System.Collections.Generic.List<INode<T, V>>();

            if(Selector.UtilityScore == null)
                Selector.UtilityScore = new System.Collections.Generic.List<Func<T, int>>();
        }

        public UtilitySelectorBuilder<T, V> AddChild(INode<T, V> node, Func<T, int> UtilityScoreGetter )
        {
            if(Selector.Childs.Count != Selector.UtilityScore.Count)
                throw new Exception("Forgot to set Utlility after some child ?");

            Selector.AddChild(node, UtilityScoreGetter);
            return this;
        }

        public override UtilitySelector<T, V> Build() { 
            if(Selector.Childs.Count != Selector.UtilityScore.Count)
                throw new Exception("Forgot to set Utlility after some child ?");

            return Selector;
        } 
    }
}