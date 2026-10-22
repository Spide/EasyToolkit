using System.Collections.Generic;
using UnityEngine;

namespace Easy.SimulationLod.Tests
{
    /// <summary>
    /// Records what a component next to an agent sees: tier changes, and the parking flags inside its own
    /// OnDisable / OnEnable (ExecuteAlways, so they also run in Edit Mode tests).
    /// </summary>
    [ExecuteAlways]
    [UnityEngine.AddComponentMenu("")]
    public sealed class SimulationTestProbe : MonoBehaviour, ISimulationTierListener
    {
        public readonly List<SimulationTier> Tiers = new List<SimulationTier>();
        public int Changes;
        public bool LastParked;
        public bool LastSuspended;
        public float LastThinkScale = 1f;
        public ISimulationThinkBudget LastBudget;
        public int Disables, Enables;
        public bool SawParkingOnDisable, SawUnparkingOnEnable;
        public bool LastDisableWasPark, LastEnableWasUnpark;

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            Changes++;
            Tiers.Add(change.Current);
            LastParked = change.Parked;
            LastSuspended = change.Suspended;
            LastThinkScale = change.ThinkIntervalScale;
            LastBudget = change.ThinkBudget;
        }

        private void OnDisable()
        {
            Disables++;
            var agent = GetComponent<SimulationAgent>();
            LastDisableWasPark = agent != null && agent.IsParking;
            SawParkingOnDisable |= LastDisableWasPark;
        }

        private void OnEnable()
        {
            Enables++;
            var agent = GetComponent<SimulationAgent>();
            LastEnableWasUnpark = agent != null && agent.IsUnparking;
            SawUnparkingOnEnable |= LastEnableWasUnpark;
        }
    }
}
