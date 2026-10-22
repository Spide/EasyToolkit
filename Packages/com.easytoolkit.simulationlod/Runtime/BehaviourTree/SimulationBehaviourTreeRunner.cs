using Easy.BehaviourTree;
using UnityEngine;

namespace Easy.SimulationLod.BehaviourTree
{
    /// <summary>
    /// A MonoBehaviour that builds and ticks one behaviour tree with simulation LOD, with no glue code: subclass it
    /// for a blackboard type, return the blackboard and the tree, and put it next to a <see cref="SimulationAgent"/>.
    /// The agent finds it as a tier listener; the tree ticks every <see cref="TickIntervalSeconds"/> at Full, slower
    /// at Minimal, not at all at Off or Dormant, and within the driver's frame think budget. Without an agent or a
    /// driver it simply ticks at its interval.
    /// <code>
    /// public sealed class ZombieTree : SimulationBehaviourTreeRunner&lt;ZombieBlackboard, string&gt;
    /// {
    ///     protected override ZombieBlackboard CreateBlackboard() =&gt; new ZombieBlackboard(this);
    ///     protected override INode&lt;ZombieBlackboard, string&gt; CreateTree(ZombieBlackboard blackboard) =&gt; ...;
    /// }
    /// </code>
    /// </summary>
    public abstract class SimulationBehaviourTreeRunner<T, V> : MonoBehaviour, ISimulationTierListener where T : IBlackboard<V>
    {
        [Tooltip("Seconds between ticks at Full. 0: every frame.")]
        [SerializeField, Min(0f)] private float tickIntervalSeconds = 0.2f;
        [Tooltip("Tick from Update. Off: call TickNow or Tick yourself.")]
        [SerializeField] private bool tickInUpdate = true;

        private SimulationTreeTicker<T, V> ticker;
        private SimulationAgent agent;

        public T Blackboard { get; private set; }

        public INode<T, V> Root => ticker?.Root;

        /// <summary>The ticker (null until the tree is built on first use).</summary>
        public SimulationTreeTicker<T, V> Ticker
        {
            get
            {
                EnsureBuilt();
                return ticker;
            }
        }

        public float TickIntervalSeconds
        {
            get => tickIntervalSeconds;
            set
            {
                tickIntervalSeconds = Mathf.Max(0f, value);
                if (ticker != null) ticker.IntervalSeconds = tickIntervalSeconds;
            }
        }

        public bool TickInUpdate { get => tickInUpdate; set => tickInUpdate = value; }

        protected abstract T CreateBlackboard();

        /// <summary>Builds the tree. It is initialised with the blackboard afterwards.</summary>
        protected abstract INode<T, V> CreateTree(T blackboard);

        /// <summary>Ticks when due and allowed by the tier and the budget. True when the tree ran.</summary>
        public bool Tick(float now)
        {
            EnsureBuilt();
            return ticker.Tick(now);
        }

        /// <summary>One tick now at <c>Time.time</c>, when due and allowed.</summary>
        public bool TickNow() => Tick(Time.time);

        /// <summary>Discards the tree and builds it again on the next tick.</summary>
        public void Rebuild()
        {
            ticker?.Root?.Stop();
            ticker = null;
            Blackboard = default;
        }

        void ISimulationTierListener.OnSimulationTierChanged(in SimulationTierChange change)
        {
            EnsureBuilt();
            ticker.OnSimulationTierChanged(in change);
        }

        protected virtual void Update()
        {
            if (tickInUpdate) Tick(Time.time);
        }

        protected virtual void OnDisable()
        {
            // A park keeps the tree as it is; any other disable stops it like an interrupted plan. IsParking and
            // IsParked are set before the park's SetActive, so this holds whatever the component order.
            if (agent == null) agent = GetComponentInParent<SimulationAgent>(true);
            // An explicit withdrawal (SuspendManaged) promises no gameplay callbacks: the owner cleans up.
            if (agent != null && (agent.IsParking || agent.IsParked || agent.IsSuspendedExplicitly)) return;
            ticker?.Root?.Stop();
        }

        private void EnsureBuilt()
        {
            if (ticker != null) return;
            Blackboard = CreateBlackboard();
            INode<T, V> root = CreateTree(Blackboard);
            root?.Initialize(Blackboard);
            ticker = new SimulationTreeTicker<T, V>(root, tickIntervalSeconds);
            if (agent == null) agent = GetComponentInParent<SimulationAgent>();
            if (agent != null)
            {
                // Catch up with the agent's current tier (the tree may be built after the agent changed tier).
                var current = new SimulationTierChange(agent, SimulationTier.Full, agent.Tier, false, agent.IsSuspended,
                    agent.Tier == SimulationTier.Minimal ? agent.MinimalThinkScale : 1f, agent.ThinkBudget);
                if (agent.Tier != SimulationTier.Full || current.ThinkBudget != null || agent.IsSuspended)
                    ticker.OnSimulationTierChanged(in current);
            }
        }
    }
}
