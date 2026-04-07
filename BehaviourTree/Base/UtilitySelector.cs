using System;
using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public class UtilitySelector<T, V> : CompositeNode<T, V> where T : IBlackboard<V>
    {
        private List<Func<T, int>> utilityScore;

        public List<Func<T, int>> UtilityScore { get => utilityScore; set => utilityScore = value; }

        public UtilitySelector<T, V> AddChild(INode<T, V> node, Func<T, int> utilityScoreGetter)
        {
            if (utilityScoreGetter == null)
                throw new ArgumentNullException(nameof(utilityScoreGetter));

            AddChild(node);

            if (utilityScore == null)
                utilityScore = new List<Func<T, int>>();

            utilityScore.Add(utilityScoreGetter);
            return this;
        }

        public override Result Run()
        {
            if (Childs == null || Childs.Count == 0)
                return Result.FAILED;

            int bestScore = int.MinValue;
            INode<T, V> current = null;

            for (int i = 0; i < Childs.Count; i++)
            {
                var calculateScore = UtilityScore[i].Invoke(blackboard);
                if (calculateScore > bestScore)
                {
                    bestScore = calculateScore;
                    current = Childs[i];
                }
            }

            return current != null ? current.Run() : Result.FAILED;
        }
    }
}
