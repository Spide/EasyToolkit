using NUnit.Framework;

namespace Easy.SimulationLod.Tests
{
    /// <summary>The pure tier rules and the hysteresis.</summary>
    public sealed class SimulationLodSolverTests
    {
        // A row of four districts: 0 - 1 - 2 - 3.
        private static SimulationSegmentGraph Row()
        {
            var graph = new SimulationSegmentGraph(4);
            graph.Connect(0, 1);
            graph.Connect(1, 2);
            graph.Connect(2, 3);
            return graph;
        }

        [Test]
        public void ObserverSegmentIsFullNeighboursMinimalRestOff()
        {
            var tiers = new SimulationTier[4];
            SimulationLodSolver.SolveSegments(Row(), new[] { 0 }, tiers);
            Assert.AreEqual(new[] { SimulationTier.Full, SimulationTier.Minimal, SimulationTier.Off, SimulationTier.Off }, tiers);
        }

        [Test]
        public void FourSpreadObserversKeepEachOfTheirSegmentsFull()
        {
            var tiers = new SimulationTier[4];
            SimulationLodSolver.SolveSegments(Row(), new[] { 0, 3, 3, -1 }, tiers);
            Assert.AreEqual(new[] { SimulationTier.Full, SimulationTier.Minimal, SimulationTier.Minimal, SimulationTier.Full }, tiers);
        }

        [Test]
        public void StickyOrNearAgentsAreFullAnywhere()
        {
            SimulationLodSettings settings = SimulationLodSettings.Default;
            Assert.AreEqual(SimulationTier.Full, SimulationLodSolver.Desired(SimulationTier.Off, 10000f, true, in settings));
            Assert.AreEqual(SimulationTier.Full, SimulationLodSolver.Desired(SimulationTier.Off, 10f * 10f, false, in settings));
            Assert.AreEqual(SimulationTier.Minimal, SimulationLodSolver.Desired(SimulationTier.Off, 30f * 30f, false, in settings));
            Assert.AreEqual(SimulationTier.Off, SimulationLodSolver.Desired(SimulationTier.Off, 60f * 60f, false, in settings));
            Assert.AreEqual(SimulationTier.Full, SimulationLodSolver.Desired(SimulationTier.Full, 60f * 60f, false, in settings));
        }

        [Test]
        public void DistanceNeverDesiresDormant()
        {
            SimulationLodSettings settings = SimulationLodSettings.Default;
            Assert.AreEqual(SimulationTier.Off, SimulationLodSolver.Desired(SimulationTier.Dormant, 1e9f, false, in settings),
                "A segment can at most switch an agent Off.");
            Assert.AreEqual(SimulationTier.Full, SimulationLodSolver.Desired(SimulationTier.Dormant, 1f, false, in settings));
        }

        [Test]
        public void PromotionIsImmediateAndDemotionWaits()
        {
            var state = new SimulationLodState();
            Assert.IsFalse(state.Resolve(SimulationTier.Off, false, 0f, 3f), "A demotion starts its delay.");
            Assert.AreEqual(SimulationTier.Full, state.Tier);
            Assert.IsFalse(state.Resolve(SimulationTier.Off, false, 2.9f, 3f));
            Assert.IsTrue(state.Resolve(SimulationTier.Off, false, 3f, 3f));
            Assert.AreEqual(SimulationTier.Off, state.Tier);
            Assert.IsTrue(state.Resolve(SimulationTier.Full, false, 3.01f, 3f), "A promotion does not wait.");
            Assert.AreEqual(SimulationTier.Full, state.Tier);
        }

        [Test]
        public void NothingIsDemotedOnScreenAndAChangedMindRestartsTheDelay()
        {
            var state = new SimulationLodState();
            state.Resolve(SimulationTier.Off, false, 0f, 3f);
            Assert.IsFalse(state.Resolve(SimulationTier.Off, true, 5f, 3f), "On screen: stays Full.");
            Assert.IsFalse(state.Resolve(SimulationTier.Off, false, 5.1f, 3f), "The delay starts again.");
            Assert.IsFalse(state.Resolve(SimulationTier.Minimal, false, 8.2f, 3f), "A different target restarts it too.");
            Assert.IsTrue(state.Resolve(SimulationTier.Minimal, false, 11.3f, 3f));
            Assert.AreEqual(SimulationTier.Minimal, state.Tier);
        }

        [Test]
        public void ForcedTiersBypassTheDelayAndTheScreenAndLeavingDormantIsAPromotion()
        {
            var state = new SimulationLodState();
            state.Resolve(SimulationTier.Off, false, 0f, 3f);
            Assert.IsTrue(state.IsDemotionPending);
            Assert.IsTrue(state.Force(SimulationTier.Dormant), "Dormant is immediate.");
            Assert.AreEqual(SimulationTier.Dormant, state.Tier);
            Assert.IsFalse(state.IsDemotionPending, "Forcing clears a pending demotion.");
            Assert.IsFalse(state.Force(SimulationTier.Dormant), "No change, no report.");
            Assert.IsTrue(state.Resolve(SimulationTier.Off, true, 1f, 3f), "Any normal tier is a promotion out of Dormant.");
            Assert.AreEqual(SimulationTier.Off, state.Tier);
        }

        [Test]
        public void DormantAlwaysWinsAndACapOnlyWhenItRunsLess()
        {
            Assert.IsTrue(SimulationLodSolver.ApplyForced(SimulationTier.Full, SimulationTier.Dormant, out SimulationTier tier));
            Assert.AreEqual(SimulationTier.Dormant, tier);
            Assert.IsTrue(SimulationLodSolver.ApplyForced(SimulationTier.Full, SimulationTier.Minimal, out tier));
            Assert.AreEqual(SimulationTier.Minimal, tier);
            Assert.IsFalse(SimulationLodSolver.ApplyForced(SimulationTier.Off, SimulationTier.Minimal, out tier), "A cap never raises a tier.");
            Assert.AreEqual(SimulationTier.Off, tier);
            Assert.IsFalse(SimulationLodSolver.ApplyForced(SimulationTier.Minimal, SimulationTier.Full, out tier), "Full means no override.");
            Assert.AreEqual(SimulationTier.Minimal, tier);
        }

        [Test]
        public void DormantParksOnlyWhenAskedAndSuspendsThinking()
        {
            Assert.IsFalse(SimulationLodSolver.Parks(SimulationTier.Dormant, true, false));
            Assert.IsTrue(SimulationLodSolver.Parks(SimulationTier.Dormant, false, true));
            Assert.IsTrue(SimulationLodSolver.TogglesActivation(SimulationTier.Off, SimulationTier.Dormant, true, false),
                "Off parked to Dormant frozen reactivates the object.");
            Assert.IsFalse(SimulationLodSolver.TogglesActivation(SimulationTier.Off, SimulationTier.Dormant, true, true));
            Assert.IsTrue(SimulationLodSolver.SuspendsThinking(SimulationTier.Dormant));
            Assert.IsTrue(SimulationLodSolver.SuspendsThinking(SimulationTier.Off));
            Assert.IsFalse(SimulationLodSolver.SuspendsThinking(SimulationTier.Minimal));
            Assert.AreEqual(SimulationTier.Dormant, SimulationLodSolver.ClampParking(SimulationTier.Dormant, false), "Dormant is never clamped.");
        }
    }
}
