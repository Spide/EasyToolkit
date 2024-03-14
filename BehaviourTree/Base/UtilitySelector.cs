using System;
using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public class UtilitySelector<T, V> : CompositeNode<T, V> where T : IBlackboard<V>
    {

        private List<Func<T, int>> utilityScore;

        public UtilitySelector()
        {
        }

        public List<Func<T, int>> UtilityScore { get => utilityScore; set => utilityScore = value; }

        public UtilitySelector<T, V> AddChild(INode<T, V> node, Func<T, int> UtilityScoreGetter)
        {
            addChild(node);

             if (utilityScore == null)
                utilityScore = new List<Func<T, int>>();

            utilityScore.Add(UtilityScoreGetter);
            return this;
        }


        public override Result Run()
        {
            int score = 0;
            INode<T,V> current = null;

            for (int i = 0; i < Childs.Count; i++)
            {
                var calculateScore = UtilityScore[i].Invoke(blackboard);
                if (calculateScore > score )
                {
                    score = calculateScore;
                    current = Childs[i];
                }
            }

            return score > 0 ? current.Run() : Result.FAILED; 
        }
    }

}