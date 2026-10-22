using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// Decides the simulation tier of every <see cref="SimulationAgent"/> from where the observers are. With a
    /// <see cref="SimulationSegmentMap"/> the map's districts count; with none, distance alone does. Volumes and
    /// dormancy requests force tiers on top. Without observers the distance rules pause (tiers stay as they
    /// are) while dormancy and volumes still apply; disabling the driver returns every managed agent to Full.
    /// Run it on the simulating authority only: in a host-authoritative game the host owns the tiers.
    /// <para>
    /// With <see cref="SimulationLodSettings.MaxEvaluationsPerFrame"/> set, agents are re-evaluated round-robin, a
    /// slice per frame, so that each is seen about once per solve interval; parks, unparks and volume wakes that
    /// are not urgent are limited per frame; and it is the think budget of every agent it manages. Without it (0)
    /// one full pass runs per interval. A steady frame allocates nothing.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation LOD Driver")]
    public class SimulationLodDriver : MonoBehaviour, ISimulationThinkBudget
    {
        private static readonly List<SimulationLodDriver> drivers = new List<SimulationLodDriver>();

        [SerializeField] private SimulationSegmentMap segmentMap;
        [SerializeField] private SimulationLodSettings settings = SimulationLodSettings.Default;
        [SerializeField, Min(0.05f)] private float solveIntervalSeconds = 0.25f;
        [Tooltip("Nothing in this camera's view is demoted or stays parked. Empty: the main camera.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Without a view camera, use Camera.main.")]
        [SerializeField] private bool useMainCameraFallback = true;
        [Tooltip("Off deactivates the agent object. Unticked: Off only suspends its thinking. Applied to adopted agents.")]
        [SerializeField] private bool deactivateWhenOff = true;
        [Tooltip("A noise or damage keeps an agent it reaches Full for this long.")]
        [SerializeField, Min(0f)] private float wakeSeconds = 6f;
        [Tooltip("Adopt every registered SimulationAgent of this scene automatically.")]
        [SerializeField] private bool autoAdoptAgents = true;
        [Tooltip("Use every enabled SimulationObserver as an observer, besides SetObservers and the observer source.")]
        [SerializeField] private bool includeObserverComponents = true;
        [Tooltip("Use every enabled SimulationVolume of this scene, besides AddVolume.")]
        [SerializeField] private bool includeRegisteredVolumes = true;

        private readonly List<Transform> explicitObservers = new List<Transform>();
        private readonly List<Transform> observers = new List<Transform>();
        private readonly List<int> observerSegments = new List<int>();
        // A parked agent leaves nothing, but a disabled one leaves the registry: the driver keeps its own list.
        private readonly List<SimulationAgent> managed = new List<SimulationAgent>();
        private readonly HashSet<SimulationAgent> known = new HashSet<SimulationAgent>();
        private readonly List<SimulationVolume> explicitVolumes = new List<SimulationVolume>();
        private readonly List<SimulationVolume> volumes = new List<SimulationVolume>();
        private readonly Plane[] frustum = new Plane[6];
        private readonly SimulationFrameBudget budget = new SimulationFrameBudget(System.Diagnostics.Stopwatch.Frequency / 1000);
        private ISimulationLodPolicy policy;
        private ISimulationObserverSource observerSource;
        private SimulationSegmentGraph graph;
        private SimulationTier[] segmentTiers;
        private float nextSolveAt;
        private float lastSolveTime;
        private bool forceFullPass;
        private bool hasView;
        private bool hasObservers;
        private bool segmented;
        private int cursor;
        private int volumesVersion = int.MinValue;
        private bool volumesDirty = true;
        private bool hasTrackingVolume;
        private int cycleFull, cycleMinimal, cycleOff, cycleDormant;

        public int FullCount { get; private set; }
        public int MinimalCount { get; private set; }
        public int OffCount { get; private set; }
        public int DormantCount { get; private set; }
        public int ManagedCount => managed.Count;
        public IReadOnlyList<SimulationAgent> Managed => managed;

        /// <summary>The observers used by the latest solve.</summary>
        public IReadOnlyList<Transform> Observers => observers;

        /// <summary>The volumes the driver currently uses.</summary>
        public IReadOnlyList<SimulationVolume> Volumes => volumes;

        public SimulationLodSettings Settings { get => settings; set => settings = value; }
        public float SolveIntervalSeconds { get => solveIntervalSeconds; set => solveIntervalSeconds = Mathf.Max(0.05f, value); }
        public float WakeSeconds { get => wakeSeconds; set => wakeSeconds = Mathf.Max(0f, value); }
        public bool AutoAdoptAgents { get => autoAdoptAgents; set => autoAdoptAgents = value; }
        public bool IncludeObserverComponents { get => includeObserverComponents; set => includeObserverComponents = value; }
        public bool IncludeRegisteredVolumes
        {
            get => includeRegisteredVolumes;
            set
            {
                includeRegisteredVolumes = value;
                volumesDirty = true;
            }
        }

        /// <summary>Counters of the per-frame budgets (thinks, activation changes).</summary>
        public SimulationFrameBudget Budget => budget;

        /// <summary>True while <see cref="BeginWakeAll"/> brings parked agents back a few per frame.</summary>
        public bool IsWakingAll { get; private set; }

        private bool wakeAllIncludesDormant;

        /// <summary>Raised for each agent <see cref="BeginWakeAll"/> brought back.</summary>
        public event Action<SimulationAgent> Woken;

        /// <summary>Raised once <see cref="BeginWakeAll"/> has nothing left to wake.</summary>
        public event Action WakeAllCompleted;

        public bool DeactivateWhenOff
        {
            get => deactivateWhenOff;
            set
            {
                deactivateWhenOff = value;
                for (int i = 0; i < managed.Count; i++)
                {
                    if (managed[i] != null) managed[i].ParkWhenOff = value;
                }
            }
        }

        private bool IsRunning => this != null && enabled && gameObject.activeInHierarchy;

        /// <summary>The camera whose view protects agents from demotion. Null: the main camera (when allowed).</summary>
        public void SetViewCamera(Camera camera) => viewCamera = camera;

        public void SetSegmentMap(SimulationSegmentMap map)
        {
            segmentMap = map;
            graph = null;
        }

        /// <summary>The game's additions to the tier rules (orders, parking permission). Null: none.</summary>
        public void SetPolicy(ISimulationLodPolicy value) => policy = value;

        /// <summary>A source asked for observers at every solve, besides <see cref="SetObservers"/>. Null: none.</summary>
        public void SetObserverSource(ISimulationObserverSource source) => observerSource = source;

        /// <summary>The transforms around which the world must run fully: every living hero.</summary>
        public void SetObservers(IReadOnlyList<Transform> transforms)
        {
            explicitObservers.Clear();
            if (transforms == null) return;
            for (int i = 0; i < transforms.Count; i++)
            {
                if (transforms[i] != null) explicitObservers.Add(transforms[i]);
            }
        }

        /// <summary>Takes an agent under this driver's care now (instead of waiting for automatic adoption).</summary>
        public void Register(SimulationAgent agent)
        {
            if (agent == null || known.Contains(agent) || agent.Owner != null && agent.Owner != this) return;
            // Observers, view and volumes as of now, so the first forced tier respects them.
            Prepare(lastSolveTime, false);
            AdoptAgent(agent, lastSolveTime);
        }

        /// <summary>Releases an agent: it returns to Full and is no longer managed by this driver.</summary>
        public void Unregister(SimulationAgent agent)
        {
            if (agent == null || !known.Remove(agent)) return;
            int index = managed.IndexOf(agent);
            if (index >= 0)
            {
                managed.RemoveAt(index);
                if (index < cursor) cursor--;
            }

            if (agent.CanManage(this))
            {
                agent.State.Reset();
                agent.WakeUntil = 0f;
                agent.ApplyTier(SimulationTier.Full, false);
            }
            else if (agent.Owner == this && agent.IsParked)
            {
                // Ineligible or disabled while parked: never leave an object this driver deactivated inactive.
                agent.RestoreFromPark();
            }

            agent.ReleaseOwner(this);
        }

        /// <summary>Uses a volume (in addition to the registered ones, or when registration is off or unavailable).</summary>
        public void AddVolume(SimulationVolume volume)
        {
            if (volume == null || explicitVolumes.Contains(volume)) return;
            explicitVolumes.Add(volume);
            volumesDirty = true;
        }

        public void RemoveVolume(SimulationVolume volume)
        {
            if (explicitVolumes.Remove(volume)) volumesDirty = true;
        }

        /// <summary>True when <paramref name="candidate"/> is (or is a child of) one of the current observers.</summary>
        public bool IsObserver(Transform candidate)
        {
            if (candidate == null) return false;
            for (int i = 0; i < observers.Count; i++)
            {
                if (observers[i] != null && (observers[i] == candidate || candidate.IsChildOf(observers[i]))) return true;
            }

            for (int i = 0; i < explicitObservers.Count; i++)
            {
                if (explicitObservers[i] != null && (explicitObservers[i] == candidate || candidate.IsChildOf(explicitObservers[i]))) return true;
            }

            return false;
        }

        /// <summary>True when any enabled driver counts <paramref name="candidate"/> as an observer.</summary>
        public static bool IsObserverOfAnyDriver(Transform candidate)
        {
            for (int i = 0; i < drivers.Count; i++)
            {
                if (drivers[i] != null && drivers[i].IsObserver(candidate)) return true;
            }

            return false;
        }

        /// <summary>
        /// A noise at <paramref name="origin"/> that carries <paramref name="radius"/> metres. A parked agent cannot
        /// hear, so every agent in reach is kept Full for a while. Agents dormant by their own request wake when
        /// they allow it; volumes that react to noise and are reached activate.
        /// </summary>
        public void NotifyNoise(Vector3 origin, float radius)
        {
            float reach = radius * radius;
            bool woke = false;
            for (int i = 0; i < managed.Count; i++)
            {
                SimulationAgent agent = managed[i];
                if (agent == null || !agent.CanManage(this) || (agent.transform.position - origin).sqrMagnitude > reach) continue;
                agent.WakeUntil = lastSolveTime + wakeSeconds;
                if (agent.DormantRequested && agent.WakeFromDormantOnNoise) agent.DormantRequested = false;
                woke |= agent.Tier != SimulationTier.Full;
            }

            Prepare(lastSolveTime, false);
            for (int i = 0; i < volumes.Count; i++)
            {
                SimulationVolume volume = volumes[i];
                if (volume != null) volume.NotifyNoise(origin, radius);
            }

            // Footsteps are noises too: only a noise that reaches a demoted agent brings the solve forward.
            if (woke)
            {
                nextSolveAt = 0f;
                forceFullPass = true;
            }
        }

        /// <summary>Damage to a managed agent: keeps it Full for the wake time, may end its dormancy and activate its volume.</summary>
        public void NotifyDamaged(SimulationAgent agent)
        {
            if (agent == null) return;
            agent.WakeUntil = Mathf.Max(agent.WakeUntil, lastSolveTime + wakeSeconds);
            if (agent.DormantRequested && agent.WakeFromDormantOnDamage) agent.DormantRequested = false;
            if (agent.Volume != null) agent.Volume.NotifyOccupantDamaged(agent);
            if (agent.Owner == this && agent.CanManage(this) && agent.ForcedTier == SimulationTier.Full && agent.Tier != SimulationTier.Full)
            {
                nextSolveAt = 0f;
                forceFullPass = true;
            }
        }

        /// <summary>
        /// Puts an agent to sleep (Dormant) or ends its own dormancy, applied now. Ending it does not release a
        /// volume's hold; use <see cref="TryWakeManaged"/> for an explicit wake that does.
        /// </summary>
        public void SetDormant(SimulationAgent agent, bool dormant)
        {
            if (agent == null) return;
            agent.DormantRequested = dormant;
            if (agent.Owner != this || !agent.CanManage(this)) return;
            Prepare(lastSolveTime, false);
            Evaluate(agent, lastSolveTime, true, false);
        }

        /// <summary>
        /// Brings every parked agent back, at most <see cref="SimulationLodSettings.MaxActivationChangesPerFrame"/> per
        /// frame, so an alarm does not activate the whole map in one frame. Nothing is demoted meanwhile. With
        /// <paramref name="includeDormant"/>, dormant agents (own requests and volume holds) wake too. Raises
        /// <see cref="Woken"/> for each and <see cref="WakeAllCompleted"/> at the end; the first batch runs now.
        /// </summary>
        public void BeginWakeAll(bool includeDormant = false)
        {
            if (!IsRunning) return;
            IsWakingAll = true;
            wakeAllIncludesDormant = includeDormant;
            budget.ResetActivationChanges();
            DrainWakeAll();
        }

        /// <summary>True when an eligible agent owned by this driver is parked.</summary>
        public bool IsManagedParked(SimulationAgent agent)
            => agent != null && agent.Owner == this && agent.IsParked && agent.CanManage(this);

        /// <summary>
        /// An explicit wake of a managed agent: ends its dormancy, releases it from its volume, restores it to Full
        /// at once and keeps it there for the wake time. False when the agent is not managed and eligible.
        /// </summary>
        public bool TryWakeManaged(SimulationAgent agent, float now)
        {
            if (!IsRunning || float.IsNaN(now) || float.IsInfinity(now) || now < 0f || agent == null || agent.Owner != this
                || !agent.CanManage(this)) return false;
            agent.ClearDormancy(true);
            agent.State.Reset();
            agent.WakeUntil = Mathf.Max(agent.WakeUntil, now + wakeSeconds);
            agent.ApplyTier(SimulationTier.Full, false);
            return agent.CanManage(this) && agent.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Withdraws an agent from the LOD for good (its run ended, it is being despawned). Listeners are told with
        /// <see cref="SimulationTierChange.Suspended"/>; nothing is reactivated. Ordinary solves never readmit it.
        /// </summary>
        public void SuspendManaged(SimulationAgent agent)
        {
            if (agent != null && agent.Owner == this) agent.Suspend(this, true);
        }

        /// <summary>An outside deactivation (pooling, hiding): suspended until the object is enabled again.</summary>
        internal void SuspendFromDisable(SimulationAgent agent)
        {
            if (agent != null && agent.Owner == this) agent.Suspend(this, false);
        }

        /// <summary>Explicit readmission of a suspended agent that is active, enabled, eligible and in this scene.</summary>
        public bool ReadmitManaged(SimulationAgent agent)
        {
            if (agent == null || agent.Owner != this || !agent.Readmit(this)) return false;
            // A readmission is a new admission, like an adoption: forget the previous run's volume state, find the
            // agent's volume where it stands now, and apply its hold at once (unprotected, like the first time).
            agent.ReleasedFromVolume = false;
            agent.VolumeWakePending = false;
            SimulationVolume previous = agent.Volume;
            if (previous != null && !previous.ListsExplicitly(agent))
            {
                previous.RemoveOccupantInternal(agent);
                agent.Volume = null;
            }

            Prepare(lastSolveTime, false);
            AssignVolume(agent);
            ApplyForcedNow(agent, lastSolveTime, true);
            return true;
        }

        bool ISimulationThinkBudget.TryBeginThink(bool minimal)
        {
            // A destroyed, disabled or alarm-draining driver limits nothing.
            if (!IsRunning || IsWakingAll) return true;
            return budget.TryBeginThink(Time.frameCount, minimal, System.Diagnostics.Stopwatch.GetTimestamp(), in settings);
        }

        void ISimulationThinkBudget.EndThink()
        {
            budget.EndThink(System.Diagnostics.Stopwatch.GetTimestamp());
        }

        internal bool CanManage(SimulationAgent agent) => agent.CanManage(this);

        protected virtual void Reset()
        {
            settings = SimulationLodSettings.Default;
        }

        protected virtual void OnEnable()
        {
            if (settings.FullRadius <= 0f) settings = SimulationLodSettings.Default;
            nextSolveAt = 0f;
            cursor = 0;
            if (!drivers.Contains(this)) drivers.Add(this);
        }

        protected virtual void OnDisable()
        {
            drivers.Remove(this);
            FullCount = MinimalCount = OffCount = DormantCount = 0;
            bool wasWakingAll = IsWakingAll;
            IsWakingAll = false;
            // A scene that is being unloaded takes its agents with it; waking them there would only start
            // components against a world that is already gone.
            if (gameObject.scene.isLoaded)
            {
                for (int i = 0; i < managed.Count; i++)
                {
                    SimulationAgent agent = managed[i];
                    if (agent == null) continue;
                    if (!agent.CanManage(this))
                    {
                        // Ineligible or disabled while parked: never leave an object this driver deactivated inactive.
                        if (agent.Owner == this && agent.IsParked && !agent.IsSuspended) agent.RestoreFromPark();
                        continue;
                    }

                    agent.State.Reset();
                    agent.WakeUntil = 0f;
                    agent.ApplyTier(SimulationTier.Full, false);
                }
            }

            // Every agent is Full now: an interrupted wake-all is complete.
            if (wasWakingAll) WakeAllCompleted?.Invoke();
        }

        protected virtual void Update()
        {
            Step(Time.time, Time.deltaTime, Time.frameCount);
        }

        /// <summary>
        /// One frame: the wake-all drain, volume wakes, a full pass when one is due (no amortisation, or a noise asked
        /// for it), otherwise the next round-robin slice. Public so that tests and custom loops can drive time.
        /// </summary>
        public void Step(float now, float deltaSeconds, int frame)
        {
            budget.BeginFrame(frame);
            if (IsWakingAll)
            {
                DrainWakeAll();
                return;
            }

            if (forceFullPass || settings.MaxEvaluationsPerFrame <= 0)
            {
                if (!forceFullPass && now < nextSolveAt)
                {
                    // Volume wakes do not wait for the interval.
                    if (HasVolumeWork())
                    {
                        Prepare(now, false);
                        ProcessVolumes(now);
                    }

                    return;
                }

                nextSolveAt = now + solveIntervalSeconds;
                forceFullPass = false;
                SolvePass(now);
                return;
            }

            bool intervalDue = now >= nextSolveAt;
            if (intervalDue) nextSolveAt = now + solveIntervalSeconds;
            Prepare(now, intervalDue);
            if (intervalDue) Adopt(now);
            ProcessVolumes(now);
            int slice = SimulationFrameBudget.SliceSize(managed.Count, deltaSeconds, solveIntervalSeconds, settings.MaxEvaluationsPerFrame);
            for (int k = 0; k < slice; k++)
            {
                if (cursor >= managed.Count)
                {
                    PublishCycle();
                    cursor = 0;
                }

                Evaluate(managed[cursor], now, false, false);
                cursor++;
            }

            if (cursor >= managed.Count)
            {
                PublishCycle();
                cursor = 0;
            }
        }

        /// <summary>One full pass over every agent the driver knows. Public so that tests can drive time.</summary>
        public void Solve(float now)
        {
            budget.ResetActivationChanges();
            SolvePass(now);
        }

        private void SolvePass(float now)
        {
            Prepare(now, true);
            Adopt(now);
            ProcessVolumes(now);
            cycleFull = cycleMinimal = cycleOff = cycleDormant = 0;
            for (int i = 0; i < managed.Count; i++) Evaluate(managed[i], now, false, false);
            PublishCycle();
            cursor = 0;
        }

        /// <summary>Observers, view and (on a solve interval) segment tiers.</summary>
        private void Prepare(float now, bool solveSegments)
        {
            if (settings.FullRadius <= 0f) settings = SimulationLodSettings.Default;
            lastSolveTime = now;
            CollectObservers();
            hasObservers = observers.Count > 0;

            // Renderer.isVisible would count every camera, the Editor's Scene view included.
            Camera view = viewCamera != null ? viewCamera : useMainCameraFallback ? Camera.main : null;
            hasView = view != null && view.isActiveAndEnabled;
            if (hasView) GeometryUtility.CalculateFrustumPlanes(view, frustum);

            segmented = segmentMap != null && segmentMap.SegmentCount > 0;
            if (segmented && hasObservers && (solveSegments || graph == null))
            {
                if (graph == null)
                {
                    graph = segmentMap.BuildGraph();
                    segmentTiers = new SimulationTier[graph.Count];
                }

                observerSegments.Clear();
                for (int i = 0; i < observers.Count; i++) observerSegments.Add(segmentMap.SegmentAt(observers[i].position));
                SimulationLodSolver.SolveSegments(graph, observerSegments, segmentTiers);
            }

            RefreshVolumes();
            hasTrackingVolume = HasTrackingVolume();
        }

        private void CollectObservers()
        {
            observers.Clear();
            for (int i = explicitObservers.Count - 1; i >= 0; i--)
            {
                if (explicitObservers[i] == null) explicitObservers.RemoveAt(i);
            }

            for (int i = 0; i < explicitObservers.Count; i++) observers.Add(explicitObservers[i]);
            if (includeObserverComponents)
            {
                IReadOnlyList<SimulationObserver> components = SimulationObserver.Active;
                for (int i = 0; i < components.Count; i++)
                {
                    SimulationObserver observer = components[i];
                    if (observer != null && observer.gameObject.scene == gameObject.scene && !observers.Contains(observer.transform))
                        observers.Add(observer.transform);
                }
            }

            if (observerSource != null)
            {
                int start = observers.Count;
                observerSource.GetObservers(observers);
                for (int i = observers.Count - 1; i >= start; i--)
                {
                    if (observers[i] == null) observers.RemoveAt(i);
                }
            }
        }

        private void RefreshVolumes()
        {
            int version = SimulationVolume.RegistryVersion;
            if (!volumesDirty && version == volumesVersion)
            {
                for (int i = 0; i < volumes.Count; i++)
                {
                    if (volumes[i] == null)
                    {
                        volumesDirty = true;
                        break;
                    }
                }

                if (!volumesDirty) return;
            }

            volumesDirty = false;
            volumesVersion = version;
            volumes.Clear();
            for (int i = explicitVolumes.Count - 1; i >= 0; i--)
            {
                if (explicitVolumes[i] == null) explicitVolumes.RemoveAt(i);
            }

            for (int i = 0; i < explicitVolumes.Count; i++) volumes.Add(explicitVolumes[i]);
            if (includeRegisteredVolumes)
            {
                IReadOnlyList<SimulationVolume> registered = SimulationVolume.Registered;
                for (int i = 0; i < registered.Count; i++)
                {
                    SimulationVolume volume = registered[i];
                    if (volume != null && volume.gameObject.scene == gameObject.scene && !volumes.Contains(volume)) volumes.Add(volume);
                }
            }

            hasTrackingVolume = HasTrackingVolume();

            // A new volume may claim adopted agents that have no volume (and were never released from one), by the
            // tracking rules: explicit listings, or containment in volumes that track membership. Agents that already
            // have a volume keep it. The hold is protected: an awake sticky or visible agent is not put to sleep.
            for (int i = 0; i < managed.Count; i++)
            {
                SimulationAgent agent = managed[i];
                if (agent == null || agent.ReleasedFromVolume || agent.Volume != null) continue;
                SimulationVolume best = FindBestVolume(agent, agent.transform.position, true);
                if (best == null) continue;
                best.AddOccupantInternal(agent);
                ApplyForcedNow(agent, lastSolveTime, false);
            }
        }

        private bool HasVolumeWork()
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                SimulationVolume volume = volumes[i];
                if (volume == null) continue;
                if (volume.IsWaking || volume.IsActive && volume.Deactivation != SimulationVolumeDeactivation.Never) return true;
            }

            return SimulationVolume.RegistryVersion != volumesVersion;
        }

        /// <summary>Deactivation clocks and the wake queues of activated volumes, within the activation budget.</summary>
        private void ProcessVolumes(float now)
        {
            for (int v = 0; v < volumes.Count; v++)
            {
                SimulationVolume volume = volumes[v];
                if (volume == null || !volume.IsOperational) continue;
                if (volume.IsActive && volume.Deactivation == SimulationVolumeDeactivation.WhenObserversLeave)
                {
                    if (float.IsNaN(volume.LastObservedAt) || AnyObserverInside(volume)) volume.LastObservedAt = now;
                    else if (now - volume.LastObservedAt >= volume.DeactivateAfterSeconds)
                    {
                        volume.Deactivate();
                        continue;
                    }
                }

                if (volume.IsWaking) DrainVolume(volume, now);
            }
        }

        private bool AnyObserverInside(SimulationVolume volume)
        {
            for (int i = 0; i < observers.Count; i++)
            {
                if (observers[i] != null && volume.Contains(observers[i].position)) return true;
            }

            return false;
        }

        private void DrainVolume(SimulationVolume volume, float now)
        {
            List<SimulationAgent> queue = volume.PendingQueue;
            int limit = volume.WakeMode == SimulationVolumeWakeMode.AllAtOnce ? int.MaxValue : Mathf.Max(1, volume.WakePerFrame);
            int woke = 0;
            int index = volume.PendingIndex;
            while (index < queue.Count && woke < limit)
            {
                SimulationAgent agent = queue[index];
                if (agent == null || agent.Volume != volume || !agent.VolumeWakePending)
                {
                    index++;
                    continue;
                }

                if (agent.Owner != this || !agent.CanManage(this))
                {
                    // Not ours, dead or withdrawn: it is simply no longer held.
                    agent.VolumeWakePending = false;
                    index++;
                    continue;
                }

                if (!budget.TryChangeActivation(volume.Instant, in settings)) break;
                agent.VolumeWakePending = false;
                index++;
                if (volume.KeepFullSeconds > 0f) agent.WakeUntil = Mathf.Max(agent.WakeUntil, now + volume.KeepFullSeconds);
                Evaluate(agent, now, true, false);
                woke++;
                volume.PendingIndex = index;
                volume.RaiseOccupantWoken(agent);
                // A callback may have deactivated the volume or the driver.
                if (!volume.IsActive || !IsRunning) return;
            }

            volume.PendingIndex = index;
            if (index >= queue.Count) volume.CompleteWake();
        }

        /// <summary>The best volume for an agent at adoption: explicit listing, then priority, then the innermost.</summary>
        private void AssignVolume(SimulationAgent agent)
        {
            if (agent.ReleasedFromVolume) return;
            SimulationVolume best = FindBestVolume(agent, agent.transform.position, false);
            if (best == null || best == agent.Volume) return;
            if (agent.Volume != null) agent.Volume.RemoveOccupantInternal(agent);
            best.AddOccupantInternal(agent);
        }

        /// <summary>
        /// The volume that should govern an agent standing at <paramref name="position"/>: a volume listing it
        /// explicitly, else the containing volume with the highest priority, then the smallest (innermost). With
        /// <paramref name="trackingOnly"/>, containment counts only for volumes that track membership.
        /// </summary>
        private SimulationVolume FindBestVolume(SimulationAgent agent, Vector3 position, bool trackingOnly)
        {
            SimulationVolume best = null;
            bool bestExplicit = false;
            float bestSize = 0f;
            for (int i = 0; i < volumes.Count; i++)
            {
                SimulationVolume volume = volumes[i];
                if (volume == null || !volume.IsOperational) continue;
                bool isExplicit = volume.ListsExplicitly(agent);
                if (!isExplicit && !(volume.AssignByContainment && (!trackingOnly || volume.TrackMembership) && volume.Contains(position))) continue;
                float size = volume.WorldVolume;
                bool better = best == null
                              || isExplicit && !bestExplicit
                              || isExplicit == bestExplicit && (volume.Priority > best.Priority
                                                                || volume.Priority == best.Priority && size < bestSize);
                if (!better) continue;
                best = volume;
                bestExplicit = isExplicit;
                bestSize = size;
            }

            return best;
        }

        /// <summary>
        /// Membership tracking on the evaluation slice: an occupant that left a tracking volume (by containment, not an
        /// explicit listing) is released, and an agent standing in a tracking volume joins it. Allocation-free.
        /// </summary>
        private void UpdateMembership(SimulationAgent agent, Vector3 position)
        {
            SimulationVolume current = agent.Volume;
            if (current != null && agent.VolumeWakePending) return;
            if (current != null && current.IsOperational && current.TrackMembership && !current.Contains(position)
                && !current.ListsExplicitly(agent))
            {
                current.RemoveOccupantInternal(agent);
                agent.Volume = null;
                current = null;
            }

            // An agent woken explicitly out of a volume is never captured again by containment.
            if (agent.ReleasedFromVolume) return;
            if (current != null && current.IsOperational && current.ListsExplicitly(agent)) return;
            SimulationVolume best = FindBestVolume(agent, position, true);
            if (best == null || best == current) return;
            if (current != null)
            {
                if (current.IsOperational && current.Contains(position) && !best.ListsExplicitly(agent)
                    && !IsBetterVolume(best, current)) return;
                current.RemoveOccupantInternal(agent);
            }

            best.AddOccupantInternal(agent);
        }

        private static bool IsBetterVolume(SimulationVolume candidate, SimulationVolume current)
            => candidate.Priority > current.Priority
               || candidate.Priority == current.Priority && candidate.WorldVolume < current.WorldVolume;

        /// <summary>A forced tier (dormancy, inactive volume) the agent does not have yet is applied at once.</summary>
        /// <param name="initial">True only at adoption: the first hold is not protected, so sleepers start asleep.</param>
        private void ApplyForcedNow(SimulationAgent agent, float now, bool initial)
        {
            SimulationTier forced = agent.ForcedTier;
            if (forced == SimulationTier.Full || agent.Tier == forced || !agent.CanManage(this)) return;
            Evaluate(agent, now, true, initial);
        }

        private bool HasTrackingVolume()
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                if (volumes[i] != null && volumes[i].TrackMembership) return true;
            }

            return false;
        }

        /// <param name="prepaid">An out-of-cycle evaluation whose activation change is already paid (or free).</param>
        /// <param name="initial">The first application at adoption: volume holds are not protected.</param>
        private void Evaluate(SimulationAgent agent, float now, bool prepaid, bool initial)
        {
            if (agent == null || !agent.CanManage(this)) return;
            Vector3 position = agent.transform.position;
            float nearest = float.MaxValue;
            for (int o = 0; o < observers.Count; o++)
            {
                Transform observer = observers[o];
                if (observer == null) continue;
                float sqr = (observer.position - position).sqrMagnitude;
                if (sqr < nearest) nearest = sqr;
            }

            // Damage wakes like a noise: an agent that lost health keeps Full for a while.
            float health = agent.SimulationHealth;
            if (health >= 0f)
            {
                bool damaged = agent.LastHealth >= 0f && health < agent.LastHealth;
                agent.LastHealth = health;
                if (damaged) NotifyDamaged(agent);
            }

            if (!prepaid && hasTrackingVolume) UpdateMembership(agent, position);

            // Whatever an observer can see runs fully: a parked agent must never be in view. The view camera is
            // tested exactly; every other observer's screen through the observer view radius.
            bool onScreen = hasView && GeometryUtility.TestPlanesAABB(frustum, agent.ViewBounds)
                            || hasObservers && SimulationLodSolver.InObserverView(nearest, in settings);
            bool engaged = now < agent.WakeUntil || agent.IsSimulationSticky || policy != null && policy.IsSticky(agent);
            bool sticky = onScreen || engaged;

            SimulationTier forced = agent.ForcedTier;
            // A volume hold (not an explicit dormancy request) never takes an awake agent that is sticky, woken or in
            // view during play: it joins the hold once it is calm and unseen. The first application (adoption, a
            // volume appearing) is not protected, so sleepers placed in a building start asleep.
            bool fromVolume = forced != SimulationTier.Full && !agent.DormantRequested;
            if (fromVolume && !initial && agent.Tier < forced && sticky) forced = SimulationTier.Full;

            SimulationLodState next = agent.State;
            bool changed;
            bool urgent;
            if (forced == SimulationTier.Dormant)
            {
                // Dormant wins over distance, camera, stickiness and policy.
                changed = next.Force(SimulationTier.Dormant);
                urgent = false;
            }
            else if (!hasObservers && forced == SimulationTier.Full && next.Tier != SimulationTier.Dormant)
            {
                // Without observers the distance rules pause: the agent keeps its tier.
                if (!prepaid) Count(agent.Tier);
                return;
            }
            else
            {
                SimulationTier segmentTier = SimulationTier.Off;
                if (segmented && hasObservers)
                {
                    int segment = segmentMap.SegmentAt(position);
                    if (segment >= 0) segmentTier = segmentTiers[segment];
                }

                SimulationTier desired = hasObservers
                    ? SimulationLodSolver.Desired(segmentTier, nearest, sticky, in settings)
                    : SimulationTier.Full;
                if (desired == SimulationTier.Off && agent.ParkWhenOff && policy != null)
                    desired = SimulationLodSolver.ClampParking(desired, policy.MayPark(agent));
                // A volume cap is enforced without delay, but never on screen: in view the normal rules apply, so a
                // capped agent is never parked or frozen where an observer can see it.
                if (!onScreen && SimulationLodSolver.ApplyForced(desired, forced, out SimulationTier capped))
                {
                    changed = next.Force(capped);
                    urgent = false;
                }
                else
                {
                    changed = next.Resolve(desired, onScreen, now, settings.DemotionDelaySeconds);
                    urgent = sticky;
                }
            }

            bool park = SimulationLodSolver.Parks(next.Tier, agent.ParkWhenOff, agent.ParkWhenDormant)
                        && (policy == null || policy.MayPark(agent));
            bool toggles = park != agent.IsParked;
            if (changed || toggles)
            {
                // Resolve on a copy: a change refused by the activation budget leaves the old state, so it is simply
                // resolved again at the next evaluation.
                if (!toggles || prepaid || budget.TryChangeActivation(urgent, in settings))
                {
                    agent.State = next;
                    agent.ApplyTier(next.Tier, park);
                }
            }
            else
            {
                agent.State = next;
            }

            // Out-of-cycle evaluations (volume wakes, forced tiers, explicit requests) are not counted twice.
            if (!prepaid) Count(agent.Tier);
        }

        private void Count(SimulationTier tier)
        {
            switch (tier)
            {
                case SimulationTier.Full: cycleFull++; break;
                case SimulationTier.Minimal: cycleMinimal++; break;
                case SimulationTier.Off: cycleOff++; break;
                default: cycleDormant++; break;
            }
        }

        private void PublishCycle()
        {
            FullCount = cycleFull;
            MinimalCount = cycleMinimal;
            OffCount = cycleOff;
            DormantCount = cycleDormant;
            cycleFull = cycleMinimal = cycleOff = cycleDormant = 0;
        }

        private void DrainWakeAll()
        {
            int limit = settings.MaxActivationChangesPerFrame > 0 ? settings.MaxActivationChangesPerFrame : int.MaxValue;
            int woke = 0;
            for (int i = 0; i < managed.Count; i++)
            {
                SimulationAgent agent = managed[i];
                if (agent == null || !agent.CanManage(this)) continue;
                SimulationTier forced = agent.ForcedTier;
                bool dormant = wakeAllIncludesDormant && forced != SimulationTier.Full;
                if (!dormant)
                {
                    if (!agent.IsParked) continue;
                    // A hold that would park it again at its next evaluation (Dormant with ParkWhenDormant, an Off cap)
                    // wakes only with includeDormant; otherwise it would be unparked, reported and re-parked. A
                    // Minimal cap does not park, so an agent parked by distance inside it wakes normally.
                    if (forced != SimulationTier.Full
                        && SimulationLodSolver.Parks(forced, agent.ParkWhenOff, agent.ParkWhenDormant)) continue;
                }

                if (woke >= limit) return;
                if (dormant) agent.ClearDormancy(true);
                agent.State.Reset();
                agent.WakeUntil = 0f;
                agent.ApplyTier(SimulationTier.Full, false);
                budget.TryChangeActivation(true, in settings);
                woke++;
                Woken?.Invoke(agent);
                // A callback may end the drain (disable the driver, end the match).
                if (!IsWakingAll || !IsRunning) return;
            }

            IsWakingAll = false;
            WakeAllCompleted?.Invoke();
        }

        /// <summary>Takes every registered agent the driver has not met yet under its care, and forgets destroyed ones.</summary>
        private void Adopt(float now)
        {
            if (autoAdoptAgents)
            {
                IReadOnlyList<SimulationAgent> active = SimulationAgent.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    SimulationAgent agent = active[i];
                    if (agent == null || known.Contains(agent) || agent.Owner != null && agent.Owner != this) continue;
                    if (agent.gameObject.scene != gameObject.scene || IsObserver(agent.transform)) continue;
                    AdoptAgent(agent, now);
                }
            }

            // Forget destroyed agents; the cursor keeps pointing at the same next agent.
            for (int i = managed.Count - 1; i >= 0; i--)
            {
                if (managed[i] != null) continue;
                managed.RemoveAt(i);
                if (i < cursor) cursor--;
            }

            // So the set does not grow over a long session.
            if (known.Count > managed.Count + 16) known.RemoveWhere(IsDestroyed);
        }

        private static readonly Predicate<SimulationAgent> IsDestroyed = agent => agent == null;

        private void AdoptAgent(SimulationAgent agent, float now)
        {
            known.Add(agent);
            agent.AdoptOwner(this);
            // The driver can veto parking for every agent; it never forces it on an agent that opted out.
            if (!deactivateWhenOff) agent.ParkWhenOff = false;
            managed.Add(agent);
            AssignVolume(agent);
            // A sleeping agent must not run a single round-robin cycle awake.
            ApplyForcedNow(agent, now, true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => drivers.Clear();
    }
}
