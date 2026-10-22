using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Easy.SimulationLod.Tests
{
    /// <summary>Volumes: occupants, nesting, activation, staggered wakes within the budget, deactivation.</summary>
    public sealed class SimulationVolumeTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private SimulationLodDriver driver;
        private Transform hero;
        private float now;
        private int frame;

        [SetUp]
        public void SetUp()
        {
            driver = NewObject("Driver", Vector3.zero).AddComponent<SimulationLodDriver>();
            driver.IncludeObserverComponents = false;
            driver.IncludeRegisteredVolumes = false;
            driver.AutoAdoptAgents = false;
            driver.Settings = new SimulationLodSettings
            {
                FullRadius = 8f, MinimalRadius = 15f, DemotionDelaySeconds = 0f, MaxEvaluationsPerFrame = 0,
                MaxActivationChangesPerFrame = 0,
            };
            hero = NewObject("Hero", new Vector3(0f, 0f, -3f)).transform;
            driver.SetObservers(new[] { hero });
            Camera blind = NewObject("Blind camera", new Vector3(0f, 1000f, 0f)).AddComponent<Camera>();
            blind.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            driver.SetViewCamera(blind);
            now = 0f;
            frame = 0;
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

        /// <summary>A box volume centred on <paramref name="position"/>, 10 m wide by default.</summary>
        private SimulationVolume Volume(string name, Vector3 position, float size = 10f)
        {
            var volume = NewObject(name, position).AddComponent<SimulationVolume>();
            volume.Center = Vector3.zero;
            volume.Size = new Vector3(size, size, size);
            volume.KeepFullSeconds = 0f;
            driver.AddVolume(volume);
            return volume;
        }

        private SimulationAgent Agent(string name, Vector3 position)
        {
            var agent = NewObject(name, position).AddComponent<SimulationAgent>();
            driver.Register(agent);
            return agent;
        }

        private void Frame(float delta = 0.05f)
        {
            now += delta;
            driver.Step(now, delta, ++frame);
        }

        private static int CountTier(IEnumerable<SimulationAgent> agents, SimulationTier tier)
        {
            int count = 0;
            foreach (SimulationAgent agent in agents) if (agent.Tier == tier) count++;
            return count;
        }

        [Test]
        public void AgentsInsideAreAssignedByContainmentAndSleepBesideTheObserver()
        {
            SimulationVolume house = Volume("House", new Vector3(0f, 0f, 3f));
            SimulationAgent inside = Agent("Inside", new Vector3(1f, 0f, 3f));
            SimulationAgent outside = Agent("Outside", new Vector3(1f, 0f, -20f));
            Assert.AreSame(house, inside.Volume);
            Assert.IsNull(outside.Volume);
            Assert.AreEqual(1, house.OccupantCount);
            Assert.AreEqual(SimulationTier.Dormant, inside.Tier, "Asleep from adoption, though the hero stands next to it.");
            for (int i = 0; i < 20; i++) Frame();
            Assert.AreEqual(SimulationTier.Dormant, inside.Tier);
            Assert.AreEqual(SimulationTier.Off, outside.Tier, "Outside every volume the normal rules apply.");
            Assert.AreEqual(1, driver.DormantCount);
        }

        [Test]
        public void ExplicitOccupantsWinOverContainmentAndAVolumeAddedLaterClaimsAgents()
        {
            SimulationAgent agent = Agent("Agent", new Vector3(50f, 0f, 0f));
            Assert.IsNull(agent.Volume);
            SimulationVolume far = Volume("Elsewhere", new Vector3(-50f, 0f, 0f));
            far.ExplicitOccupants.Add(agent);
            Volume("Around it", new Vector3(50f, 0f, 0f));
            Frame();
            Assert.AreSame(far, agent.Volume, "An explicit listing wins over containment.");
            Assert.AreEqual(SimulationTier.Dormant, agent.Tier, "Claimed and put to sleep at once.");
        }

        [Test]
        public void TheInnermostVolumeWinsAndPriorityBeatsSize()
        {
            SimulationVolume house = Volume("House", new Vector3(0f, 0f, 20f), 20f);
            SimulationVolume cellar = Volume("Cellar", new Vector3(0f, -5f, 20f), 6f);
            SimulationAgent down = Agent("In the cellar", new Vector3(0f, -5f, 20f));
            SimulationAgent up = Agent("Upstairs", new Vector3(5f, 5f, 20f));
            Assert.AreSame(cellar, down.Volume, "Nested: the innermost wins.");
            Assert.AreSame(house, up.Volume);

            SimulationVolume bunker = Volume("Big but important", new Vector3(30f, 0f, 0f), 40f);
            bunker.Priority = 5;
            Volume("Small", new Vector3(30f, 0f, 0f), 4f);
            SimulationAgent guard = Agent("Guard", new Vector3(30f, 0f, 0f));
            Assert.AreSame(bunker, guard.Volume, "A higher priority wins over a smaller volume.");
        }

        [Test]
        public void ExplicitActivateWakesEveryOccupantAndRaisesEvents()
        {
            SimulationVolume cellar = Volume("Cellar", new Vector3(0f, 0f, 3f));
            cellar.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            var sleepers = new List<SimulationAgent>();
            for (int i = 0; i < 4; i++) sleepers.Add(Agent("Sleeper " + i, new Vector3(i - 2f, 0f, 3f)));
            int activated = 0, woken = 0, completed = 0;
            cellar.Activated += _ => activated++;
            cellar.OccupantWoken += (_, __) => woken++;
            cellar.WakeCompleted += _ => completed++;

            Assert.IsTrue(cellar.Activate(), "The door opens.");
            Assert.IsFalse(cellar.Activate(), "Only once.");
            Assert.AreEqual(1, activated);
            Assert.IsTrue(cellar.IsWaking);
            Assert.AreEqual(4, CountTier(sleepers, SimulationTier.Dormant), "Nothing happens before the driver's frame.");
            Frame();
            Assert.AreEqual(4, CountTier(sleepers, SimulationTier.Full), "Near the hero, they all run in full.");
            Assert.AreEqual(4, woken);
            Assert.AreEqual(1, completed);
            Assert.IsFalse(cellar.IsWaking);
        }

        [Test]
        public void StaggeredWakesRespectTheVolumeRateAndTheDriverBudgetUnlessInstant()
        {
            driver.Settings = new SimulationLodSettings
            {
                FullRadius = 8f, MinimalRadius = 15f, DemotionDelaySeconds = 0f, MaxActivationChangesPerFrame = 2,
            };
            SimulationVolume barracks = Volume("Barracks", new Vector3(0f, 0f, 3f));
            barracks.WakeMode = SimulationVolumeWakeMode.Staggered;
            barracks.WakePerFrame = 3;
            var sleepers = new List<SimulationAgent>();
            for (int i = 0; i < 6; i++) sleepers.Add(Agent("Sleeper " + i, new Vector3(i * 0.5f - 1.5f, 0f, 3f)));
            barracks.Activate();
            Frame();
            Assert.AreEqual(2, sleepers.Count - CountTier(sleepers, SimulationTier.Dormant), "The driver budget (2) is below the volume rate (3).");
            Frame();
            Assert.AreEqual(4, sleepers.Count - CountTier(sleepers, SimulationTier.Dormant));
            Frame();
            Assert.AreEqual(0, CountTier(sleepers, SimulationTier.Dormant));
            Assert.Greater(driver.Budget.DeferredActivationChanges, 0);

            SimulationVolume panic = Volume("Panic room", new Vector3(0f, 0f, 30f));
            panic.WakeMode = SimulationVolumeWakeMode.Staggered;
            panic.WakePerFrame = 5;
            panic.Instant = true;
            var crowd = new List<SimulationAgent>();
            for (int i = 0; i < 5; i++) crowd.Add(Agent("Crowd " + i, new Vector3(i - 2f, 0f, 30f)));
            panic.Activate();
            Frame();
            Assert.AreEqual(0, CountTier(crowd, SimulationTier.Dormant), "An instant volume ignores the driver budget; its own rate holds.");
        }

        [Test]
        public void NearestFirstWakesTheClosestToTheOriginFirst()
        {
            SimulationVolume hall = Volume("Hall", new Vector3(0f, 0f, 30f), 30f);
            hall.WakeMode = SimulationVolumeWakeMode.NearestFirst;
            hall.WakePerFrame = 1;
            SimulationAgent far = Agent("Far", new Vector3(10f, 0f, 30f));
            SimulationAgent near = Agent("Near", new Vector3(-10f, 0f, 30f));
            SimulationAgent middle = Agent("Middle", new Vector3(0f, 0f, 30f));
            var order = new List<SimulationAgent>();
            hall.OccupantWoken += (_, agent) => order.Add(agent);
            hall.Activate(new Vector3(-14f, 0f, 30f));
            Frame();
            Frame();
            Frame();
            CollectionAssert.AreEqual(new[] { near, middle, far }, order);
        }

        [Test]
        public void ACappedVolumeLimitsItsOccupantsUntilActivated()
        {
            SimulationVolume shed = Volume("Shed", new Vector3(0f, 0f, 3f));
            shed.InactiveTier = SimulationTier.Minimal;
            SimulationAgent worker = Agent("Worker", new Vector3(0f, 0f, 3f));
            Frame();
            Assert.AreEqual(SimulationTier.Minimal, worker.Tier, "Beside the hero, but capped to Minimal.");
            shed.Activate();
            Frame();
            Assert.AreEqual(SimulationTier.Full, worker.Tier);
        }

        [Test]
        public void DeactivationReturnsInsideOccupantsToSleepAndReleasesThoseThatLeft()
        {
            SimulationVolume den = Volume("Den", new Vector3(0f, 0f, 30f));
            den.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            den.Deactivation = SimulationVolumeDeactivation.WhenObserversLeave;
            den.DeactivateAfterSeconds = 1f;
            SimulationAgent stayer = Agent("Stayer", new Vector3(0f, 0f, 30f));
            SimulationAgent leaver = Agent("Leaver", new Vector3(1f, 0f, 30f));
            hero.position = new Vector3(0f, 0f, 28f);
            den.Activate();
            Frame();
            Assert.AreEqual(SimulationTier.Full, stayer.Tier);
            for (int i = 0; i < 40; i++) Frame();
            Assert.IsTrue(den.IsActive, "An observer inside keeps it active.");

            leaver.transform.position = new Vector3(0f, 0f, 60f);
            hero.position = new Vector3(0f, 0f, 20f);
            int deactivated = 0;
            den.Deactivated += _ => deactivated++;
            for (int i = 0; i < 30; i++) Frame();
            Assert.AreEqual(1, deactivated, "No observer inside for a second.");
            Assert.IsFalse(den.IsActive);
            for (int i = 0; i < 10; i++) Frame();
            Assert.AreEqual(SimulationTier.Dormant, stayer.Tier, "Back to sleep.");
            Assert.IsNull(leaver.Volume, "An occupant that left is released.");
            Assert.AreNotEqual(SimulationTier.Dormant, leaver.Tier);

            Assert.IsTrue(den.Activate(), "It can be activated again.");
            Frame();
            Assert.AreNotEqual(SimulationTier.Dormant, stayer.Tier);
        }

        [Test]
        public void DeadOrDestroyedOccupantsAreSkipped()
        {
            SimulationVolume crypt = Volume("Crypt", new Vector3(0f, 0f, 3f));
            crypt.WakeMode = SimulationVolumeWakeMode.Staggered;
            crypt.WakePerFrame = 1;
            SimulationAgent gone = Agent("Gone", new Vector3(-1f, 0f, 3f));
            SimulationAgent dead = Agent("Dead", new Vector3(0f, 0f, 3f));
            SimulationAgent alive = Agent("Alive", new Vector3(1f, 0f, 3f));
            var state = dead.gameObject.AddComponent<SimulationTestState>();
            dead.RefreshComponents();
            state.Alive = false;
            Object.DestroyImmediate(gone.gameObject);
            int woken = 0;
            crypt.OccupantWoken += (_, __) => woken++;
            Assert.DoesNotThrow(() => crypt.Activate());
            Frame();
            Assert.AreEqual(SimulationTier.Full, alive.Tier, "The dead one does not take the living one's turn.");
            Assert.AreEqual(1, woken);
            Assert.IsFalse(crypt.IsWaking);
            Assert.AreEqual(2, crypt.OccupantCount, "The destroyed occupant is forgotten.");
        }

        [Test]
        public void NoiseReachingTheVolumeAndDamageToAnOccupantActivateIt()
        {
            SimulationVolume loud = Volume("Loud", new Vector3(0f, 0f, 40f));
            SimulationAgent sleeper = Agent("Sleeper", new Vector3(0f, 0f, 40f));
            driver.NotifyNoise(new Vector3(0f, 0f, 60f), 5f);
            Assert.IsFalse(loud.IsActive, "15 m away, a 5 m noise does not reach it.");
            driver.NotifyNoise(new Vector3(0f, 0f, 60f), 20f);
            Assert.IsTrue(loud.IsActive);
            Assert.AreEqual(new Vector3(0f, 0f, 60f), loud.ActivationOrigin);

            SimulationVolume deaf = Volume("Deaf", new Vector3(0f, 0f, 80f));
            deaf.ActivateOnNoise = false;
            SimulationAgent victim = Agent("Victim", new Vector3(0f, 0f, 80f));
            driver.NotifyNoise(new Vector3(0f, 0f, 80f), 50f);
            Assert.IsFalse(deaf.IsActive);
            victim.NotifyDamaged();
            Assert.IsTrue(deaf.IsActive, "Damage to an occupant wakes the whole volume.");
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier, "Waking is the driver's job, on its next frame.");
            Frame();
            Assert.AreNotEqual(SimulationTier.Dormant, sleeper.Tier);
        }

        [Test]
        public void AnExplicitWakeReleasesOneOccupantFromAnInactiveVolume()
        {
            SimulationVolume vault = Volume("Vault", new Vector3(0f, 0f, 3f));
            SimulationAgent a = Agent("A", new Vector3(0f, 0f, 3f));
            SimulationAgent b = Agent("B", new Vector3(1f, 0f, 3f));
            Assert.IsTrue(driver.TryWakeManaged(a, 1f));
            Frame();
            Assert.AreEqual(SimulationTier.Full, a.Tier);
            Assert.IsNull(a.Volume);
            Assert.AreEqual(SimulationTier.Dormant, b.Tier);
            Assert.IsFalse(vault.IsActive);
        }

        [Test]
        public void WakeAllCanIncludeDormantAgents()
        {
            Volume("Vault", new Vector3(0f, 0f, 3f));
            SimulationAgent a = Agent("A", new Vector3(0f, 0f, 3f));
            SimulationAgent b = Agent("B", new Vector3(50f, 0f, 3f));
            b.DormantRequested = true;
            Frame();
            driver.BeginWakeAll();
            Assert.AreEqual(SimulationTier.Dormant, a.Tier, "By default an alarm wakes parked agents only.");
            Frame();
            driver.BeginWakeAll(includeDormant: true);
            Assert.AreEqual(SimulationTier.Full, a.Tier);
            Assert.AreEqual(SimulationTier.Full, b.Tier);
            Assert.IsFalse(b.DormantRequested);
        }

        [Test]
        public void ContainmentFollowsRotationScaleAndColliders()
        {
            SimulationVolume box = NewObject("Rotated", new Vector3(100f, 0f, 0f)).AddComponent<SimulationVolume>();
            box.Center = Vector3.zero;
            box.Size = new Vector3(2f, 2f, 20f);
            box.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.IsTrue(box.Contains(new Vector3(108f, 0f, 0f)), "The long axis now runs along X.");
            Assert.IsFalse(box.Contains(new Vector3(100f, 0f, 8f)));
            Assert.AreEqual(0f, box.SqrDistance(new Vector3(105f, 0f, 0f)));
            Assert.AreEqual(4f, box.SqrDistance(new Vector3(100f, 0f, 3f)), 1e-3f);

            var go = NewObject("Collider volume", new Vector3(-100f, 0f, 0f));
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 3f;
            var volume = go.AddComponent<SimulationVolume>();
            volume.Shape = SimulationVolumeShape.Collider;
            Assert.IsTrue(volume.Contains(new Vector3(-98f, 0f, 0f)));
            Assert.IsFalse(volume.Contains(new Vector3(-96f, 0f, 0f)));
        }

        private void Frames(int count)
        {
            for (int i = 0; i < count; i++) Frame();
        }

        private Camera LookAt(Vector3 point)
        {
            Camera camera = NewObject("Camera", point + new Vector3(0f, 20f, 0f)).AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            driver.SetViewCamera(camera);
            return camera;
        }

        [Test]
        public void AnAgentWalkingIntoAnInactiveVolumeJoinsAndSleepsOnceCalmAndUnseen()
        {
            SimulationVolume house = Volume("House", new Vector3(0f, 0f, 40f));
            SimulationAgent walker = Agent("Walker", new Vector3(0f, 0f, 20f));
            var state = walker.gameObject.AddComponent<SimulationTestState>();
            walker.RefreshComponents();
            Frames(10);
            Assert.IsNull(walker.Volume);

            state.Alerted = true;
            walker.transform.position = new Vector3(0f, 0f, 40f);
            Frames(10);
            Assert.AreSame(house, walker.Volume, "Joined on the evaluation slice.");
            Assert.AreNotEqual(SimulationTier.Dormant, walker.Tier, "A sticky (fighting) agent is not put to sleep.");

            state.Alerted = false;
            Frames(10);
            Assert.AreEqual(SimulationTier.Dormant, walker.Tier, "Calm and unseen: it joins the hold.");
        }

        [Test]
        public void AnOccupantLeavingATrackingVolumeIsReleasedAndAnUntrackedVolumeKeepsItsOccupants()
        {
            SimulationVolume tracked = Volume("Tracked", new Vector3(0f, 0f, 3f));
            tracked.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            SimulationAgent runner = Agent("Runner", new Vector3(0f, 0f, 3f));
            tracked.Activate();
            Frames(2);
            runner.transform.position = new Vector3(0f, 0f, -30f);
            Frames(10);
            Assert.IsNull(runner.Volume, "Leaving releases it.");
            Assert.AreEqual(0, tracked.OccupantCount);

            SimulationVolume fixedRoom = Volume("Fixed", new Vector3(50f, 0f, 3f));
            fixedRoom.TrackMembership = false;
            SimulationAgent stray = Agent("Stray", new Vector3(50f, 0f, 30f));
            stray.transform.position = new Vector3(50f, 0f, 3f);
            Frames(10);
            Assert.IsNull(stray.Volume, "Without tracking, membership is decided at adoption only.");
        }

        [Test]
        public void TrackingHonoursExplicitListPriorityAndInnermost()
        {
            SimulationVolume house = Volume("House", new Vector3(0f, 0f, 40f), 20f);
            SimulationVolume cellar = Volume("Cellar", new Vector3(0f, -5f, 40f), 6f);
            house.Activate();
            cellar.Activate();
            SimulationAgent agent = Agent("Agent", new Vector3(0f, 0f, 0f));
            agent.transform.position = new Vector3(5f, 5f, 40f);
            Frames(10);
            Assert.AreSame(house, agent.Volume);
            agent.transform.position = new Vector3(0f, -5f, 40f);
            Frames(10);
            Assert.AreSame(cellar, agent.Volume, "Moving into the nested volume: the innermost wins.");

            SimulationVolume listing = Volume("Listing", new Vector3(-80f, 0f, 0f));
            listing.ExplicitOccupants.Add(agent);
            listing.Activate();
            Frames(10);
            Assert.AreSame(listing, agent.Volume, "An explicit listing wins wherever the agent stands.");
        }

        [Test]
        public void AnExplicitlyWokenAgentIsNotCapturedAgainByContainment()
        {
            Volume("Vault", new Vector3(0f, 0f, 3f));
            SimulationAgent a = Agent("A", new Vector3(0f, 0f, 3f));
            Assert.IsTrue(driver.TryWakeManaged(a, now));
            Frames(20);
            Assert.IsNull(a.Volume);
            Assert.AreNotEqual(SimulationTier.Dormant, a.Tier);
        }

        [Test]
        public void ReturningToSleepWaitsForAStickyOccupantToCalmDown()
        {
            SimulationVolume den = Volume("Den", new Vector3(0f, 0f, 30f));
            den.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            SimulationAgent fighter = Agent("Fighter", new Vector3(0f, 0f, 30f));
            var state = fighter.gameObject.AddComponent<SimulationTestState>();
            fighter.RefreshComponents();
            den.Activate();
            Frames(2);
            state.Alerted = true;
            den.Deactivate();
            Frames(10);
            Assert.AreNotEqual(SimulationTier.Dormant, fighter.Tier, "A fighting occupant stays awake.");
            Assert.AreSame(den, fighter.Volume);
            state.Alerted = false;
            Frames(10);
            Assert.AreEqual(SimulationTier.Dormant, fighter.Tier, "Calm again: back to sleep.");
        }

        [Test]
        public void ReturningToSleepWaitsUntilTheOccupantIsOutOfView()
        {
            SimulationVolume den = Volume("Den", new Vector3(0f, 0f, 30f));
            den.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            SimulationAgent seen = Agent("Seen", new Vector3(0f, 0f, 30f));
            den.Activate();
            Frames(2);
            Camera camera = LookAt(new Vector3(0f, 0f, 30f));
            den.Deactivate();
            Frames(10);
            Assert.AreNotEqual(SimulationTier.Dormant, seen.Tier, "On screen: it does not fall asleep in view.");
            camera.transform.position = new Vector3(0f, 20f, -500f);
            Frames(10);
            Assert.AreEqual(SimulationTier.Dormant, seen.Tier);
        }

        [Test]
        public void AnOffCapNeverParksAnAgentOnScreenButDormantIgnoresTheCamera()
        {
            Camera camera = LookAt(new Vector3(0f, 0f, 60f));
            SimulationVolume yard = Volume("Yard", new Vector3(0f, 0f, 60f));
            yard.InactiveTier = SimulationTier.Off;
            SimulationAgent visible = Agent("Visible", new Vector3(0f, 0f, 60f));
            Frames(10);
            Assert.AreEqual(SimulationTier.Full, visible.Tier, "In view: the cap does not demote it.");
            Assert.IsTrue(visible.gameObject.activeSelf);
            camera.transform.position = new Vector3(0f, 20f, -500f);
            Frames(10);
            Assert.AreEqual(SimulationTier.Off, visible.Tier, "Out of view the cap applies at once.");
            Assert.IsFalse(visible.gameObject.activeSelf);

            camera.transform.position = new Vector3(100f, 20f, 60f);
            SimulationVolume crypt = Volume("Crypt", new Vector3(100f, 0f, 60f));
            SimulationAgent sleeper = Agent("Sleeper", new Vector3(100f, 0f, 60f));
            Frames(5);
            Assert.AreEqual(SimulationTier.Dormant, sleeper.Tier, "Dormant is camera-agnostic: keep such volumes out of view.");
            Assert.IsTrue(sleeper.gameObject.activeSelf, "Not parked: ParkWhenDormant is off by default.");
            Assert.AreSame(crypt, sleeper.Volume);
        }

        private SimulationAgent ProtectedOccupantOfDeactivatedDen(out SimulationVolume den, out SimulationTestState state)
        {
            den = Volume("Den", new Vector3(0f, 0f, 30f));
            den.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            SimulationAgent fighter = Agent("Fighter", new Vector3(0f, 0f, 30f));
            fighter.ParkWhenDormant = true;
            state = fighter.gameObject.AddComponent<SimulationTestState>();
            fighter.RefreshComponents();
            den.Activate();
            Frames(2);
            state.Alerted = true;
            den.Deactivate();
            Frames(10);
            Assert.AreNotEqual(SimulationTier.Dormant, fighter.Tier);
            return fighter;
        }

        [Test]
        public void AVolumeAppearingDoesNotPutAProtectedOccupantToSleep()
        {
            SimulationAgent fighter = ProtectedOccupantOfDeactivatedDen(out _, out _);
            Volume("Unrelated", new Vector3(200f, 0f, 0f));
            Frames(10);
            Assert.AreNotEqual(SimulationTier.Dormant, fighter.Tier, "A volume list change must not bypass the protection.");
            Assert.IsTrue(fighter.gameObject.activeSelf, "Nor park it.");
        }

        [Test]
        public void EndingAnOwnDormancyRequestKeepsAProtectedOccupantAwake()
        {
            SimulationAgent fighter = ProtectedOccupantOfDeactivatedDen(out _, out SimulationTestState state);
            driver.SetDormant(fighter, false);
            Assert.AreNotEqual(SimulationTier.Dormant, fighter.Tier);
            state.Alerted = false;
            Frames(10);
            Assert.AreEqual(SimulationTier.Dormant, fighter.Tier, "Calm: the volume's hold applies.");
        }

        [Test]
        public void AVolumeListChangeDoesNotCaptureForANonTrackingVolume()
        {
            SimulationVolume room = Volume("Untracked", new Vector3(0f, 0f, 40f));
            room.TrackMembership = false;
            SimulationAgent roamer = Agent("Roamer", new Vector3(0f, 0f, 20f));
            roamer.transform.position = new Vector3(0f, 0f, 40f);
            Frames(5);
            Volume("Elsewhere", new Vector3(200f, 0f, 0f));
            Frames(5);
            Assert.IsNull(roamer.Volume, "Membership of a non-tracking volume is decided at adoption only.");
            Assert.AreNotEqual(SimulationTier.Dormant, roamer.Tier);
        }

        [Test]
        public void WakeAllWithoutDormantSkipsHeldParkedAgents()
        {
            SimulationVolume crypt = Volume("Crypt", new Vector3(0f, 0f, 60f));
            var held = new List<SimulationAgent>();
            for (int i = 0; i < 3; i++)
            {
                GameObject go = NewObject("Held " + i, new Vector3(i, 0f, 60f));
                var agent = go.AddComponent<SimulationAgent>();
                agent.ParkWhenDormant = true;
                driver.Register(agent);
                held.Add(agent);
            }

            SimulationAgent far = Agent("Far", new Vector3(0f, 0f, -100f));
            Frames(10);
            Assert.IsTrue(far.IsParked);
            foreach (SimulationAgent agent in held) Assert.IsTrue(agent.IsParked);
            int woken = 0;
            driver.Woken += _ => woken++;
            driver.BeginWakeAll();
            Assert.AreEqual(1, woken, "Only the distance-parked agent wakes.");
            foreach (SimulationAgent agent in held) Assert.IsTrue(agent.IsParked, "No unpark/re-park flicker.");
            Assert.AreSame(crypt, held[0].Volume);
        }

        [Test]
        public void AVolumeDestroyedOrDisabledMidWakeReleasesItsHold()
        {
            SimulationVolume a = Volume("A", new Vector3(0f, 0f, 3f));
            a.WakeMode = SimulationVolumeWakeMode.Staggered;
            a.WakePerFrame = 1;
            var first = new List<SimulationAgent>();
            for (int i = 0; i < 3; i++) first.Add(Agent("A" + i, new Vector3(i - 1f, 0f, 3f)));
            a.Activate();
            Frame();
            Assert.AreEqual(2, CountTier(first, SimulationTier.Dormant));
            Object.DestroyImmediate(a.gameObject);
            Assert.DoesNotThrow(() => Frames(10));
            Assert.AreEqual(0, CountTier(first, SimulationTier.Dormant), "Without its volume nothing holds them.");

            SimulationVolume b = Volume("B", new Vector3(0f, 0f, 3f));
            b.WakeMode = SimulationVolumeWakeMode.Staggered;
            b.WakePerFrame = 1;
            var second = new List<SimulationAgent>();
            for (int i = 0; i < 3; i++) second.Add(Agent("B" + i, new Vector3(i - 1f, 0f, 3f)));
            Assert.AreEqual(3, CountTier(second, SimulationTier.Dormant));
            b.Activate();
            Frame();
            b.enabled = false;
            Assert.DoesNotThrow(() => Frames(10));
            Assert.AreEqual(0, CountTier(second, SimulationTier.Dormant), "A disabled volume holds nothing.");
        }

        [Test]
        public void ACallbackMayDeactivateTheVolumeOrDisableTheDriverMidWake()
        {
            SimulationVolume a = Volume("A", new Vector3(0f, 0f, 3f));
            a.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            var agents = new List<SimulationAgent>();
            for (int i = 0; i < 3; i++) agents.Add(Agent("A" + i, new Vector3(i - 1f, 0f, 3f)));
            int woken = 0;
            a.OccupantWoken += (volume, _) =>
            {
                woken++;
                volume.Deactivate();
            };
            a.Activate();
            Assert.DoesNotThrow(() => Frame());
            Assert.AreEqual(1, woken);
            Assert.IsFalse(a.IsActive);
            Assert.IsFalse(a.IsWaking);

            SimulationVolume b = Volume("B", new Vector3(0f, 0f, 60f));
            b.WakeMode = SimulationVolumeWakeMode.AllAtOnce;
            for (int i = 0; i < 3; i++) Agent("B" + i, new Vector3(i - 1f, 0f, 60f));
            int bWoken = 0;
            b.OccupantWoken += (_, __) =>
            {
                bWoken++;
                driver.enabled = false;
            };
            b.Activate();
            Assert.DoesNotThrow(() => Frame());
            Assert.AreEqual(1, bWoken, "A disabled driver stops draining.");
            Assert.IsTrue(b.IsWaking, "The rest waits for a running driver.");
        }

        [Test]
        public void ADestroyedObserverIsIgnored()
        {
            var extra = NewObject("Extra hero", new Vector3(0f, 0f, 10f)).transform;
            driver.SetObservers(new[] { hero, extra });
            Agent("Agent", new Vector3(0f, 0f, 5f));
            Frame();
            Object.DestroyImmediate(extra.gameObject);
            Assert.DoesNotThrow(() => driver.NotifyNoise(Vector3.zero, 50f));
            Assert.DoesNotThrow(() => Frames(5));
        }

        [Test]
        public void UnregisterReactivatesAnAgentParkedBeforeItBecameIneligible()
        {
            SimulationAgent far = Agent("Far", new Vector3(0f, 0f, -100f));
            var state = far.gameObject.AddComponent<SimulationTestState>();
            far.RefreshComponents();
            Frames(10);
            Assert.IsTrue(far.IsParked);
            state.Alive = false;
            driver.Unregister(far);
            Assert.IsTrue(far.gameObject.activeSelf, "Never left inactive by the driver that parked it.");
            Assert.IsFalse(far.IsParked);
        }

        [Test]
        public void WakeAllWakesAnAgentParkedByDistanceInsideAMinimalCapVolume()
        {
            SimulationVolume shed = Volume("Shed", new Vector3(0f, 0f, 60f));
            shed.InactiveTier = SimulationTier.Minimal;
            SimulationAgent worker = Agent("Worker", new Vector3(0f, 0f, 60f));
            Frames(10);
            Assert.IsTrue(worker.IsParked, "Far away: Off by distance, the Minimal cap does not win.");
            int woken = 0;
            driver.Woken += _ => woken++;
            driver.BeginWakeAll();
            Assert.AreEqual(1, woken);
            Assert.IsFalse(worker.IsParked);
            Assert.AreEqual(SimulationTier.Full, worker.Tier);
        }

        [Test]
        public void ReadmissionIsANewAdmissionInsideAnInactiveDormantVolume()
        {
            SimulationVolume vault = Volume("Vault", new Vector3(0f, 0f, 60f));
            SimulationAgent offScreen = Agent("Off screen", new Vector3(0f, 0f, 60f));
            SimulationAgent woken = Agent("Previously woken", new Vector3(1f, 0f, 60f));
            Assert.IsTrue(driver.TryWakeManaged(woken, now));
            Assert.IsNull(woken.Volume);

            foreach (SimulationAgent agent in new[] { offScreen, woken })
            {
                driver.SuspendManaged(agent);
                Assert.IsTrue(driver.ReadmitManaged(agent));
                Assert.AreSame(vault, agent.Volume, agent.name + ": assigned again where it stands.");
                Assert.AreEqual(SimulationTier.Dormant, agent.Tier, agent.name + ": the hold applies at once, no awake cycle.");
            }

            LookAt(new Vector3(-1f, 0f, 60f));
            SimulationAgent onScreen = Agent("On screen", new Vector3(-1f, 0f, 60f));
            Assert.IsTrue(driver.TryWakeManaged(onScreen, now));
            Frames(5);
            Assert.AreEqual(SimulationTier.Full, onScreen.Tier);
            driver.SuspendManaged(onScreen);
            Assert.IsTrue(driver.ReadmitManaged(onScreen));
            Assert.AreEqual(SimulationTier.Dormant, onScreen.Tier, "Like an adoption, the first hold is not protected (Dormant ignores the camera).");
        }
    }
}
