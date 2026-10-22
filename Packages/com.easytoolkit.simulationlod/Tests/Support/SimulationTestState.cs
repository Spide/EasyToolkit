using UnityEngine;

namespace Easy.SimulationLod.Tests
{
    /// <summary>Game-side state of a test agent: eligibility, stickiness and health.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class SimulationTestState : MonoBehaviour, ISimulationEligibility, ISimulationStickiness, ISimulationHealth
    {
        public bool Alive = true;
        public bool Alerted;
        public float Health = -1f;

        public bool IsSimulationEligible => Alive;
        public bool IsSimulationSticky => Alerted;
        public float SimulationHealth => Health;
    }
}
