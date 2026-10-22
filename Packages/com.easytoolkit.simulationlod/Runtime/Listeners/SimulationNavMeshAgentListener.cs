using UnityEngine;
using UnityEngine.AI;

namespace Easy.SimulationLod
{
    /// <summary>
    /// Stops a NavMeshAgent while its agent does not think (Off without parking, Dormant, suspension) and lets it
    /// go again afterwards, restoring its own stopped flag. Parking needs nothing: an inactive object does not move.
    /// Optionally lowers the obstacle avoidance quality below Full.
    /// </summary>
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation NavMeshAgent Listener")]
    public sealed class SimulationNavMeshAgentListener : MonoBehaviour, ISimulationTierListener
    {
        [Tooltip("Empty: the NavMeshAgent on this object.")]
        [SerializeField] private NavMeshAgent navMeshAgent;
        [Tooltip("Below Full, use this avoidance quality (restored at Full).")]
        [SerializeField] private bool reduceAvoidance = true;
        [SerializeField] private ObstacleAvoidanceType reducedAvoidance = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        private bool stoppedByLod;
        private bool wasStopped;
        private bool avoidanceCaptured;
        private ObstacleAvoidanceType fullAvoidance;

        public NavMeshAgent NavMeshAgent
        {
            get
            {
                if (navMeshAgent == null && TryGetComponent(out NavMeshAgent found)) navMeshAgent = found;
                return navMeshAgent;
            }
            set => navMeshAgent = value;
        }

        /// <summary>True while this listener holds the NavMeshAgent stopped.</summary>
        public bool IsHoldingStopped => stoppedByLod;

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            NavMeshAgent target = NavMeshAgent;
            if (target == null || change.Parked) return;

            if (reduceAvoidance)
            {
                if (!avoidanceCaptured)
                {
                    avoidanceCaptured = true;
                    fullAvoidance = target.obstacleAvoidanceType;
                }

                target.obstacleAvoidanceType = change.Current == SimulationTier.Full ? fullAvoidance : reducedAvoidance;
            }

            bool hold = change.ThinkSuspended;
            if (hold == stoppedByLod) return;
            bool canDrive = target.isActiveAndEnabled && target.isOnNavMesh;
            if (hold)
            {
                stoppedByLod = true;
                if (!canDrive) return;
                wasStopped = target.isStopped;
                target.isStopped = true;
                target.velocity = Vector3.zero;
                return;
            }

            stoppedByLod = false;
            if (canDrive) target.isStopped = wasStopped;
        }
    }
}
