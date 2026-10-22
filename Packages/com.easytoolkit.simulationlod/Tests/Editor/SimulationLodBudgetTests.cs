using NUnit.Framework;

namespace Easy.SimulationLod.Tests
{
    /// <summary>The per-frame budgets of the simulation LOD (pure rules).</summary>
    public sealed class SimulationLodBudgetTests
    {
        private const long TicksPerMs = 1000;

        private static SimulationLodSettings Budget(int thinks, int minimalThinks = 0, float milliseconds = 0f, int activations = 0)
            => new SimulationLodSettings
            {
                FullRadius = 22f, MinimalRadius = 40f, DemotionDelaySeconds = 3f,
                MaxThinksPerFrame = thinks, MaxMinimalThinksPerFrame = minimalThinks,
                ThinkBudgetMilliseconds = milliseconds, MaxActivationChangesPerFrame = activations,
            };

        [Test]
        public void NeverMoreThinksInAFrameThanTheBudgetAndANewFrameStartsAgain()
        {
            var budget = new SimulationFrameBudget(TicksPerMs);
            SimulationLodSettings settings = Budget(3);
            int granted = 0;
            for (int thinker = 0; thinker < 10; thinker++)
            {
                if (!budget.TryBeginThink(1, false, 0, in settings)) continue;
                granted++;
                budget.EndThink(0);
            }

            Assert.AreEqual(3, granted);
            Assert.AreEqual(7, budget.DeferredThinks, "Every refused think is counted; each retries next frame.");
            Assert.IsTrue(budget.TryBeginThink(2, false, 0, in settings), "The next frame has a fresh budget.");
            Assert.AreEqual(3, budget.PeakThinksPerFrame);
        }

        [Test]
        public void MinimalThinkersOnlyTakeTheirShare()
        {
            var budget = new SimulationFrameBudget(TicksPerMs);
            SimulationLodSettings settings = Budget(4, 1);
            Assert.IsTrue(budget.TryBeginThink(1, true, 0, in settings));
            Assert.IsFalse(budget.TryBeginThink(1, true, 0, in settings), "One Minimal think per frame.");
            Assert.IsTrue(budget.TryBeginThink(1, false, 0, in settings), "Full thinkers still think.");
            Assert.IsTrue(budget.TryBeginThink(1, false, 0, in settings));
            Assert.IsTrue(budget.TryBeginThink(1, false, 0, in settings));
            Assert.IsFalse(budget.TryBeginThink(1, false, 0, in settings), "The total includes the Minimal think.");
        }

        [Test]
        public void TheTimeBudgetStopsLaterThinksButNeverTheFirst()
        {
            var budget = new SimulationFrameBudget(TicksPerMs);
            SimulationLodSettings settings = Budget(0, 0, 1f);
            Assert.IsTrue(budget.TryBeginThink(1, false, 0, in settings));
            budget.EndThink(5 * TicksPerMs);
            Assert.IsFalse(budget.TryBeginThink(1, false, 0, in settings), "5 ms spent of a 1 ms budget.");
            Assert.IsTrue(budget.TryBeginThink(2, false, 0, in settings), "The first think of a frame always runs.");
            budget.EndThink(TicksPerMs / 2);
            Assert.IsTrue(budget.TryBeginThink(2, false, 0, in settings), "Half a millisecond spent: still within the budget.");
        }

        [Test]
        public void ZeroMeansUnlimited()
        {
            var budget = new SimulationFrameBudget(TicksPerMs);
            SimulationLodSettings settings = Budget(0);
            for (int i = 0; i < 1000; i++) Assert.IsTrue(budget.TryBeginThink(1, i % 2 == 0, 0, in settings));
            for (int i = 0; i < 1000; i++) Assert.IsTrue(budget.TryChangeActivation(false, in settings));
        }

        [Test]
        public void OnlyNonUrgentActivationChangesWait()
        {
            var budget = new SimulationFrameBudget(TicksPerMs);
            SimulationLodSettings settings = Budget(0, 0, 0f, 2);
            budget.BeginFrame(1);
            Assert.IsTrue(budget.TryChangeActivation(false, in settings));
            Assert.IsTrue(budget.HasActivationBudget(in settings));
            Assert.IsTrue(budget.TryChangeActivation(false, in settings));
            Assert.IsFalse(budget.HasActivationBudget(in settings));
            Assert.IsFalse(budget.TryChangeActivation(false, in settings), "The third park of a frame waits.");
            Assert.IsTrue(budget.TryChangeActivation(true, in settings), "An agent coming into view is never held back.");
            Assert.AreEqual(1, budget.DeferredActivationChanges);
            budget.BeginFrame(2);
            Assert.IsTrue(budget.TryChangeActivation(false, in settings), "A new frame, a new budget.");
            budget.ResetActivationChanges();
            Assert.AreEqual(0, budget.ActivationChangesThisFrame);
        }

        [Test]
        public void TheSliceSpreadsEveryAgentOverOneIntervalWithinTheCap()
        {
            Assert.AreEqual(0, SimulationFrameBudget.SliceSize(0, 0.016f, 0.25f, 24));
            Assert.AreEqual(7, SimulationFrameBudget.SliceSize(100, 0.016f, 0.25f, 24), "100 agents over 15.6 frames.");
            Assert.AreEqual(24, SimulationFrameBudget.SliceSize(1000, 0.016f, 0.25f, 24), "The cap holds; the cycle gets longer.");
            Assert.AreEqual(1, SimulationFrameBudget.SliceSize(3, 0.001f, 0.25f, 24), "At least one per frame.");
            Assert.AreEqual(5, SimulationFrameBudget.SliceSize(5, 0.5f, 0.25f, 0), "A long frame takes everyone.");
            Assert.AreEqual(5, SimulationFrameBudget.SliceSize(5, 0f, 0.25f, 0), "No frame time: a full pass.");
        }

        [Test]
        public void ParkingCanBeForbiddenAndOnlyOffChangesActivation()
        {
            Assert.AreEqual(SimulationTier.Minimal, SimulationLodSolver.ClampParking(SimulationTier.Off, false));
            Assert.AreEqual(SimulationTier.Off, SimulationLodSolver.ClampParking(SimulationTier.Off, true));
            Assert.AreEqual(SimulationTier.Full, SimulationLodSolver.ClampParking(SimulationTier.Full, false));
            Assert.IsTrue(SimulationLodSolver.TogglesActivation(SimulationTier.Full, SimulationTier.Off, true));
            Assert.IsTrue(SimulationLodSolver.TogglesActivation(SimulationTier.Off, SimulationTier.Minimal, true));
            Assert.IsFalse(SimulationLodSolver.TogglesActivation(SimulationTier.Full, SimulationTier.Minimal, true));
            Assert.IsFalse(SimulationLodSolver.TogglesActivation(SimulationTier.Full, SimulationTier.Off, false));
        }

        [Test]
        public void AnyObserverViewCountsAsOnScreen()
        {
            SimulationLodSettings settings = SimulationLodSettings.Default;
            Assert.IsTrue(SimulationLodSolver.InObserverView(19f * 19f, in settings));
            Assert.IsFalse(SimulationLodSolver.InObserverView(21f * 21f, in settings));
            Assert.LessOrEqual(settings.ObserverViewRadius, settings.FullRadius, "Every observer's screen is inside its Full radius.");
            settings.ObserverViewRadius = 0f;
            Assert.IsFalse(SimulationLodSolver.InObserverView(0f, in settings), "0 switches the rule off.");
        }
    }
}
