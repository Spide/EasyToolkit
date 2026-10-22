using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Easy.SimulationLod.Tests
{
    /// <summary>Real Play Mode lifecycle: auto registration, Update-driven tiers, physics triggers, park callbacks.</summary>
    public sealed class SimulationLodPlayModeTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null) Object.Destroy(go);
            }

            created.Clear();
        }

        private GameObject NewObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            created.Add(go);
            return go;
        }

        private SimulationLodDriver NewDriver()
        {
            var driver = NewObject("Driver", Vector3.zero).AddComponent<SimulationLodDriver>();
            driver.Settings = new SimulationLodSettings
            {
                FullRadius = 5f, MinimalRadius = 10f, DemotionDelaySeconds = 0f, MaxEvaluationsPerFrame = 0,
            };
            driver.SolveIntervalSeconds = 0.05f;
            // A camera that sees nothing, so that a main camera cannot protect a test agent.
            Camera blind = NewObject("Blind camera", new Vector3(0f, 1000f, 0f)).AddComponent<Camera>();
            blind.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            driver.SetViewCamera(blind);
            return driver;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float seconds = 3f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator AgentsAreAdoptedObserversAreNotAndParkingCallbacksSeeTheFlags()
        {
            SimulationLodDriver driver = NewDriver();
            GameObject hero = NewObject("Hero", Vector3.zero);
            hero.AddComponent<SimulationObserver>();
            var heroAgent = hero.AddComponent<SimulationAgent>();
            GameObject zombie = NewObject("Zombie", new Vector3(70f, 0f, 0f));
            var agent = zombie.AddComponent<SimulationAgent>();
            var probe = zombie.AddComponent<SimulationTestProbe>();

            yield return WaitUntilOrTimeout(() => agent.IsParked);
            Assert.IsTrue(agent.IsParked, "Far from the observer component, it is parked.");
            Assert.AreSame(driver, agent.Owner, "Adopted from the registry.");
            Assert.IsNull(heroAgent.Owner, "An observer is never simulated.");
            Assert.IsTrue(probe.LastDisableWasPark, "A real OnDisable of the park sees IsParking.");

            hero.transform.position = new Vector3(70f, 0f, 0f);
            yield return WaitUntilOrTimeout(() => !agent.IsParked);
            Assert.IsTrue(zombie.activeSelf);
            Assert.IsTrue(probe.LastEnableWasUnpark, "A real OnEnable of the wake sees IsUnparking.");

            zombie.SetActive(false);
            Assert.IsFalse(probe.LastDisableWasPark, "An external deactivation never sees IsParking.");
            Assert.IsTrue(agent.IsSuspended, "An external disable withdraws the agent.");
            zombie.SetActive(true);
            Assert.IsFalse(probe.LastEnableWasUnpark, "An outside activation is never an unpark.");
            Assert.IsFalse(agent.IsSuspended, "Enabled again: readmitted.");
        }

        [UnityTest]
        public IEnumerator DisablingTheDriverReturnsParkedAgentsToFull()
        {
            SimulationLodDriver driver = NewDriver();
            NewObject("Hero", Vector3.zero).AddComponent<SimulationObserver>();
            var agent = NewObject("Far", new Vector3(70f, 0f, 0f)).AddComponent<SimulationAgent>();
            yield return WaitUntilOrTimeout(() => agent.IsParked);
            Assert.IsTrue(agent.IsParked);
            driver.enabled = false;
            Assert.IsFalse(agent.IsParked);
            Assert.IsTrue(agent.gameObject.activeSelf);
            Assert.AreEqual(SimulationTier.Full, agent.Tier);
        }

        [UnityTest]
        public IEnumerator AnObserverEnteringTheTriggerActivatesTheVolumeAndOthersDoNot()
        {
            NewDriver();
            GameObject room = NewObject("Room", new Vector3(0f, 0f, 30f));
            var trigger = room.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(10f, 4f, 10f);
            var volume = room.AddComponent<SimulationVolume>();
            volume.Shape = SimulationVolumeShape.Collider;
            volume.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            var sleeper = NewObject("Sleeper", new Vector3(0f, 0f, 30f)).AddComponent<SimulationAgent>();

            GameObject hero = NewBody("Hero", Vector3.zero);
            hero.AddComponent<SimulationObserver>();
            GameObject stray = NewBody("Stray", new Vector3(20f, 0f, 0f));

            yield return WaitUntilOrTimeout(() => sleeper.Tier == SimulationTier.Dormant);
            Assert.AreSame(volume, sleeper.Volume, "Assigned by containment.");
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier);

            stray.GetComponent<Rigidbody>().position = new Vector3(0f, 0f, 30f);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(volume.IsActive, "Only an observer activates it.");

            hero.GetComponent<Rigidbody>().position = new Vector3(0f, 0f, 28f);
            yield return WaitUntilOrTimeout(() => volume.IsActive);
            Assert.IsTrue(volume.IsActive, "The observer walked in.");
            yield return WaitUntilOrTimeout(() => sleeper.Tier == SimulationTier.Full);
            Assert.AreEqual(SimulationTier.Full, sleeper.Tier);
        }

        [UnityTest]
        public IEnumerator ATreeRunnerTicksInUpdateAndKeepsItsTreeAcrossAPark()
        {
            NewDriver();
            GameObject hero = NewObject("Hero", Vector3.zero);
            hero.AddComponent<SimulationObserver>();
            GameObject zombie = NewObject("Zombie", new Vector3(1f, 0f, 0f));
            var runner = zombie.AddComponent<SimulationTestTreeRunner>();
            runner.TickIntervalSeconds = 0f;
            var agent = zombie.AddComponent<SimulationAgent>();

            yield return WaitUntilOrTimeout(() => runner.Leaf != null && runner.Leaf.Runs > 3);
            Assert.Greater(runner.Leaf.Runs, 3, "Full: ticks from Update.");

            hero.transform.position = new Vector3(100f, 0f, 0f);
            yield return WaitUntilOrTimeout(() => agent.IsParked);
            Assert.IsTrue(agent.IsParked);
            int parkedRuns = runner.Leaf.Runs;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(parkedRuns, runner.Leaf.Runs, "Parked: no ticks.");
            Assert.AreEqual(0, runner.Leaf.Stops, "A park keeps the tree.");

            hero.transform.position = new Vector3(1f, 0f, 1f);
            yield return WaitUntilOrTimeout(() => runner.Leaf.Runs > parkedRuns + 2);
            Assert.Greater(runner.Leaf.Runs, parkedRuns + 2, "Resumed after the unpark.");
            Assert.AreEqual(0, runner.Leaf.Stops);
        }

        [UnityTest]
        public IEnumerator AnyColliderShapeContainsByClosestPoint()
        {
            GameObject go = NewObject("Capsule room", new Vector3(0f, 0f, -50f));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 1f;
            capsule.height = 6f;
            capsule.direction = 1;
            var volume = go.AddComponent<SimulationVolume>();
            volume.Shape = SimulationVolumeShape.Collider;
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(volume.Contains(new Vector3(0f, 2f, -50f)));
            Assert.IsFalse(volume.Contains(new Vector3(0.9f, 2.9f, -50.9f)), "Inside the bounds, outside the capsule.");
        }

        [UnityTest]
        public IEnumerator AnOutsideDisableReadmitsOnEnableButAnExplicitSuspensionStaysOut()
        {
            SimulationLodDriver driver = NewDriver();
            NewObject("Hero", Vector3.zero).AddComponent<SimulationObserver>();
            GameObject pooled = NewObject("Pooled", new Vector3(1f, 0f, 0f));
            var runner = pooled.AddComponent<SimulationTestTreeRunner>();
            runner.TickIntervalSeconds = 0f;
            var agent = pooled.AddComponent<SimulationAgent>();
            yield return WaitUntilOrTimeout(() => agent.Owner == driver && runner.Leaf != null && runner.Leaf.Runs > 2);
            Assert.AreSame(driver, agent.Owner);

            pooled.SetActive(false);
            Assert.IsTrue(agent.IsSuspended);
            Assert.IsFalse(agent.IsSuspendedExplicitly);
            pooled.SetActive(true);
            Assert.IsFalse(agent.IsSuspended, "Back from the pool: readmitted.");
            int runs = runner.Leaf.Runs;
            yield return WaitUntilOrTimeout(() => runner.Leaf.Runs > runs + 2);
            Assert.Greater(runner.Leaf.Runs, runs + 2, "Its tree ticks again.");

            driver.SuspendManaged(agent);
            pooled.SetActive(false);
            pooled.SetActive(true);
            Assert.IsTrue(agent.IsSuspendedExplicitly, "An explicit withdrawal stays out.");
            Assert.IsTrue(driver.ReadmitManaged(agent));
            Assert.IsFalse(agent.IsSuspended);

            agent.ReadmitOnEnable = false;
            pooled.SetActive(false);
            pooled.SetActive(true);
            Assert.IsTrue(agent.IsSuspended, "ReadmitOnEnable off: the original semantics.");
        }

        [UnityTest]
        public IEnumerator TheTreeRunnerStopsOnceOnAnOutsideDisableWhateverTheComponentOrder()
        {
            NewDriver();
            NewObject("Hero", Vector3.zero).AddComponent<SimulationObserver>();
            GameObject runnerFirst = NewObject("Runner first", new Vector3(1f, 0f, 0f));
            var a = runnerFirst.AddComponent<SimulationTestTreeRunner>();
            runnerFirst.AddComponent<SimulationAgent>();
            GameObject agentFirst = NewObject("Agent first", new Vector3(-1f, 0f, 0f));
            agentFirst.AddComponent<SimulationAgent>();
            var b = agentFirst.AddComponent<SimulationTestTreeRunner>();
            yield return WaitUntilOrTimeout(() => a.Leaf != null && b.Leaf != null && a.Leaf.Runs > 1 && b.Leaf.Runs > 1);
            runnerFirst.SetActive(false);
            agentFirst.SetActive(false);
            Assert.AreEqual(1, a.Leaf.Stops);
            Assert.AreEqual(1, b.Leaf.Stops);
        }

        [UnityTest]
        public IEnumerator DisablingTheDriverMidWakeAllCompletesItAndRestoresParkedIneligibleAgents()
        {
            SimulationLodDriver driver = NewDriver();
            var settings = driver.Settings;
            settings.MaxActivationChangesPerFrame = 1;
            driver.Settings = settings;
            NewObject("Hero", Vector3.zero).AddComponent<SimulationObserver>();
            var far = new List<SimulationAgent>();
            for (int i = 0; i < 3; i++) far.Add(NewObject("Far " + i, new Vector3(70f, 0f, i * 2f)).AddComponent<SimulationAgent>());
            GameObject deadObject = NewObject("Dead", new Vector3(-70f, 0f, 0f));
            var state = deadObject.AddComponent<SimulationTestState>();
            var dead = deadObject.AddComponent<SimulationAgent>();
            yield return WaitUntilOrTimeout(() => far.TrueForAll(a => a.IsParked) && dead.IsParked);
            Assert.IsTrue(dead.IsParked);
            state.Alive = false;

            int completed = 0;
            driver.WakeAllCompleted += () => completed++;
            driver.BeginWakeAll();
            Assert.IsTrue(driver.IsWakingAll);
            driver.enabled = false;
            Assert.AreEqual(1, completed, "An interrupted wake-all still completes.");
            Assert.IsFalse(driver.IsWakingAll);
            foreach (SimulationAgent agent in far) Assert.IsFalse(agent.IsParked);
            Assert.IsTrue(deadObject.activeSelf, "Parked, then ineligible: still reactivated by its driver.");
        }

        [UnityTest]
        public IEnumerator AnExplicitWithdrawalFollowedByDeactivationRunsNoTreeCallback()
        {
            SimulationLodDriver driver = NewDriver();
            NewObject("Hero", Vector3.zero).AddComponent<SimulationObserver>();
            GameObject zombie = NewObject("Withdrawn", new Vector3(1f, 0f, 0f));
            var runner = zombie.AddComponent<SimulationTestTreeRunner>();
            var agent = zombie.AddComponent<SimulationAgent>();
            yield return WaitUntilOrTimeout(() => agent.Owner == driver && runner.Leaf != null && runner.Leaf.Runs > 1);
            driver.SuspendManaged(agent);
            zombie.SetActive(false);
            Assert.AreEqual(0, runner.Leaf.Stops, "SuspendManaged promises no gameplay callbacks, also on the later disable.");
        }

        private GameObject NewBody(string name, Vector3 position)
        {
            GameObject go = NewObject(name, position);
            go.AddComponent<SphereCollider>().radius = 0.5f;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return go;
        }
    }
}
