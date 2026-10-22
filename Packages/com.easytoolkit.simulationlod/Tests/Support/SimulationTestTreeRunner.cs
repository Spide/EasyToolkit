using Easy.BehaviourTree;
using Easy.SimulationLod.BehaviourTree;

namespace Easy.SimulationLod.Tests
{
    [UnityEngine.AddComponentMenu("")]
    public sealed class SimulationTestTreeRunner : SimulationBehaviourTreeRunner<SimulationTestBlackboard, string>
    {
        public SimulationCountingNode Leaf { get; private set; }

        protected override SimulationTestBlackboard CreateBlackboard() => new SimulationTestBlackboard();

        protected override INode<SimulationTestBlackboard, string> CreateTree(SimulationTestBlackboard blackboard)
        {
            Leaf = new SimulationCountingNode();
            return Leaf;
        }
    }
}
