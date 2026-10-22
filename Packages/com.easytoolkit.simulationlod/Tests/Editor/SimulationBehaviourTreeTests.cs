using System.Collections.Generic;
using Easy.SimulationLod.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace Easy.SimulationLod.Tests
{
    /// <summary>Behaviour tree tick rate follows the tier, within the frame think budget.</summary>
    public sealed class SimulationBehaviourTreeTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created) Object.DestroyImmediate(go);
            created.Clear();
        }

        private static SimulationTreeTicker<SimulationTestBlackboard, string> Ticker(out SimulationCountingNode leaf, float interval = 0.2f)
        {
            leaf = new SimulationCountingNode();
            leaf.Initialize(new SimulationTestBlackboard());
            return new SimulationTreeTicker<SimulationTestBlackboard, string>(leaf, interval);
        }

        private static SimulationTierChange Change(SimulationTier current, float scale = 1f, bool parked = false,
            bool suspended = false, ISimulationThinkBudget budget = null)
            => new SimulationTierChange(null, SimulationTier.Full, current, parked, suspended, scale, budget);

        private static int TicksOver(SimulationTreeTicker<SimulationTestBlackboard, string> ticker, float from, float seconds, float step = 0.01f)
        {
            int before = ticker.TickCount;
            for (float t = from; t < from + seconds - 1e-4f; t += step) ticker.Tick(t);
            return ticker.TickCount - before;
        }

        [Test]
        public void FullTicksAtTheIntervalAndMinimalAtTheScaledInterval()
        {
            var ticker = Ticker(out SimulationCountingNode leaf);
            Assert.AreEqual(10, TicksOver(ticker, 0f, 2f), "Every 0.2 s over 2 s.");
            ticker.OnSimulationTierChanged(Change(SimulationTier.Minimal, 5f));
            int minimal = TicksOver(ticker, 2f, 4f);
            Assert.That(minimal, Is.InRange(4, 5), "Every 1 s over 4 s.");
            Assert.AreEqual(ticker.TickCount, leaf.Runs);
        }

        [Test]
        public void OffAndDormantNeverTickAndStopTheTreeOnce()
        {
            var ticker = Ticker(out SimulationCountingNode leaf);
            ticker.Tick(0f);
            ticker.OnSimulationTierChanged(Change(SimulationTier.Off));
            Assert.AreEqual(1, leaf.Stops, "Going to sleep interrupts the plan.");
            Assert.AreEqual(0, TicksOver(ticker, 1f, 2f));
            ticker.OnSimulationTierChanged(Change(SimulationTier.Dormant));
            Assert.AreEqual(1, leaf.Stops, "Already asleep: no second Stop.");
            Assert.AreEqual(0, TicksOver(ticker, 3f, 2f));

            ticker.OnSimulationTierChanged(Change(SimulationTier.Full));
            Assert.IsTrue(ticker.Tick(5f), "Waking ticks at once.");
        }

        [Test]
        public void AParkPausesWithoutStopAndASuspensionNeverCallsStop()
        {
            var ticker = Ticker(out SimulationCountingNode leaf);
            ticker.Tick(0f);
            ticker.OnSimulationTierChanged(Change(SimulationTier.Off, parked: true));
            Assert.AreEqual(0, leaf.Stops, "A park keeps the tree.");
            Assert.IsFalse(ticker.Tick(10f));
            ticker.OnSimulationTierChanged(Change(SimulationTier.Full));
            Assert.IsTrue(ticker.Tick(10f), "The unpark resumes at once.");

            ticker.OnSimulationTierChanged(Change(SimulationTier.Off, suspended: true));
            Assert.AreEqual(0, leaf.Stops, "A withdrawal runs no tree callback.");
            Assert.IsFalse(ticker.Tick(20f));
        }

        [Test]
        public void ARefusedTickKeepsItsDueTime()
        {
            var ticker = Ticker(out _);
            var budget = new FakeBudget { Allow = false };
            ticker.OnSimulationTierChanged(Change(SimulationTier.Full, budget: budget));
            Assert.IsFalse(ticker.Tick(0f));
            Assert.AreEqual(1, budget.Asked);
            budget.Allow = true;
            Assert.IsTrue(ticker.Tick(0.01f), "First in line next frame.");
            Assert.AreEqual(1, budget.Ended);
            ticker.OnSimulationTierChanged(Change(SimulationTier.Minimal, 5f, budget: budget));
            ticker.Tick(5f);
            Assert.IsTrue(budget.LastMinimal, "A Minimal tick asks for a Minimal slot.");
        }

        [Test]
        public void TheRunnerComponentFollowsItsAgentAndTheDriverBudget()
        {
            var driverObject = new GameObject("Driver");
            created.Add(driverObject);
            var driver = driverObject.AddComponent<SimulationLodDriver>();
            driver.AutoAdoptAgents = false;
            driver.IncludeObserverComponents = false;
            driver.IncludeRegisteredVolumes = false;
            driver.Settings = new SimulationLodSettings { FullRadius = 5f, MinimalRadius = 10f, MaxThinksPerFrame = 1 };
            var hero = new GameObject("Hero");
            created.Add(hero);
            driver.SetObservers(new[] { hero.transform });

            var zombie = new GameObject("Zombie");
            created.Add(zombie);
            var runner = zombie.AddComponent<SimulationTestTreeRunner>();
            runner.TickIntervalSeconds = 0f;
            var agent = zombie.AddComponent<SimulationAgent>();
            var other = new GameObject("Other");
            created.Add(other);
            var otherRunner = other.AddComponent<SimulationTestTreeRunner>();
            otherRunner.TickIntervalSeconds = 0f;
            var otherAgent = other.AddComponent<SimulationAgent>();
            driver.Register(agent);
            driver.Register(otherAgent);
            // Tell the runners the budget (a Full agent is told on its first tier change; force one now).
            driver.SetDormant(agent, true);
            driver.SetDormant(agent, false);
            driver.SetDormant(otherAgent, true);
            driver.SetDormant(otherAgent, false);

            Assert.IsTrue(runner.Leaf.Initialized, "The tree is built and initialised with its blackboard.");
            Assert.IsNotNull(runner.Blackboard);
            Assert.AreEqual(1, runner.Leaf.Stops, "Dormant stopped the tree once.");
            int ran = 0;
            ran += runner.TickNow() ? 1 : 0;
            ran += otherRunner.TickNow() ? 1 : 0;
            Assert.AreEqual(1, ran, "One think per frame across every managed tree.");

            driver.SetDormant(agent, true);
            int before = runner.Leaf.Runs;
            for (int i = 0; i < 5; i++) runner.Tick(100f + i);
            Assert.AreEqual(before, runner.Leaf.Runs, "A dormant agent's tree never ticks.");
        }

        private sealed class FakeBudget : ISimulationThinkBudget
        {
            public bool Allow = true;
            public int Asked, Ended;
            public bool LastMinimal;

            public bool TryBeginThink(bool minimal)
            {
                Asked++;
                LastMinimal = minimal;
                return Allow;
            }

            public void EndThink() => Ended++;
        }
    }
}
