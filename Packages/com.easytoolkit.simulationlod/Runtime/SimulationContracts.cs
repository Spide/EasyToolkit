using System.Collections.Generic;
using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>What changed when a <see cref="SimulationAgent"/> took a tier. Passed by reference, never boxed.</summary>
    public readonly struct SimulationTierChange
    {
        public SimulationTierChange(
            SimulationAgent agent, SimulationTier previous, SimulationTier current, bool parked, bool suspended,
            float thinkIntervalScale, ISimulationThinkBudget thinkBudget)
        {
            Agent = agent;
            Previous = previous;
            Current = current;
            Parked = parked;
            Suspended = suspended;
            ThinkIntervalScale = thinkIntervalScale;
            ThinkBudget = thinkBudget;
        }

        public SimulationAgent Agent { get; }
        public SimulationTier Previous { get; }
        public SimulationTier Current { get; }

        /// <summary>
        /// True when this change parks the object (it is deactivated right after the listeners ran). A listener
        /// should keep its state across a park; nothing should be torn down. On the unpark the listeners are told
        /// again, after the object is active, with <see cref="Parked"/> false.
        /// </summary>
        public bool Parked { get; }

        /// <summary>
        /// True when the driver withdrew the agent (<see cref="SimulationLodDriver.SuspendManaged"/>). Listeners
        /// must stop work without running gameplay callbacks (for example, pause a tree without Stop()).
        /// </summary>
        public bool Suspended { get; }

        /// <summary>Thinking interval multiplier: 1 at Full, <see cref="SimulationAgent.MinimalThinkScale"/> at Minimal.</summary>
        public float ThinkIntervalScale { get; }

        /// <summary>The frame think budget to ask before a timed think; null when unbudgeted.</summary>
        public ISimulationThinkBudget ThinkBudget { get; }

        /// <summary>True when the agent must not think at all (Off, Dormant, suspended).</summary>
        public bool ThinkSuspended => Suspended || SimulationLodSolver.SuspendsThinking(Current);

        public bool Changed => Previous != Current;
    }

    /// <summary>
    /// A component (or a plain object added with <see cref="SimulationAgent.AddListener"/>) that reacts to tier
    /// changes: slows a sensor, culls an animator, stops a NavMeshAgent. Components on the agent object and its
    /// children are found automatically.
    /// </summary>
    public interface ISimulationTierListener
    {
        void OnSimulationTierChanged(in SimulationTierChange change);
    }

    /// <summary>Optional component: false (dead, despawning) withdraws the agent from the LOD. Every one must be true.</summary>
    public interface ISimulationEligibility
    {
        bool IsSimulationEligible { get; }
    }

    /// <summary>Optional component: true (alerted, fighting, downed) keeps the agent Full wherever it stands.</summary>
    public interface ISimulationStickiness
    {
        bool IsSimulationSticky { get; }
    }

    /// <summary>
    /// Optional component: the agent's health, polled at each evaluation. A drop since the previous evaluation
    /// counts as damage (<see cref="SimulationAgent.NotifyDamaged"/>). Below zero means unknown.
    /// </summary>
    public interface ISimulationHealth
    {
        float SimulationHealth { get; }
    }

    /// <summary>
    /// The game's additions to the tier rules for one agent: an order keeps it Full, and parking can be
    /// forbidden (an online actor not yet replicated).
    /// </summary>
    public interface ISimulationLodPolicy
    {
        /// <summary>True keeps the agent Full wherever it stands (never overrides Dormant or a volume cap).</summary>
        bool IsSticky(SimulationAgent agent);

        /// <summary>False forbids parking: Off is clamped to Minimal, Dormant freezes in place.</summary>
        bool MayPark(SimulationAgent agent);
    }

    /// <summary>Supplies observers (heroes, players, cameras) each solve. Fill the list; never keep it.</summary>
    public interface ISimulationObserverSource
    {
        void GetObservers(List<Transform> observers);
    }
}
