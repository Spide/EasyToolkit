using System.Collections.Generic;
using Easy.SimulationLod.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace Easy.SimulationLod.Tests
{
    /// <summary>The driver on real agents: who gets which tier, budgets, dormancy, managed parking, allocations.</summary>
    public sealed class SimulationLodDriverTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private SimulationLodDriver driver;
        private Transform hero;

        [SetUp]
        public void SetUp()
        {
            driver = NewObject("Driver", Vector3.zero).AddComponent<SimulationLodDriver>();
            driver.Settings = SimulationLodSettings.Default;
            driver.IncludeObserverComponents = false;
            driver.IncludeRegisteredVolumes = false;
            driver.AutoAdoptAgents = false;
            hero = NewObject("Hero", Vector3.zero).transform;
            driver.SetObservers(new[] { hero });
            // A camera that sees nothing, so that a main camera of an open scene cannot protect a test agent.
            Camera blind = NewObject("Blind camera", new Vector3(0f, 1000f, 0f)).AddComponent<Camera>();
            blind.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            driver.SetViewCamera(blind);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created) Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject NewObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            created.Add(go);
            return go;
        }

        private SimulationAgent Zombie(string name, float x, float z = 0f)
        {
            GameObject go = NewObject(name, new Vector3(x, 0f, z));
            var agent = go.AddComponent<SimulationAgent>();
            driver.Register(agent);
            return agent;
        }

        [Test]
        public void WithoutSegmentsDistanceDecidesAfterTheDelay()
        {
            SimulationAgent near = Zombie("Near", 10f), middle = Zombie("Middle", 30f), far = Zombie("Far", 70f);
            driver.Solve(0f);
            Assert.AreEqual(SimulationTier.Full, far.Tier, "A demotion waits for its delay.");
            driver.Solve(driver.Settings.DemotionDelaySeconds + 0.1f);
            Assert.AreEqual(SimulationTier.Full, near.Tier);
            Assert.AreEqual(SimulationTier.Minimal, middle.Tier);
            Assert.AreEqual(SimulationTier.Off, far.Tier);
            Assert.IsFalse(far.gameObject.activeSelf, "Off parks the whole object.");
            Assert.IsTrue(middle.gameObject.activeSelf);
            Assert.AreEqual(1, driver.FullCount);
            Assert.AreEqual(1, driver.MinimalCount);
            Assert.AreEqual(1, driver.OffCount);

            hero.position = new Vector3(70f, 0f, 0f);
            driver.Solve(10f);
            Assert.AreEqual(SimulationTier.Full, far.Tier, "A promotion is immediate.");
            Assert.IsTrue(far.gameObject.activeSelf, "A parked agent comes back when it is promoted.");
            Assert.AreEqual(SimulationTier.Full, near.Tier, "The demotion of the others is only pending.");
        }

        [Test]
        public void NothingInTheViewOfTheGameCameraIsDemoted()
        {
            SimulationAgent watched = Zombie("Watched", 70f), unseen = Zombie("Unseen", 0f, -70f);
            Camera camera = NewObject("Camera", new Vector3(70f, 20f, 0f)).AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            driver.SetViewCamera(camera);
            driver.Solve(0f);
            driver.Solve(10f);
            driver.Solve(20f);
            Assert.AreEqual(SimulationTier.Full, watched.Tier, "Far from the hero, but on screen.");
            Assert.AreEqual(SimulationTier.Off, unseen.Tier);

            camera.transform.position = new Vector3(-200f, 20f, 0f);
            driver.Solve(21f);
            driver.Solve(30f);
            Assert.AreEqual(SimulationTier.Off, watched.Tier, "Out of view, it is demoted after the delay.");
        }

        [Test]
        public void ANoiseInReachWakesAParkedAgentForAWhile()
        {
            SimulationAgent far = Zombie("Far", 70f), farther = Zombie("Farther", 150f);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.IsFalse(far.gameObject.activeSelf);
            driver.NotifyNoise(new Vector3(60f, 0f, 0f), 20f);
            driver.Solve(10.1f);
            Assert.AreEqual(SimulationTier.Full, far.Tier, "The noise reached it.");
            Assert.IsTrue(far.gameObject.activeSelf);
            Assert.AreEqual(SimulationTier.Off, farther.Tier, "Out of reach of the noise.");
            driver.Solve(17f);
            driver.Solve(21f);
            Assert.AreEqual(SimulationTier.Off, far.Tier, "Once the noise is forgotten it is parked again.");
        }

        [Test]
        public void DisablingParkingKeepsOffAgentsActive()
        {
            driver.DeactivateWhenOff = false;
            SimulationAgent far = Zombie("Far", 70f);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.AreEqual(SimulationTier.Off, far.Tier);
            Assert.IsTrue(far.gameObject.activeSelf);
        }

        [Test]
        public void WithSegmentsTheNeighbourIsMinimalAndTheRestIsOff()
        {
            // Three districts in a row, each 100 m wide, so that distance alone would switch everything off.
            var map = NewObject("Map", Vector3.zero).AddComponent<SimulationSegmentMap>();
            map.Configure(3, 1, 100f, new[] { "West", "Middle", "East" }, new byte[] { 0, 1, 2 }, new[] { 0, 1, 1, 2 });
            driver.SetSegmentMap(map);
            hero.position = new Vector3(-140f, 0f, 0f);
            SimulationAgent sameDistrict = Zombie("Same district", -60f);
            SimulationAgent nextDoor = Zombie("Next door", 40f);
            SimulationAgent twoAway = Zombie("Two away", 140f);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.AreEqual(SimulationTier.Full, sameDistrict.Tier, "The hero's district runs fully whatever the distance.");
            Assert.AreEqual(SimulationTier.Minimal, nextDoor.Tier);
            Assert.AreEqual(SimulationTier.Off, twoAway.Tier);
        }

        internal static SimulationLodSettings Tight(int activations = 0, int evaluations = 0, float viewRadius = 0f) => new SimulationLodSettings
        {
            FullRadius = 5f, MinimalRadius = 10f, DemotionDelaySeconds = 0f, ObserverViewRadius = viewRadius,
            MaxActivationChangesPerFrame = activations, MaxEvaluationsPerFrame = evaluations,
        };

        private static int CountActive(SimulationAgent[] agents)
        {
            int active = 0;
            foreach (SimulationAgent agent in agents) if (agent.gameObject.activeSelf) active++;
            return active;
        }

        private SimulationAgent[] Row(int count, float x)
        {
            var row = new SimulationAgent[count];
            for (int i = 0; i < count; i++) row[i] = Zombie("Row " + i, x, i * 2f);
            return row;
        }

        [Test]
        public void ParkingIsSpreadOverFramesButAnAgentComingIntoViewIsNotHeldBack()
        {
            driver.Settings = Tight(activations: 2, viewRadius: 4f);
            SimulationAgent[] far = Row(5, 70f);
            driver.Solve(0f);
            driver.Solve(1f);
            Assert.AreEqual(3, CountActive(far), "Two parks per pass.");
            driver.Solve(2f);
            Assert.AreEqual(1, CountActive(far));
            driver.Solve(3f);
            Assert.AreEqual(0, CountActive(far));
            Assert.AreEqual(4, driver.Budget.DeferredActivationChanges, "Three parks waited once, one twice.");

            // The hero arrives among them: each is on its screen, so all come back in the same pass.
            hero.position = new Vector3(70f, 0f, 4f);
            driver.Solve(4f);
            Assert.AreEqual(5, CountActive(far), "Urgent unparks ignore the budget.");
        }

        [Test]
        public void ADistantPromotionWaitsForTheBudgetToo()
        {
            driver.Settings = Tight(activations: 1);
            SimulationAgent[] far = Row(3, 70f);
            driver.Solve(0f);
            for (int pass = 1; pass <= 3; pass++) driver.Solve(pass);
            Assert.AreEqual(0, CountActive(far));
            // Within the Minimal radius but off every screen (no view radius): not urgent.
            hero.position = new Vector3(70f, 0f, -6f);
            driver.Solve(10f);
            Assert.AreEqual(1, CountActive(far), "One unpark per pass.");
            driver.Solve(11f);
            driver.Solve(12f);
            Assert.AreEqual(3, CountActive(far));
        }

        [Test]
        public void RoundRobinStepsEvaluateASliceEachFrame()
        {
            driver.Settings = Tight(evaluations: 2);
            SimulationAgent[] far = Row(6, 70f);
            // A frame as long as the interval asks for everyone; the cap keeps it to two per frame.
            driver.Step(0f, 0.25f, 1);
            driver.Step(0.01f, 0.25f, 2);
            driver.Step(0.02f, 0.25f, 3);
            Assert.AreEqual(6, CountActive(far), "Each has been seen once: the demotion is only pending.");
            Assert.AreEqual(6, driver.FullCount, "The counts are published once per cycle.");
            driver.Step(0.03f, 0.25f, 4);
            Assert.IsFalse(far[0].gameObject.activeSelf);
            Assert.IsFalse(far[1].gameObject.activeSelf);
            Assert.AreEqual(4, CountActive(far), "Only this frame's slice moved.");
            driver.Step(0.04f, 0.25f, 5);
            driver.Step(0.05f, 0.25f, 6);
            Assert.AreEqual(0, CountActive(far));
            Assert.AreEqual(6, driver.OffCount);
        }

        [Test]
        public void ThePolicyKeepsAnOrderFullAndCanForbidParking()
        {
            driver.Settings = Tight();
            SimulationAgent ordered = Zombie("Ordered", 70f), unreplicated = Zombie("Unreplicated", 0f, 70f), plain = Zombie("Plain", -70f);
            var policy = new FakePolicy { Sticky = ordered, NoPark = unreplicated };
            driver.SetPolicy(policy);
            driver.Solve(0f);
            driver.Solve(1f);
            Assert.AreEqual(SimulationTier.Full, ordered.Tier, "An order keeps it Full far from every observer.");
            Assert.AreEqual(SimulationTier.Minimal, unreplicated.Tier, "Off is clamped to Minimal when parking is forbidden.");
            Assert.IsTrue(unreplicated.gameObject.activeSelf);
            Assert.AreEqual(SimulationTier.Off, plain.Tier);
            policy.NoPark = null;
            driver.Solve(2f);
            driver.Solve(3f);
            Assert.AreEqual(SimulationTier.Off, unreplicated.Tier, "Once allowed, it parks like any other.");
        }

        [Test]
        public void AnObserverViewProtectsFromDemotionBeyondTheFullRadius()
        {
            driver.Settings = Tight(viewRadius: 30f);
            SimulationAgent seen = Zombie("Seen", 25f), unseen = Zombie("Unseen", 60f);
            driver.Solve(0f);
            driver.Solve(1f);
            driver.Solve(2f);
            Assert.AreEqual(SimulationTier.Full, seen.Tier, "On some observer's screen, never demoted.");
            Assert.AreEqual(SimulationTier.Off, unseen.Tier);
        }

        [Test]
        public void TheAlarmWakesParkedAgentsAFewPerFrameAndThenCompletes()
        {
            driver.Settings = Tight(activations: 2);
            SimulationAgent[] far = Row(5, 70f);
            driver.Solve(0f);
            for (int pass = 1; pass <= 3; pass++) driver.Solve(pass);
            Assert.AreEqual(0, CountActive(far));

            int woken = 0, completed = 0;
            driver.Woken += _ => woken++;
            driver.WakeAllCompleted += () => completed++;
            driver.BeginWakeAll();
            Assert.IsTrue(driver.IsWakingAll);
            Assert.AreEqual(2, CountActive(far), "The first batch wakes at once.");
            driver.Step(10f, 0.016f, 100);
            Assert.AreEqual(4, CountActive(far));
            Assert.AreEqual(0, completed);
            driver.Step(10.02f, 0.016f, 101);
            Assert.AreEqual(5, CountActive(far));
            Assert.AreEqual(5, woken);
            Assert.AreEqual(1, completed);
            Assert.IsFalse(driver.IsWakingAll);
            foreach (SimulationAgent agent in far) Assert.AreEqual(SimulationTier.Full, agent.Tier);
        }

        [Test]
        public void NothingHappensWithoutObservers()
        {
            SimulationAgent far = Zombie("Far", 70f);
            driver.SetObservers(new Transform[0]);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.AreEqual(SimulationTier.Full, far.Tier);
            driver.SetObservers(new[] { hero });
            driver.Solve(20f);
            driver.Solve(30f);
            Assert.AreEqual(SimulationTier.Off, far.Tier);
            driver.SetObservers(new Transform[0]);
            driver.Solve(40f);
            Assert.AreEqual(SimulationTier.Off, far.Tier, "Without observers the tiers stay as they are.");
        }

        [Test]
        public void AnObserverSourceIsAskedAtEverySolve()
        {
            driver.SetObservers(new Transform[0]);
            var source = new FakeObserverSource { Observer = hero };
            driver.SetObserverSource(source);
            SimulationAgent far = Zombie("Far", 70f);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.AreEqual(SimulationTier.Off, far.Tier);
            Assert.AreEqual(1, driver.Observers.Count);
        }

        [Test]
        public void UnregisterReturnsAnAgentToFullAndReleasesIt()
        {
            SimulationAgent far = Zombie("Far", 70f), sleeper = Zombie("Sleeper", 5f);
            driver.SetDormant(sleeper, true);
            driver.Solve(0f);
            driver.Solve(10f);
            Assert.IsFalse(far.gameObject.activeSelf);
            driver.Unregister(far);
            driver.Unregister(sleeper);
            Assert.IsTrue(far.gameObject.activeSelf);
            Assert.AreEqual(SimulationTier.Full, far.Tier);
            Assert.AreEqual(SimulationTier.Full, sleeper.Tier);
            Assert.IsNull(far.Owner);
            Assert.AreEqual(0, driver.ManagedCount);
        }

        // ---- Dormant ----

        [Test]
        public void ADormantAgentSleepsBesideTheObserverAndOnScreenUntilWoken()
        {
            driver.Settings = Tight(viewRadius: 30f);
            SimulationAgent sleeper = Zombie("Sleeper", 2f);
            var probe = sleeper.gameObject.AddComponent<SimulationTestProbe>();
            sleeper.RefreshComponents();
            driver.SetDormant(sleeper, true);
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier, "Applied at once.");
            Assert.IsTrue(sleeper.gameObject.activeSelf, "Frozen, not parked.");
            Camera camera = NewObject("Camera", new Vector3(2f, 20f, 0f)).AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            driver.SetViewCamera(camera);
            for (int pass = 0; pass < 4; pass++) driver.Solve(pass);
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier, "Neither distance nor camera override Dormant.");
            Assert.AreEqual(1, driver.DormantCount);

            Assert.IsTrue(driver.TryWakeManaged(sleeper, 5f));
            Assert.AreEqual(SimulationTier.Full, sleeper.Tier);
            Assert.IsFalse(sleeper.DormantRequested);
            hero.position = new Vector3(500f, 0f, 0f);
            camera.transform.position = new Vector3(-500f, 20f, 0f);
            driver.Solve(20f);
            driver.Solve(21f);
            Assert.AreEqual(SimulationTier.Off, sleeper.Tier, "Once activated it follows the normal rules.");
            CollectionAssert.Contains(probe.Tiers, SimulationTier.Dormant);
        }

        [Test]
        public void StickinessAndPolicyDoNotOverrideDormant()
        {
            driver.Settings = Tight();
            SimulationAgent sleeper = Zombie("Sleeper", 70f);
            var state = sleeper.gameObject.AddComponent<SimulationTestState>();
            sleeper.RefreshComponents();
            state.Alerted = true;
            driver.SetPolicy(new FakePolicy { Sticky = sleeper });
            driver.SetDormant(sleeper, true);
            driver.Solve(0f);
            driver.Solve(1f);
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier);
        }

        [Test]
        public void ANoiseWakesAnAgentFromItsOwnDormancyWhenItAllows()
        {
            driver.Settings = Tight();
            SimulationAgent light = Zombie("Light sleeper", 70f), deep = Zombie("Deep sleeper", 72f);
            deep.WakeFromDormantOnNoise = false;
            driver.SetDormant(light, true);
            driver.SetDormant(deep, true);
            driver.NotifyNoise(new Vector3(71f, 0f, 0f), 10f);
            driver.Solve(0.1f);
            Assert.AreEqual(SimulationTier.Full, light.Tier, "Woken and kept Full by the noise.");
            Assert.AreEqual(SimulationTier.Dormant, deep.Tier);
        }

        [Test]
        public void DamageFromAHealthSourceWakesAndKeepsFull()
        {
            driver.Settings = Tight();
            SimulationAgent sleeper = Zombie("Sleeper", 70f);
            var state = sleeper.gameObject.AddComponent<SimulationTestState>();
            state.Health = 10f;
            sleeper.RefreshComponents();
            driver.SetDormant(sleeper, true);
            driver.Solve(0f);
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier);
            state.Health = 8f;
            driver.Solve(1f);
            Assert.AreEqual(SimulationTier.Full, sleeper.Tier, "Damage ends its own dormancy and wakes it.");
            driver.Solve(2f);
            Assert.AreEqual(SimulationTier.Full, sleeper.Tier, "Kept Full for the wake time.");
            driver.Solve(driver.WakeSeconds + 2f);
            driver.Solve(driver.WakeSeconds + 3f);
            Assert.AreEqual(SimulationTier.Off, sleeper.Tier);
        }

        [Test]
        public void StartDormantAgentsSleepFromAdoption()
        {
            GameObject go = NewObject("Born asleep", new Vector3(1f, 0f, 0f));
            var agent = go.AddComponent<SimulationAgent>();
            agent.DormantRequested = true;
            driver.Register(agent);
            Assert.AreEqual(SimulationTier.Dormant, agent.Tier, "Not a single cycle awake.");
        }

        [Test]
        public void ADormantAgentThatMayParkParksWithinTheBudget()
        {
            driver.Settings = Tight(activations: 1);
            SimulationAgent a = Zombie("A", 70f), b = Zombie("B", 72f);
            a.ParkWhenDormant = b.ParkWhenDormant = true;
            a.DormantRequested = b.DormantRequested = true;
            driver.Solve(0f);
            Assert.AreEqual(1, CountActive(new[] { a, b }), "One park per pass.");
            driver.Solve(1f);
            Assert.AreEqual(0, CountActive(new[] { a, b }));
            Assert.IsTrue(a.IsParked && b.IsParked);
        }

        // ---- Managed parking API ----

        [Test]
        public void ManagedParkingWakeSuspendAndReadmit()
        {
            driver.Settings = Tight();
            SimulationAgent far = Zombie("Far", 70f);
            var probe = far.gameObject.AddComponent<SimulationTestProbe>();
            far.RefreshComponents();
            driver.Solve(0f);
            driver.Solve(1f);
            Assert.IsTrue(driver.IsManagedParked(far));
            Assert.IsTrue(driver.TryWakeManaged(far, 2f));
            Assert.IsTrue(far.gameObject.activeSelf);
            Assert.AreEqual(SimulationTier.Full, far.Tier);
            Assert.IsFalse(driver.IsManagedParked(far));
            Assert.IsTrue(probe.LastEnableWasUnpark, "An explicit wake is a legitimate unpark.");
            driver.Solve(3f);
            Assert.AreEqual(SimulationTier.Full, far.Tier, "The wake time keeps it Full.");

            driver.SuspendManaged(far);
            Assert.IsTrue(far.IsSuspended);
            Assert.IsTrue(probe.LastSuspended, "Listeners hear the withdrawal.");
            Assert.IsNull(probe.LastBudget, "A suspended agent has no budget.");
            driver.Solve(20f);
            driver.Solve(21f);
            Assert.IsTrue(far.gameObject.activeSelf, "Ordinary solves never touch a suspended agent.");
            Assert.IsFalse(driver.TryWakeManaged(far, 22f));

            Assert.IsTrue(driver.ReadmitManaged(far));
            Assert.IsFalse(far.IsSuspended);
            driver.Solve(23f);
            driver.Solve(24f);
            Assert.AreEqual(SimulationTier.Off, far.Tier, "Readmitted, it is managed again.");
        }

        [Test]
        public void IneligibleAgentsAreLeftAlone()
        {
            driver.Settings = Tight();
            SimulationAgent far = Zombie("Dead", 70f);
            var state = far.gameObject.AddComponent<SimulationTestState>();
            far.RefreshComponents();
            state.Alive = false;
            driver.Solve(0f);
            driver.Solve(1f);
            Assert.AreEqual(SimulationTier.Full, far.Tier);
            Assert.IsTrue(far.gameObject.activeSelf);
            Assert.IsFalse(driver.IsManagedParked(far));
            Assert.IsFalse(driver.TryWakeManaged(far, 2f));
            Assert.IsFalse(driver.ReadmitManaged(far));
        }

        [Test]
        public void DestroyedAgentsAreForgotten()
        {
            driver.Settings = Tight(evaluations: 2);
            SimulationAgent[] row = Row(4, 70f);
            driver.Solve(0f);
            Object.DestroyImmediate(row[1].gameObject);
            Assert.DoesNotThrow(() => driver.Solve(1f));
            Assert.AreEqual(3, driver.ManagedCount);
        }

        [Test]
        public void ManagedAgentsGetTheDriverAsTheirThinkBudget()
        {
            driver.Settings = new SimulationLodSettings { FullRadius = 5f, MinimalRadius = 10f, MaxThinksPerFrame = 1 };
            SimulationAgent a = Zombie("A", 1f);
            Assert.AreSame(driver, a.ThinkBudget);
        }

        // ---- Allocation ----

        [Test]
        public void SteadySteppingOfMixedTiersVolumesAndTreesAllocatesNothing()
        {
            driver.Settings = new SimulationLodSettings
            {
                FullRadius = 5f, MinimalRadius = 10f, DemotionDelaySeconds = 0f, MaxEvaluationsPerFrame = 4,
                MaxThinksPerFrame = 4, MaxActivationChangesPerFrame = 2,
            };
            SimulationAgent near = Zombie("Near", 3f);
            Zombie("Middle", 8f);
            Row(6, 70f);
            Row(6, 150f);

            // A cellar of sleepers that never activates, and one woken volume that has finished waking.
            var cellar = NewObject("Cellar", new Vector3(-40f, 0f, 0f)).AddComponent<SimulationVolume>();
            cellar.Size = new Vector3(10f, 10f, 10f);
            cellar.Center = Vector3.zero;
            var hall = NewObject("Hall", new Vector3(-80f, 0f, 0f)).AddComponent<SimulationVolume>();
            hall.Size = new Vector3(10f, 10f, 10f);
            hall.Center = Vector3.zero;
            hall.Deactivation = SimulationVolumeDeactivation.WhenObserversLeave;
            hall.DeactivateAfterSeconds = 1e6f;
            driver.AddVolume(cellar);
            driver.AddVolume(hall);
            for (int i = 0; i < 4; i++) Zombie("Sleeper " + i, -40f + i, 0f);
            for (int i = 0; i < 3; i++) Zombie("Hall " + i, -80f + i, 0f);

            // Two neighbouring collider volumes and an agent that keeps moving between them (membership tracking).
            SimulationVolume ColliderVolume(string name, float x)
            {
                GameObject go = NewObject(name, new Vector3(x, 0f, 0f));
                go.AddComponent<BoxCollider>().size = new Vector3(10f, 10f, 10f);
                var volume = go.AddComponent<SimulationVolume>();
                volume.Shape = SimulationVolumeShape.Collider;
                driver.AddVolume(volume);
                volume.Activate();
                return volume;
            }

            SimulationVolume left = ColliderVolume("Left", -120f);
            SimulationVolume right = ColliderVolume("Right", -109f);
            SimulationAgent mover = Zombie("Mover", -120f);
            int joins = 0;

            // Trees on the near agent and on a sleeper (which must not tick).
            var tickers = new List<SimulationTreeTicker<SimulationTestBlackboard, string>>();
            foreach (SimulationAgent agent in new[] { near, cellar.Occupants[0] })
            {
                var leaf = new SimulationCountingNode();
                leaf.Initialize(new SimulationTestBlackboard());
                var ticker = new SimulationTreeTicker<SimulationTestBlackboard, string>(leaf, 0.1f);
                agent.AddListener(ticker);
                tickers.Add(ticker);
            }

            tickers[1].OnSimulationTierChanged(new SimulationTierChange(cellar.Occupants[0], SimulationTier.Full,
                SimulationTier.Dormant, false, false, 1f, driver));

            float now = 0f;
            int frame = 0;
            void Frame()
            {
                now += 0.05f;
                SimulationVolume before = mover.Volume;
                mover.transform.position = new Vector3((frame & 8) == 0 ? -120f : -109f, 0f, 0f);
                driver.Step(now, 0.05f, ++frame);
                if (mover.Volume != before) joins++;
                for (int t = 0; t < tickers.Count; t++) tickers[t].Tick(now);
            }

            for (int i = 0; i < 100; i++) Frame();
            hall.Activate();
            for (int i = 0; i < 300; i++) Frame(); // settle every tier, finish the hall's wake
            Assert.Greater(driver.OffCount, 0, "Some agents are parked.");
            Assert.AreEqual(4, driver.DormantCount, "The cellar sleeps.");
            Assert.IsFalse(hall.IsWaking);
            Assert.AreEqual(SimulationTier.Full, near.Tier, "The near agent runs in full.");
            Assert.Greater(tickers[0].TickCount, 0);
            Assert.AreEqual(0, tickers[1].TickCount, "A dormant tree never ticks.");
            Assert.Greater(joins, 2, "The mover changes volume as it moves.");
            Assert.IsTrue(mover.Volume == left || mover.Volume == right);

            long samples = AllocationProbe.Measure(240, Frame);
            Assert.AreEqual(0, samples, "A steady frame of the scheduler allocated.");

            // Positive control: the recorder does see an allocation made the same way.
            long control = AllocationProbe.Measure(4, () => { _ = new object[4]; });
            Assert.Greater(control, 0, "The recorder must see a real allocation.");
        }

        private sealed class FakePolicy : ISimulationLodPolicy
        {
            public SimulationAgent Sticky, NoPark;
            public bool IsSticky(SimulationAgent agent) => agent == Sticky;
            public bool MayPark(SimulationAgent agent) => agent != NoPark;
        }

        private sealed class FakeObserverSource : ISimulationObserverSource
        {
            public Transform Observer;
            public void GetObservers(List<Transform> observers) => observers.Add(Observer);
        }
    }
}
