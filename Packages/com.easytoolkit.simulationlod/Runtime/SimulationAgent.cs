using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// One simulated agent. Applies the tier a <see cref="SimulationLodDriver"/> gives it: tells every
    /// <see cref="ISimulationTierListener"/> on its object and children (and any added with
    /// <see cref="AddListener"/>), and parks the object (deactivates it) at Off, or at Dormant when
    /// <see cref="ParkWhenDormant"/> is set. An agent without a driver always runs fully.
    /// <para>
    /// Games either subclass it (override <see cref="IsSimulationEligible"/>, <see cref="IsSimulationSticky"/>,
    /// <see cref="SimulationHealth"/>, <see cref="OnTierChanged"/>) or put small components next to it that
    /// implement <see cref="ISimulationEligibility"/>, <see cref="ISimulationStickiness"/>,
    /// <see cref="ISimulationHealth"/> and <see cref="ISimulationTierListener"/>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Agent")]
    public class SimulationAgent : MonoBehaviour
    {
        private static readonly List<SimulationAgent> active = new List<SimulationAgent>();

        /// <summary>Agents that registered themselves (enabled, <see cref="AutoRegister"/>). Parked agents stay listed.</summary>
        public static IReadOnlyList<SimulationAgent> Active => active;

        [Tooltip("Thinking at Minimal: the thinker's own interval times this.")]
        [SerializeField, Min(1f)] private float minimalThinkScale = 5f;
        [Tooltip("Off deactivates the object. Unticked: Off only suspends thinking.")]
        [SerializeField] private bool parkWhenOff = true;
        [Tooltip("Dormant deactivates the object. Unticked (default): a dormant agent stays visible, frozen.")]
        [SerializeField] private bool parkWhenDormant;
        [Tooltip("Starts dormant: asleep until it is activated (wake call, noise, damage).")]
        [SerializeField] private bool startDormant;
        [Tooltip("A noise routed through the driver wakes this agent from its own dormancy (not a volume's).")]
        [SerializeField] private bool wakeFromDormantOnNoise = true;
        [Tooltip("Damage wakes this agent from its own dormancy (not a volume's).")]
        [SerializeField] private bool wakeFromDormantOnDamage = true;
        [Tooltip("Registers itself so that a driver adopts it automatically.")]
        [SerializeField] private bool autoRegister = true;
        [Tooltip("After an outside deactivation (pooling, hiding), enabling the object again readmits it. An explicit SuspendManaged always stays out.")]
        [SerializeField] private bool readmitOnEnable = true;
        [Tooltip("Box tested against the view camera's frustum, centred above the pivot.")]
        [SerializeField] private Vector3 viewSize = new Vector3(4f, 3f, 4f);

        private readonly List<ISimulationTierListener> extraListeners = new List<ISimulationTierListener>();
        private ISimulationTierListener[] listeners = Array.Empty<ISimulationTierListener>();
        private ISimulationEligibility[] eligibility = Array.Empty<ISimulationEligibility>();
        private ISimulationStickiness[] stickiness = Array.Empty<ISimulationStickiness>();
        private ISimulationHealth health;
        private bool cached;
        private bool registered;
        private bool dormantRequested;
        private bool dormantRequestInitialised;
        private SimulationLodDriver owner;
        private bool suspended;
        private bool suspendedExplicitly;

        /// <summary>Hysteresis state, owned by the driver.</summary>
        [NonSerialized] public SimulationLodState State;

        /// <summary>Until this time a noise, damage or wake keeps the agent Full. Owned by the driver.</summary>
        [NonSerialized] public float WakeUntil;

        /// <summary>Health at the last evaluation, below zero when unknown. Owned by the driver.</summary>
        [NonSerialized] internal float LastHealth = -1f;

        /// <summary>True while a volume activation has queued this agent and not woken it yet.</summary>
        [NonSerialized] internal bool VolumeWakePending;

        /// <summary>True once a volume released this agent for good (activated and left, or woken explicitly).</summary>
        [NonSerialized] internal bool ReleasedFromVolume;

        /// <summary>Raised after every applied tier (listeners first).</summary>
        public event Action<SimulationAgent> TierChanged;

        public SimulationTier Tier { get; private set; } = SimulationTier.Full;

        /// <summary>The driver that manages this agent, or null.</summary>
        public SimulationLodDriver Owner => owner;

        /// <summary>The volume that governs this agent, or null.</summary>
        public SimulationVolume Volume { get; internal set; }

        /// <summary>The think budget of the managing driver; null when unmanaged or suspended.</summary>
        public ISimulationThinkBudget ThinkBudget => owner != null && !suspended ? owner : null;

        public float MinimalThinkScale { get => minimalThinkScale; set => minimalThinkScale = Mathf.Max(1f, value); }

        /// <summary>True: Off deactivates the object. False: Off only suspends thinking (for an object that must stay active).</summary>
        public bool ParkWhenOff { get => parkWhenOff; set => parkWhenOff = value; }

        /// <summary>The same switch as <see cref="ParkWhenOff"/>, named as in the original port.</summary>
        public bool DeactivateWhenOff { get => parkWhenOff; set => parkWhenOff = value; }

        /// <summary>True: Dormant deactivates the object. False (default): a dormant agent stays visible and frozen.</summary>
        public bool ParkWhenDormant { get => parkWhenDormant; set => parkWhenDormant = value; }

        public bool WakeFromDormantOnNoise { get => wakeFromDormantOnNoise; set => wakeFromDormantOnNoise = value; }
        public bool WakeFromDormantOnDamage { get => wakeFromDormantOnDamage; set => wakeFromDormantOnDamage = value; }

        public bool AutoRegister { get => autoRegister; set => autoRegister = value; }

        /// <summary>
        /// True (default): an agent suspended by an outside deactivation (pooling, hiding) is readmitted when its object
        /// is enabled again. An explicit <see cref="SimulationLodDriver.SuspendManaged"/> always stays out until
        /// <see cref="SimulationLodDriver.ReadmitManaged"/>. False: any withdrawal stays out (the original semantics).
        /// </summary>
        public bool ReadmitOnEnable { get => readmitOnEnable; set => readmitOnEnable = value; }

        /// <summary>True when the current suspension came from <see cref="SimulationLodDriver.SuspendManaged"/>.</summary>
        public bool IsSuspendedExplicitly => suspended && suspendedExplicitly;

        public Vector3 ViewSize { get => viewSize; set => viewSize = value; }

        /// <summary>
        /// The agent's own dormancy request (separate from its volume). Applied at its next evaluation; use
        /// <see cref="SimulationLodDriver.SetDormant"/> to apply it at once. Initialised from "Start Dormant".
        /// </summary>
        public bool DormantRequested
        {
            get
            {
                InitDormantRequest();
                return dormantRequested;
            }
            set
            {
                dormantRequestInitialised = true;
                dormantRequested = value;
            }
        }

        /// <summary>True while this component keeps its own object deactivated.</summary>
        public bool IsParked { get; private set; }

        /// <summary>
        /// True only inside this component's own park deactivation (the OnDisable callbacks of a legitimate LOD
        /// park). Components may keep episodic runtime state across it instead of rebuilding it on wake. Any other
        /// disable (death, external SetActive, unload, suspension) never sees it.
        /// </summary>
        public bool IsParking { get; private set; }

        /// <summary>
        /// True only inside this component's own reactivation of an object it parked (the OnEnable callbacks of
        /// the wake). State kept across <see cref="IsParking"/> may be restored only here; an object reactivated by
        /// anything else, including after <see cref="SimulationLodDriver.SuspendManaged"/>, must rebuild.
        /// </summary>
        public bool IsUnparking { get; private set; }

        /// <summary>True after <see cref="SimulationLodDriver.SuspendManaged"/> until it is readmitted.</summary>
        public bool IsSuspended => suspended;

        /// <summary>False withdraws the agent from the LOD (dead, despawning). Default: every <see cref="ISimulationEligibility"/>.</summary>
        public virtual bool IsSimulationEligible
        {
            get
            {
                Cache();
                for (int i = 0; i < eligibility.Length; i++)
                {
                    ISimulationEligibility source = eligibility[i];
                    if (source is UnityEngine.Object unityObject && unityObject == null) continue;
                    if (!source.IsSimulationEligible) return false;
                }

                return true;
            }
        }

        /// <summary>True keeps the agent Full anywhere. Default: any <see cref="ISimulationStickiness"/>.</summary>
        public virtual bool IsSimulationSticky
        {
            get
            {
                Cache();
                for (int i = 0; i < stickiness.Length; i++)
                {
                    ISimulationStickiness source = stickiness[i];
                    if (source is UnityEngine.Object unityObject && unityObject == null) continue;
                    if (source.IsSimulationSticky) return true;
                }

                return false;
            }
        }

        /// <summary>Current health for the damage wake, or below zero when unknown. Default: the first <see cref="ISimulationHealth"/>.</summary>
        public virtual float SimulationHealth
        {
            get
            {
                Cache();
                if (health == null || health is UnityEngine.Object unityObject && unityObject == null) return -1f;
                return health.SimulationHealth;
            }
        }

        /// <summary>What the driver tests against the frustum of its view camera.</summary>
        public virtual Bounds ViewBounds => new Bounds(transform.position + Vector3.up * (viewSize.y * 0.5f), viewSize);

        /// <summary>The tier this agent is forced to (Dormant or a volume cap), or Full for none.</summary>
        public SimulationTier ForcedTier
        {
            get
            {
                if (DormantRequested) return SimulationTier.Dormant;
                SimulationVolume volume = Volume;
                if (volume != null && volume.IsOperational && (!volume.IsActive || VolumeWakePending))
                    return volume.InactiveTier;
                return SimulationTier.Full;
            }
        }

        /// <summary>Adds a listener that is not a component (a plain tree ticker, for example). Allocation-free afterwards.</summary>
        public void AddListener(ISimulationTierListener listener)
        {
            if (listener != null && !extraListeners.Contains(listener)) extraListeners.Add(listener);
        }

        public void RemoveListener(ISimulationTierListener listener) => extraListeners.Remove(listener);

        /// <summary>Re-reads the listener, eligibility, stickiness and health components (after adding some at runtime).</summary>
        public void RefreshComponents()
        {
            cached = false;
            Cache();
        }

        /// <summary>
        /// Damage happened (called by the game, or detected through <see cref="ISimulationHealth"/>). Keeps the agent
        /// Full for the driver's wake time, wakes it from its own dormancy when allowed, and lets its volume react.
        /// </summary>
        public void NotifyDamaged()
        {
            if (owner != null) owner.NotifyDamaged(this);
            else if (Volume != null) Volume.NotifyOccupantDamaged(this);
        }

        /// <summary>
        /// Applies a tier directly, parking by this agent's own switches. The driver normally does this; calling
        /// it on a managed agent is overwritten at its next evaluation.
        /// </summary>
        public void Apply(SimulationTier tier)
        {
            ApplyTier(tier, SimulationLodSolver.Parks(tier, parkWhenOff, parkWhenDormant));
        }

        internal void ApplyTier(SimulationTier tier, bool park)
        {
            Cache();
            if (suspended || owner != null && !owner.CanManage(this)) return;
            SimulationTier previous = Tier;
            Tier = tier;
            if (park)
            {
                if (IsParked)
                {
                    if (previous != tier) Notify(previous, true);
                    return;
                }

                Notify(previous, true);
                IsParked = true;
                IsParking = true;
                try { gameObject.SetActive(false); }
                finally { IsParking = false; }
                return;
            }

            if (IsParked)
            {
                IsParked = false;
                IsUnparking = true;
                try { gameObject.SetActive(true); }
                finally { IsUnparking = false; }
            }

            Notify(previous, false);
        }

        private void Notify(SimulationTier previous, bool parked)
        {
            float scale = Tier == SimulationTier.Minimal ? minimalThinkScale : 1f;
            var change = new SimulationTierChange(this, previous, Tier, parked, suspended, scale, ThinkBudget);
            for (int i = 0; i < listeners.Length; i++)
            {
                ISimulationTierListener listener = listeners[i];
                if (listener is UnityEngine.Object unityObject && unityObject == null) continue;
                listener.OnSimulationTierChanged(in change);
            }

            for (int i = 0; i < extraListeners.Count; i++) extraListeners[i].OnSimulationTierChanged(in change);
            OnTierChanged(in change);
            TierChanged?.Invoke(this);
        }

        /// <summary>Called after the listeners on every applied tier. Override in a subclass.</summary>
        protected virtual void OnTierChanged(in SimulationTierChange change) { }

        internal void AdoptOwner(SimulationLodDriver driver)
        {
            Cache();
            owner = driver;
            suspended = false;
            LastHealth = -1f;
        }

        internal void ReleaseOwner(SimulationLodDriver driver)
        {
            if (owner != driver) return;
            owner = null;
        }

        internal bool CanManage(SimulationLodDriver driver)
        {
            Cache();
            return owner == driver && driver != null && !suspended && enabled
                   && gameObject.scene == driver.gameObject.scene && gameObject.scene.isLoaded
                   && (transform.parent == null || transform.parent.gameObject.activeInHierarchy)
                   && (gameObject.activeInHierarchy || IsParked)
                   && IsSimulationEligible;
        }

        internal bool Readmit(SimulationLodDriver driver)
        {
            Cache();
            if (owner != driver || !gameObject.activeInHierarchy || !enabled
                || gameObject.scene != driver.gameObject.scene || !gameObject.scene.isLoaded || !IsSimulationEligible) return false;
            suspended = false;
            suspendedExplicitly = false;
            IsParked = false;
            State.Reset();
            WakeUntil = 0f;
            LastHealth = -1f;
            ApplyTier(SimulationTier.Full, false);
            return true;
        }

        /// <summary>
        /// Reactivates an object this agent parked although it can no longer be managed (ineligible, component
        /// disabled), and tells the listeners it is Full again.
        /// </summary>
        internal void RestoreFromPark()
        {
            if (!IsParked) return;
            Cache();
            SimulationTier previous = Tier;
            IsParked = false;
            State.Reset();
            Tier = SimulationTier.Full;
            IsUnparking = true;
            try { gameObject.SetActive(true); }
            finally { IsUnparking = false; }
            Notify(previous, false);
        }

        internal void Suspend(SimulationLodDriver driver, bool explicitly)
        {
            if (owner != driver) return;
            Cache();
            if (suspended)
            {
                suspendedExplicitly |= explicitly;
                return;
            }

            SimulationTier previous = Tier;
            suspended = true;
            suspendedExplicitly = explicitly;
            IsParked = false;
            WakeUntil = 0f;
            State.Reset();
            Tier = SimulationTier.Off;
            // Withdrawal tells the listeners with Suspended set: they stop without gameplay callbacks.
            Notify(previous, false);
        }

        /// <summary>Clears the dormancy request and the volume hold of an explicit wake.</summary>
        internal void ClearDormancy(bool releaseVolume)
        {
            DormantRequested = false;
            VolumeWakePending = false;
            if (releaseVolume && Volume != null)
            {
                Volume.RemoveOccupantInternal(this);
                Volume = null;
                ReleasedFromVolume = true;
            }
        }

        protected virtual void OnEnable()
        {
            if (IsUnparking) return;
            InitDormantRequest();
            if (autoRegister && !registered)
            {
                registered = true;
                active.Add(this);
            }

            // Back from an outside deactivation (pooling, hiding): readmit, so listeners and trees run again.
            if (suspended && !suspendedExplicitly && readmitOnEnable && owner != null) owner.ReadmitManaged(this);
        }

        protected virtual void OnDisable()
        {
            // Parking disables this component too; that is not a reason to undo the tier.
            if (IsParked || IsParking) return;
            if (registered)
            {
                registered = false;
                active.Remove(this);
            }

            if (!cached) return;
            if (owner != null)
            {
                owner.SuspendFromDisable(this);
                return;
            }

            // Anything else that disables it must leave the agent exactly as it was.
            if (Tier != SimulationTier.Full)
            {
                State.Reset();
                Apply(SimulationTier.Full);
            }
        }

        protected virtual void OnDestroy()
        {
            if (registered)
            {
                registered = false;
                active.Remove(this);
            }

            if (Volume != null) Volume.RemoveOccupantInternal(this);
        }

        private void InitDormantRequest()
        {
            if (dormantRequestInitialised) return;
            dormantRequestInitialised = true;
            dormantRequested = startDormant;
        }

        private void Cache()
        {
            if (cached) return;
            cached = true;
            listeners = GetComponentsInChildren<ISimulationTierListener>(true);
            eligibility = GetComponents<ISimulationEligibility>();
            stickiness = GetComponents<ISimulationStickiness>();
            health = GetComponent<ISimulationHealth>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();
    }
}
