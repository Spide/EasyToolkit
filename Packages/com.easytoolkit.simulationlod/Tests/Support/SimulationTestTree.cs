using System.Collections.Generic;
using Easy.BehaviourTree;

namespace Easy.SimulationLod.Tests
{
    public sealed class SimulationTestBlackboard : IBlackboard<string>
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public T GetVariable<T>(string key) => values.TryGetValue(key, out object value) ? (T)value : default;
        public void SetVariable(string key, object value) => values[key] = value;
    }

    /// <summary>A leaf that counts its runs and stops and keeps running. Allocation-free.</summary>
    public sealed class SimulationCountingNode : Node<SimulationTestBlackboard, string>
    {
        public int Runs;
        public int Stops;
        public bool Initialized;

        public override void Initialize(SimulationTestBlackboard blackboard)
        {
            base.Initialize(blackboard);
            Initialized = true;
        }

        public override Result Run()
        {
            Runs++;
            return Result.RUNNING;
        }

        public override void Stop() => Stops++;
    }
}
