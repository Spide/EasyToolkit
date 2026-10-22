using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Easy.SimulationLod.Tests
{
    /// <summary>What a tier does to one agent and its listeners, and that switching back restores it.</summary>
    public sealed class SimulationAgentTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [Test]
        public void AnimatorCullingFollowsTheTierAndComesBack()
        {
            GameObject go = NewObject("Animated");
            Animator animator = go.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            go.AddComponent<SimulationAnimatorListener>();
            var agent = go.AddComponent<SimulationAgent>();
            agent.ParkWhenOff = false;
            agent.Apply(SimulationTier.Minimal);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            agent.Apply(SimulationTier.Off);
            Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            agent.Apply(SimulationTier.Full);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode);
            agent.Apply(SimulationTier.Dormant);
            Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode, "A dormant agent is frozen.");
        }

        [Test]
        public void ListenersHearEveryTierWithTheThinkScale()
        {
            GameObject go = NewObject("Listened");
            var agent = go.AddComponent<SimulationAgent>();
            var probe = go.AddComponent<SimulationTestProbe>();
            var child = NewObject("Child");
            child.transform.SetParent(go.transform);
            var childProbe = child.AddComponent<SimulationTestProbe>();
            agent.ParkWhenOff = false;
            agent.MinimalThinkScale = 4f;
            agent.RefreshComponents();
            int raised = 0;
            agent.TierChanged += _ => raised++;

            agent.Apply(SimulationTier.Minimal);
            Assert.AreEqual(4f, probe.LastThinkScale);
            agent.Apply(SimulationTier.Off);
            agent.Apply(SimulationTier.Full);
            Assert.AreEqual(new[] { SimulationTier.Minimal, SimulationTier.Off, SimulationTier.Full }, probe.Tiers);
            Assert.AreEqual(3, childProbe.Changes, "Listeners on children are found too.");
            Assert.AreEqual(1f, probe.LastThinkScale);
            Assert.AreEqual(3, raised);

            var plain = new PlainListener();
            agent.AddListener(plain);
            agent.Apply(SimulationTier.Minimal);
            Assert.AreEqual(SimulationTier.Minimal, plain.Last, "A plain object added as a listener is told as well.");
            agent.RemoveListener(plain);
            agent.Apply(SimulationTier.Full);
            Assert.AreEqual(SimulationTier.Minimal, plain.Last);
        }

        [Test]
        public void OffParksTheObjectAndAnyOtherTierBringsItBack()
        {
            GameObject go = NewObject("Parked");
            var agent = go.AddComponent<SimulationAgent>();
            Assert.IsTrue(agent.ParkWhenOff, "Parking is the default.");
            agent.Apply(SimulationTier.Off);
            Assert.IsFalse(go.activeSelf);
            Assert.IsTrue(agent.IsParked);
            Assert.AreEqual(SimulationTier.Off, agent.Tier);
            agent.Apply(SimulationTier.Minimal);
            Assert.IsTrue(go.activeSelf);
            Assert.IsFalse(agent.IsParked);
            agent.Apply(SimulationTier.Off);
            agent.Apply(SimulationTier.Full);
            Assert.IsTrue(go.activeSelf);
        }

        [Test]
        public void DormantFreezesInPlaceUnlessItMayPark()
        {
            GameObject go = NewObject("Sleeper");
            var agent = go.AddComponent<SimulationAgent>();
            var probe = go.AddComponent<SimulationTestProbe>();
            agent.Apply(SimulationTier.Dormant);
            Assert.IsTrue(go.activeSelf, "A sleeping agent stays visible by default.");
            Assert.IsFalse(agent.IsParked);
            Assert.AreEqual(SimulationTier.Dormant, probe.Tiers[probe.Tiers.Count - 1]);

            agent.Apply(SimulationTier.Full);
            agent.ParkWhenDormant = true;
            agent.Apply(SimulationTier.Dormant);
            Assert.IsFalse(go.activeSelf);
            Assert.IsTrue(agent.IsParked);
            Assert.IsTrue(probe.LastParked, "Listeners hear the park before the object goes inactive.");
            agent.Apply(SimulationTier.Full);
            Assert.IsTrue(go.activeSelf);
        }

        [Test]
        public void ParkingFlagsAreSeenOnlyInsideTheAgentsOwnParkAndUnpark()
        {
            GameObject go = NewObject("Flags");
            var agent = go.AddComponent<SimulationAgent>();
            var probe = go.AddComponent<SimulationTestProbe>();
            agent.Apply(SimulationTier.Off);
            Assert.IsTrue(probe.LastDisableWasPark, "The park's OnDisable sees IsParking.");
            Assert.IsFalse(agent.IsParking, "Only inside the park.");
            agent.Apply(SimulationTier.Full);
            Assert.IsTrue(probe.LastEnableWasUnpark, "The wake's OnEnable sees IsUnparking.");
            Assert.IsFalse(agent.IsUnparking);

            go.SetActive(false);
            Assert.IsFalse(probe.LastDisableWasPark, "Any other disable never sees IsParking.");
            go.SetActive(true);
            Assert.IsFalse(probe.LastEnableWasUnpark, "Any other activation never sees IsUnparking.");
        }

        [Test]
        public void BehaviourToggleRunsComponentsOnlyUpToItsTier()
        {
            GameObject go = NewObject("Toggled");
            var sensor = go.AddComponent<SimulationTestState>();
            var toggle = go.AddComponent<SimulationBehaviourToggle>();
            toggle.Targets = new Behaviour[] { sensor };
            toggle.EnabledUpTo = SimulationTier.Minimal;
            var agent = go.AddComponent<SimulationAgent>();
            agent.ParkWhenOff = false;
            agent.Apply(SimulationTier.Minimal);
            Assert.IsTrue(sensor.enabled);
            agent.Apply(SimulationTier.Off);
            Assert.IsFalse(sensor.enabled);
            agent.Apply(SimulationTier.Full);
            Assert.IsTrue(sensor.enabled, "The authored state comes back.");
        }

        [Test]
        public void NavMeshAgentListenerHoldsStillWithoutANavMeshAndNeverThrows()
        {
            GameObject go = NewObject("Walker");
            go.AddComponent<NavMeshAgent>();
            var listener = go.AddComponent<SimulationNavMeshAgentListener>();
            var agent = go.AddComponent<SimulationAgent>();
            agent.ParkWhenOff = false;
            agent.Apply(SimulationTier.Off);
            Assert.IsTrue(listener.IsHoldingStopped);
            agent.Apply(SimulationTier.Minimal);
            Assert.IsFalse(listener.IsHoldingStopped);
            Assert.AreEqual(ObstacleAvoidanceType.LowQualityObstacleAvoidance, go.GetComponent<NavMeshAgent>().obstacleAvoidanceType);
            agent.Apply(SimulationTier.Full);
            Assert.AreEqual(ObstacleAvoidanceType.HighQualityObstacleAvoidance, go.GetComponent<NavMeshAgent>().obstacleAvoidanceType);
        }

        [Test]
        public void EligibilityStickinessAndHealthComeFromComponents()
        {
            GameObject go = NewObject("Stated");
            var state = go.AddComponent<SimulationTestState>();
            var agent = go.AddComponent<SimulationAgent>();
            Assert.IsTrue(agent.IsSimulationEligible);
            Assert.IsFalse(agent.IsSimulationSticky);
            Assert.AreEqual(-1f, agent.SimulationHealth);
            state.Alive = false;
            state.Alerted = true;
            state.Health = 7f;
            Assert.IsFalse(agent.IsSimulationEligible);
            Assert.IsTrue(agent.IsSimulationSticky);
            Assert.AreEqual(7f, agent.SimulationHealth);
        }

        [Test]
        public void SegmentMapAnswersPositionsAndBuildsItsGraph()
        {
            // Two columns, two rows of 2 m cells: west column is segment 0, east column segment 1.
            var map = NewObject("Segments").AddComponent<SimulationSegmentMap>();
            map.Configure(2, 2, 2f, new[] { "West", "East" }, new byte[] { 0, 1, 0, SimulationSegmentMap.None }, new[] { 0, 1 });
            Assert.AreEqual(0, map.SegmentAt(new Vector3(-1f, 0f, 1f)));
            Assert.AreEqual(1, map.SegmentAt(new Vector3(1f, 0f, 1f)));
            Assert.AreEqual(-1, map.SegmentAt(new Vector3(1f, 0f, -1f)), "A cell with no segment.");
            Assert.AreEqual(-1, map.SegmentAt(new Vector3(9f, 0f, 0f)), "Outside the map.");
            Assert.IsTrue(map.BuildGraph().AreNeighbours(0, 1));
        }

        private sealed class PlainListener : ISimulationTierListener
        {
            public SimulationTier Last = SimulationTier.Full;
            public void OnSimulationTierChanged(in SimulationTierChange change) => Last = change.Current;
        }
    }
}
