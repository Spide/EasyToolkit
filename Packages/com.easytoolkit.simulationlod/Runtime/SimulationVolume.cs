using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Easy.SimulationLod
{
    /// <summary>The space a <see cref="SimulationVolume"/> covers.</summary>
    public enum SimulationVolumeShape
    {
        /// <summary>The volume's own box (center, size) in its local space; rotation and scale apply.</summary>
        Box,

        /// <summary>A collider on the volume (BoxCollider and SphereCollider exactly, others by closest point).</summary>
        Collider,
    }

    /// <summary>How an activated volume wakes its occupants.</summary>
    public enum SimulationVolumeWakeMode
    {
        /// <summary>Every occupant as soon as the driver's activation budget allows (or at once when instant).</summary>
        AllAtOnce,

        /// <summary>At most "Wake Per Frame" occupants per frame, in occupant order.</summary>
        Staggered,

        /// <summary>At most "Wake Per Frame" occupants per frame, nearest to the activation origin first.</summary>
        NearestFirst,
    }

    /// <summary>When an active volume returns to its inactive state.</summary>
    public enum SimulationVolumeDeactivation
    {
        /// <summary>Once activated, it stays active.</summary>
        Never,

        /// <summary>After no observer has been inside for "Deactivate After Seconds".</summary>
        WhenObserversLeave,
    }

    /// <summary>
    /// A space whose occupants sleep (or run capped) until it is activated: the house or cellar of sleeping
    /// zombies, a closed bunker, a dormant nest. While inactive, its <see cref="InactiveTier"/> overrides the
    /// distance-based tiers of its occupants: Dormant (asleep whatever the distance and camera) or a cap
    /// (Minimal, Off). <see cref="Activate()"/> (a door opened, a script, a behaviour tree action), a trigger
    /// entered by an observer, a noise reaching it or damage to an occupant activate it; the occupants then wake
    /// by its wake mode within the driver's activation budget and follow the normal rules.
    /// <para>
    /// Occupants are the agents listed explicitly plus, with "Assign By Containment", every agent standing inside
    /// it when the driver adopts it. An agent belongs to one volume only: an explicit listing wins, then the higher
    /// priority, then the smaller (innermost) volume.
    /// </para>
    /// <para>
    /// Deterministic and authority-agnostic: activation is a method call. The package does no networking: in a
    /// host-authoritative game, activate on the host only (disable the trigger on clients) and replicate the
    /// result (the agents' state) as the game already does.
    /// </para>
    /// </summary>
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Volume")]
    public class SimulationVolume : MonoBehaviour
    {
        private static readonly List<SimulationVolume> registry = new List<SimulationVolume>();

        /// <summary>Enabled volumes. <see cref="RegistryVersion"/> changes whenever this list does.</summary>
        public static IReadOnlyList<SimulationVolume> Registered => registry;

        /// <summary>Changes whenever a volume is enabled or disabled.</summary>
        public static int RegistryVersion { get; private set; }

        [Header("Shape")]
        [SerializeField] private SimulationVolumeShape shape = SimulationVolumeShape.Box;
        [SerializeField] private Vector3 center = new Vector3(0f, 2f, 0f);
        [SerializeField] private Vector3 size = new Vector3(10f, 4f, 10f);
        [Tooltip("Collider shape: this collider. Empty: the first collider on this object.")]
        [SerializeField] private Collider shapeCollider;
        [Tooltip("Of two volumes that contain an agent, the higher priority wins; on a tie the smaller one.")]
        [SerializeField] private int priority;

        [Header("State")]
        [Tooltip("The tier forced on the occupants while inactive: Dormant, or a cap (Minimal, Off).")]
        [SerializeField] private SimulationTier inactiveTier = SimulationTier.Dormant;
        [SerializeField] private bool startActive;

        [Header("Occupants")]
        [SerializeField] private bool assignByContainment = true;
        [Tooltip("Re-check containment on the driver's evaluation slice: agents that enter (or spawn) join, occupants that leave are released.")]
        [SerializeField] private bool trackMembership = true;
        [SerializeField] private List<SimulationAgent> explicitOccupants = new List<SimulationAgent>();

        [Header("Activation")]
        [Tooltip("A collider entering this volume's trigger collider activates it (filtered below).")]
        [SerializeField] private bool activateOnTrigger = true;
        [SerializeField] private LayerMask triggerLayers = ~0;
        [Tooltip("Empty: any tag.")]
        [SerializeField] private string triggerTag = string.Empty;
        [Tooltip("Only a SimulationObserver (or a driver's observer) activates it.")]
        [SerializeField] private bool triggerRequiresObserver = true;
        [Tooltip("A noise routed through the driver that reaches this volume activates it.")]
        [SerializeField] private bool activateOnNoise = true;
        [Tooltip("Damage to an occupant activates it.")]
        [SerializeField] private bool activateOnOccupantDamage = true;

        [Header("Wake")]
        [SerializeField] private SimulationVolumeWakeMode wakeMode = SimulationVolumeWakeMode.Staggered;
        [SerializeField, Min(1)] private int wakePerFrame = 2;
        [Tooltip("Ignore the driver's activation budget: every wake of this volume is urgent.")]
        [SerializeField] private bool instant;
        [Tooltip("A woken occupant stays Full this long before the normal rules may demote it. 0: none.")]
        [SerializeField, Min(0f)] private float keepFullSeconds = 2f;

        [Header("Deactivation")]
        [SerializeField] private SimulationVolumeDeactivation deactivation = SimulationVolumeDeactivation.Never;
        [SerializeField, Min(0f)] private float deactivateAfterSeconds = 15f;

        [Header("Events")]
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

        private readonly List<SimulationAgent> occupants = new List<SimulationAgent>();
        private readonly List<SimulationAgent> pending = new List<SimulationAgent>();
        private readonly DistanceComparer distanceComparer = new DistanceComparer();
        private int pendingIndex;
        private bool isActive;
        private bool stateInitialised;
        private bool registered;
        private Vector3 activationOrigin;
        private float lastObservedAt = float.NaN;

        /// <summary>Raised once per activation, after the wake queue is built (<see cref="Occupants"/> lists who wakes).</summary>
        public event Action<SimulationVolume> Activated;

        /// <summary>Raised when it returns to its inactive state.</summary>
        public event Action<SimulationVolume> Deactivated;

        /// <summary>Raised for each occupant woken by the activation, right after its tier was applied.</summary>
        public event Action<SimulationVolume, SimulationAgent> OccupantWoken;

        /// <summary>Raised once the wake queue of an activation is empty.</summary>
        public event Action<SimulationVolume> WakeCompleted;

        public SimulationVolumeShape Shape { get => shape; set => shape = value; }
        public Vector3 Center { get => center; set => center = value; }
        public Vector3 Size { get => size; set => size = value; }
        public Collider ShapeCollider { get => shapeCollider; set => shapeCollider = value; }
        public int Priority { get => priority; set => priority = value; }
        public SimulationTier InactiveTier { get => inactiveTier; set => inactiveTier = value; }
        public bool AssignByContainment { get => assignByContainment; set => assignByContainment = value; }

        /// <summary>
        /// True (default): the driver re-checks containment as it evaluates each agent, so agents that walk in or spawn
        /// later join, and containment occupants that walk out are released. An agent joining while the volume is
        /// inactive is held only once it is calm and out of view. False: occupants are assigned once, at adoption.
        /// </summary>
        public bool TrackMembership { get => trackMembership; set => trackMembership = value; }
        public bool ActivateOnTrigger { get => activateOnTrigger; set => activateOnTrigger = value; }
        public LayerMask TriggerLayers { get => triggerLayers; set => triggerLayers = value; }
        public string TriggerTag { get => triggerTag; set => triggerTag = value ?? string.Empty; }
        public bool TriggerRequiresObserver { get => triggerRequiresObserver; set => triggerRequiresObserver = value; }
        public bool ActivateOnNoise { get => activateOnNoise; set => activateOnNoise = value; }
        public bool ActivateOnOccupantDamage { get => activateOnOccupantDamage; set => activateOnOccupantDamage = value; }
        public SimulationVolumeWakeMode WakeMode { get => wakeMode; set => wakeMode = value; }
        public int WakePerFrame { get => wakePerFrame; set => wakePerFrame = Mathf.Max(1, value); }
        public bool Instant { get => instant; set => instant = value; }
        public float KeepFullSeconds { get => keepFullSeconds; set => keepFullSeconds = Mathf.Max(0f, value); }
        public SimulationVolumeDeactivation Deactivation { get => deactivation; set => deactivation = value; }
        public float DeactivateAfterSeconds { get => deactivateAfterSeconds; set => deactivateAfterSeconds = Mathf.Max(0f, value); }
        public UnityEvent OnActivatedEvent => onActivated;
        public UnityEvent OnDeactivatedEvent => onDeactivated;

        /// <summary>Agents listed explicitly in the inspector. Read when the driver assigns volumes.</summary>
        public List<SimulationAgent> ExplicitOccupants => explicitOccupants;

        /// <summary>True once activated (until deactivated). Initialised from "Start Active".</summary>
        public bool IsActive
        {
            get
            {
                InitState();
                return isActive;
            }
        }

        /// <summary>True while an activation still has occupants to wake.</summary>
        public bool IsWaking => pendingIndex < pending.Count;

        public int PendingWakeCount => pending.Count - pendingIndex;

        /// <summary>The agents this volume governs (woken ones included until they leave and it deactivates).</summary>
        public IReadOnlyList<SimulationAgent> Occupants => occupants;

        public int OccupantCount => occupants.Count;

        /// <summary>Where the current activation came from (the nearest-first origin).</summary>
        public Vector3 ActivationOrigin => activationOrigin;

        internal bool IsOperational => this != null && enabled && gameObject.activeInHierarchy;

        internal List<SimulationAgent> PendingQueue => pending;

        internal int PendingIndex { get => pendingIndex; set => pendingIndex = value; }

        internal float LastObservedAt { get => lastObservedAt; set => lastObservedAt = value; }

        /// <summary>The world-space centre of the volume.</summary>
        public Vector3 WorldCenter
        {
            get
            {
                Collider c = ResolveCollider();
                if (shape == SimulationVolumeShape.Collider && c != null) return c.bounds.center;
                return transform.TransformPoint(center);
            }
        }

        /// <summary>World-space volume (m³), used to find the innermost of nested volumes.</summary>
        public float WorldVolume
        {
            get
            {
                Collider c = ResolveCollider();
                if (shape == SimulationVolumeShape.Collider && c != null)
                {
                    Vector3 s = c.bounds.size;
                    return Mathf.Abs(s.x * s.y * s.z);
                }

                Vector3 scale = transform.lossyScale;
                return Mathf.Abs(size.x * scale.x * size.y * scale.y * size.z * scale.z);
            }
        }

        /// <summary>World-space bounds (axis aligned, for gizmos and quick rejection).</summary>
        public Bounds WorldBounds
        {
            get
            {
                Collider c = ResolveCollider();
                if (shape == SimulationVolumeShape.Collider && c != null) return c.bounds;
                Vector3 extents = size * 0.5f;
                var bounds = new Bounds(transform.TransformPoint(center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -extents.x : extents.x, (i & 2) == 0 ? -extents.y : extents.y,
                        (i & 4) == 0 ? -extents.z : extents.z);
                    bounds.Encapsulate(transform.TransformPoint(center + corner));
                }

                return bounds;
            }
        }

        /// <summary>True when a world position is inside the volume.</summary>
        public bool Contains(Vector3 worldPosition)
        {
            Collider c = shape == SimulationVolumeShape.Collider ? ResolveCollider() : null;
            if (c == null) return InsideLocalBox(transform, center, size, worldPosition);
            switch (c)
            {
                case BoxCollider box:
                    return InsideLocalBox(box.transform, box.center, box.size, worldPosition);
                case SphereCollider sphere:
                {
                    Vector3 world = sphere.transform.TransformPoint(sphere.center);
                    Vector3 scale = sphere.transform.lossyScale;
                    float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    return (worldPosition - world).sqrMagnitude <= radius * radius;
                }
                default:
                    if (!c.bounds.Contains(worldPosition)) return false;
                    return (c.ClosestPoint(worldPosition) - worldPosition).sqrMagnitude < 1e-6f;
            }
        }

        /// <summary>Squared distance from a world position to the volume (0 inside).</summary>
        public float SqrDistance(Vector3 worldPosition)
        {
            Collider c = shape == SimulationVolumeShape.Collider ? ResolveCollider() : null;
            if (c != null) return Contains(worldPosition) ? 0f : c.bounds.SqrDistance(worldPosition);
            Vector3 local = transform.InverseTransformPoint(worldPosition) - center;
            Vector3 extents = size * 0.5f;
            var clamped = new Vector3(
                Mathf.Clamp(local.x, -extents.x, extents.x),
                Mathf.Clamp(local.y, -extents.y, extents.y),
                Mathf.Clamp(local.z, -extents.z, extents.z));
            return (transform.TransformPoint(center + clamped) - worldPosition).sqrMagnitude;
        }

        /// <summary>Activates the volume from its centre. False when it was already active.</summary>
        public bool Activate() => Activate(WorldCenter);

        /// <summary>
        /// Activates the volume: the wake queue is built from its living occupants (sorted by distance to
        /// <paramref name="origin"/> for <see cref="SimulationVolumeWakeMode.NearestFirst"/>) and the driver wakes
        /// them over the next frames. False when it was already active.
        /// </summary>
        public bool Activate(Vector3 origin)
        {
            InitState();
            if (isActive) return false;
            isActive = true;
            activationOrigin = origin;
            lastObservedAt = float.NaN;
            RemoveDestroyedOccupants();
            pending.Clear();
            pendingIndex = 0;
            for (int i = 0; i < occupants.Count; i++)
            {
                SimulationAgent agent = occupants[i];
                agent.VolumeWakePending = true;
                pending.Add(agent);
            }

            if (wakeMode == SimulationVolumeWakeMode.NearestFirst && pending.Count > 1)
            {
                distanceComparer.Origin = origin;
                pending.Sort(distanceComparer);
            }

            Activated?.Invoke(this);
            onActivated?.Invoke();
            if (pending.Count == 0) WakeCompleted?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Returns the volume to its inactive state. Occupants still inside are put back to <see cref="InactiveTier"/>
        /// at their next evaluation (within the activation budget) once they are calm (not sticky, not woken) and out of
        /// every observer's view; until then they keep running. Occupants that left are released (and may join again).
        /// </summary>
        public void Deactivate()
        {
            InitState();
            if (!isActive) return;
            isActive = false;
            for (int i = pendingIndex; i < pending.Count; i++)
            {
                if (pending[i] != null) pending[i].VolumeWakePending = false;
            }

            pending.Clear();
            pendingIndex = 0;
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                SimulationAgent agent = occupants[i];
                if (agent == null)
                {
                    occupants.RemoveAt(i);
                    continue;
                }

                if (Contains(agent.transform.position) || ListsExplicitly(agent)) continue;
                occupants.RemoveAt(i);
                agent.Volume = null;
            }

            Deactivated?.Invoke(this);
            onDeactivated?.Invoke();
        }

        /// <summary>A noise at <paramref name="origin"/> carrying <paramref name="radius"/> metres. True when it activated the volume.</summary>
        public bool NotifyNoise(Vector3 origin, float radius)
        {
            if (!activateOnNoise || IsActive || !IsOperational) return false;
            if (SqrDistance(origin) > radius * radius) return false;
            return Activate(origin);
        }

        /// <summary>An occupant took damage. True when it activated the volume.</summary>
        public bool NotifyOccupantDamaged(SimulationAgent agent)
        {
            if (!activateOnOccupantDamage || agent == null || agent.Volume != this || IsActive || !IsOperational) return false;
            return Activate(agent.transform.position);
        }

        /// <summary>
        /// Makes this volume govern <paramref name="agent"/> (taking it from another volume). An agent added while
        /// the volume is active is not held.
        /// </summary>
        public void AddOccupant(SimulationAgent agent)
        {
            if (agent == null || agent.Volume == this) return;
            if (agent.Volume != null) agent.Volume.RemoveOccupantInternal(agent);
            agent.ReleasedFromVolume = false;
            AddOccupantInternal(agent);
        }

        /// <summary>Releases <paramref name="agent"/>: it follows the normal rules from its next evaluation.</summary>
        public void RemoveOccupant(SimulationAgent agent)
        {
            if (agent == null || agent.Volume != this) return;
            RemoveOccupantInternal(agent);
            agent.Volume = null;
            agent.VolumeWakePending = false;
            agent.ReleasedFromVolume = true;
        }

        /// <summary>True when the trigger filter (layer, tag, observer) lets <paramref name="other"/> activate the volume.</summary>
        public bool PassesTriggerFilter(Collider other)
        {
            if (other == null) return false;
            GameObject go = other.gameObject;
            if ((triggerLayers.value & (1 << go.layer)) == 0) return false;
            Rigidbody body = other.attachedRigidbody;
            if (!string.IsNullOrEmpty(triggerTag) && !go.CompareTag(triggerTag)
                && (body == null || !body.gameObject.CompareTag(triggerTag))) return false;
            if (!triggerRequiresObserver) return true;
            if (other.GetComponentInParent<SimulationObserver>() != null) return true;
            return SimulationLodDriver.IsObserverOfAnyDriver(other.transform);
        }

        internal bool ListsExplicitly(SimulationAgent agent)
        {
            // A plain reference loop: called on the evaluation slice and must not allocate.
            if (explicitOccupants == null) return false;
            for (int i = 0; i < explicitOccupants.Count; i++)
            {
                if (ReferenceEquals(explicitOccupants[i], agent)) return true;
            }

            return false;
        }

        internal void AddOccupantInternal(SimulationAgent agent)
        {
            if (!occupants.Contains(agent)) occupants.Add(agent);
            agent.Volume = this;
            agent.VolumeWakePending = false;
        }

        internal void RemoveOccupantInternal(SimulationAgent agent)
        {
            occupants.Remove(agent);
        }

        internal void RaiseOccupantWoken(SimulationAgent agent) => OccupantWoken?.Invoke(this, agent);

        internal void CompleteWake()
        {
            pending.Clear();
            pendingIndex = 0;
            WakeCompleted?.Invoke(this);
        }

        protected virtual void OnEnable()
        {
            InitState();
            if (!registered)
            {
                registered = true;
                registry.Add(this);
                RegistryVersion++;
            }
        }

        protected virtual void OnDisable()
        {
            if (!registered) return;
            registered = false;
            registry.Remove(this);
            RegistryVersion++;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!activateOnTrigger || IsActive || !PassesTriggerFilter(other)) return;
            Activate(other.transform.position);
        }

        protected virtual void Reset()
        {
            if (TryGetComponent(out Collider found))
            {
                shapeCollider = found;
                shape = SimulationVolumeShape.Collider;
            }
        }

        private void InitState()
        {
            if (stateInitialised) return;
            stateInitialised = true;
            isActive = startActive;
        }

        /// <summary>The shape collider for the Collider shape, else null. Allocation-free (no GetComponent fake null).</summary>
        private Collider ResolveCollider()
        {
            if (shape != SimulationVolumeShape.Collider) return null;
            if (shapeCollider == null && TryGetComponent(out Collider found)) shapeCollider = found;
            return shapeCollider;
        }

        private void RemoveDestroyedOccupants()
        {
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                if (occupants[i] == null) occupants.RemoveAt(i);
            }
        }

        private static bool InsideLocalBox(Transform space, Vector3 boxCenter, Vector3 boxSize, Vector3 worldPosition)
        {
            Vector3 local = space.InverseTransformPoint(worldPosition) - boxCenter;
            Vector3 extents = boxSize * 0.5f;
            return Mathf.Abs(local.x) <= extents.x && Mathf.Abs(local.y) <= extents.y && Mathf.Abs(local.z) <= extents.z;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            registry.Clear();
            RegistryVersion++;
        }

        private sealed class DistanceComparer : IComparer<SimulationAgent>
        {
            public Vector3 Origin;

            public int Compare(SimulationAgent a, SimulationAgent b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return (a.transform.position - Origin).sqrMagnitude.CompareTo((b.transform.position - Origin).sqrMagnitude);
            }
        }
    }
}
