using System;
using System.Collections.Generic;

namespace Easy.SimulationLod
{
    /// <summary>
    /// How much of an agent runs. A higher value runs less. The order matters: a change to a lower value is a
    /// promotion (immediate), a change to a higher value is a demotion (delayed, never on screen), except for
    /// forced tiers (see <see cref="SimulationLodState.Force"/>).
    /// </summary>
    public enum SimulationTier
    {
        /// <summary>Everything runs.</summary>
        Full = 0,

        /// <summary>Slow thinking, reduced sensing, culled animation.</summary>
        Minimal = 1,

        /// <summary>Nothing runs: the agent is frozen or parked until it is promoted. Reached by distance.</summary>
        Off = 2,

        /// <summary>
        /// Asleep regardless of distance, observers, camera and stickiness. Never reached by distance: only a
        /// volume or an explicit request puts an agent here, and only an explicit activation (a volume activation,
        /// a wake call, a noise or damage routed by the game) brings it back. Once activated the agent follows the
        /// normal rules again. Dormant does not park the object unless <see cref="SimulationAgent.ParkWhenDormant"/>
        /// is set, so a sleeping agent can stay visible.
        /// </summary>
        Dormant = 3,
    }

    /// <summary>The segments of a map (its districts) and which of them touch.</summary>
    public sealed class SimulationSegmentGraph
    {
        private readonly bool[] adjacent;

        public SimulationSegmentGraph(int segmentCount)
        {
            if (segmentCount < 1) throw new ArgumentOutOfRangeException(nameof(segmentCount));
            Count = segmentCount;
            adjacent = new bool[segmentCount * segmentCount];
        }

        public int Count { get; }

        public void Connect(int a, int b)
        {
            if (a == b) return;
            if (a < 0 || b < 0 || a >= Count || b >= Count) throw new ArgumentOutOfRangeException(a < 0 || a >= Count ? nameof(a) : nameof(b));
            adjacent[a * Count + b] = true;
            adjacent[b * Count + a] = true;
        }

        public bool AreNeighbours(int a, int b) => a != b && a >= 0 && b >= 0 && a < Count && b < Count && adjacent[a * Count + b];
    }

    /// <summary>Distances, delays and per-frame budgets of a <see cref="SimulationLodDriver"/>. 0 switches a budget off.</summary>
    [Serializable]
    public struct SimulationLodSettings
    {
        /// <summary>Within this distance of an observer an agent is Full in any segment.</summary>
        public float FullRadius;

        /// <summary>Within this distance of an observer an agent is at least Minimal.</summary>
        public float MinimalRadius;

        /// <summary>A demotion waits this long, so a border is not a flicker line.</summary>
        public float DemotionDelaySeconds;

        /// <summary>
        /// Within this distance of any observer an agent counts as on an observer's screen: it is never demoted.
        /// The driver's view camera is tested as well; this covers every other observer (co-op, bots). 0: off.
        /// </summary>
        public float ObserverViewRadius;

        /// <summary>Agents re-evaluated per frame by the amortised (round-robin) solve. 0: one full pass per interval.</summary>
        public int MaxEvaluationsPerFrame;

        /// <summary>
        /// Parks, unparks and volume wakes per frame that are not urgent. An urgent change (on screen, sticky,
        /// woken, instant volume) is never deferred. 0: unlimited.
        /// </summary>
        public int MaxActivationChangesPerFrame;

        /// <summary>Behaviour tree thinks per frame across every budgeted agent. 0: unlimited.</summary>
        public int MaxThinksPerFrame;

        /// <summary>Of <see cref="MaxThinksPerFrame"/>, at most this many may be Minimal thinks. 0: no separate cap.</summary>
        public int MaxMinimalThinksPerFrame;

        /// <summary>Once the thinks of a frame took this long, later thinks wait for the next frame. 0: no time budget.</summary>
        public float ThinkBudgetMilliseconds;

        public const float DefaultFullRadius = 22f;
        public const float DefaultMinimalRadius = 40f;
        public const float DefaultDemotionDelaySeconds = 3f;
        public const float DefaultObserverViewRadius = 20f;
        public const int DefaultMaxEvaluationsPerFrame = 24;
        public const int DefaultMaxActivationChangesPerFrame = 3;
        public const int DefaultMaxThinksPerFrame = 8;
        public const int DefaultMaxMinimalThinksPerFrame = 2;
        /// <summary>Off by default: measure on the target device before setting a time budget.</summary>
        public const float DefaultThinkBudgetMilliseconds = 0f;

        public static SimulationLodSettings Default => new SimulationLodSettings
        {
            FullRadius = DefaultFullRadius,
            MinimalRadius = DefaultMinimalRadius,
            DemotionDelaySeconds = DefaultDemotionDelaySeconds,
            ObserverViewRadius = DefaultObserverViewRadius,
            MaxEvaluationsPerFrame = DefaultMaxEvaluationsPerFrame,
            MaxActivationChangesPerFrame = DefaultMaxActivationChangesPerFrame,
            MaxThinksPerFrame = DefaultMaxThinksPerFrame,
            MaxMinimalThinksPerFrame = DefaultMaxMinimalThinksPerFrame,
            ThinkBudgetMilliseconds = DefaultThinkBudgetMilliseconds,
        };
    }

    /// <summary>The pure tier rules: no Unity types, no allocation.</summary>
    public static class SimulationLodSolver
    {
        /// <summary>
        /// Full where an observer stands, Minimal next door, Off elsewhere. An observer outside every segment
        /// (index below zero) is ignored here; the radius rule still covers what is near it.
        /// </summary>
        public static void SolveSegments(SimulationSegmentGraph graph, IReadOnlyList<int> observerSegments, SimulationTier[] tiers)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (tiers.Length < graph.Count) throw new ArgumentException("One tier per segment is required.", nameof(tiers));
            for (int s = 0; s < graph.Count; s++) tiers[s] = SimulationTier.Off;
            for (int i = 0; i < observerSegments.Count; i++)
            {
                int segment = observerSegments[i];
                if (segment < 0 || segment >= graph.Count) continue;
                tiers[segment] = SimulationTier.Full;
                for (int s = 0; s < graph.Count; s++)
                {
                    if (tiers[s] == SimulationTier.Off && graph.AreNeighbours(segment, s)) tiers[s] = SimulationTier.Minimal;
                }
            }
        }

        /// <summary>
        /// The distance-based tier of an agent: its segment's tier, raised by its own state. A sticky agent
        /// (fighting, chasing, carrying an order) is Full wherever it stands. Never returns Dormant.
        /// </summary>
        public static SimulationTier Desired(
            SimulationTier segmentTier, float sqrDistanceToNearestObserver, bool sticky, in SimulationLodSettings settings)
        {
            if (sticky) return SimulationTier.Full;
            if (segmentTier > SimulationTier.Off) segmentTier = SimulationTier.Off;
            if (sqrDistanceToNearestObserver <= settings.FullRadius * settings.FullRadius) return SimulationTier.Full;
            if (sqrDistanceToNearestObserver <= settings.MinimalRadius * settings.MinimalRadius && segmentTier > SimulationTier.Minimal)
                return SimulationTier.Minimal;
            return segmentTier;
        }

        /// <summary>
        /// Applies a forced tier (a volume's inactive tier or a dormant request) to a distance-based desire.
        /// <see cref="SimulationTier.Full"/> means "no override". Returns true when the forced tier wins, which is
        /// whenever it runs less than the desire: Dormant always wins, a cap (Minimal, Off) wins when the desire
        /// would run more.
        /// </summary>
        public static bool ApplyForced(SimulationTier desired, SimulationTier forced, out SimulationTier result)
        {
            if (forced > desired)
            {
                result = forced;
                return true;
            }

            result = desired;
            return false;
        }

        /// <summary>True when an agent this far from its nearest observer is on some observer's screen.</summary>
        public static bool InObserverView(float sqrDistanceToNearestObserver, in SimulationLodSettings settings)
            => settings.ObserverViewRadius > 0f
               && sqrDistanceToNearestObserver <= settings.ObserverViewRadius * settings.ObserverViewRadius;

        /// <summary>
        /// The tier an agent may take when parking is forbidden for it (an online actor that is not replicated
        /// yet, for example): Off becomes Minimal, everything else is unchanged. Dormant is never clamped; a
        /// dormant agent that may not park is frozen in place instead.
        /// </summary>
        public static SimulationTier ClampParking(SimulationTier desired, bool mayPark)
            => desired == SimulationTier.Off && !mayPark ? SimulationTier.Minimal : desired;

        /// <summary>True when an agent at <paramref name="tier"/> deactivates its object.</summary>
        public static bool Parks(SimulationTier tier, bool parkWhenOff, bool parkWhenDormant)
            => tier == SimulationTier.Off ? parkWhenOff : tier == SimulationTier.Dormant && parkWhenDormant;

        /// <summary>True when going from one tier to the other switches the agent object on or off (Off only).</summary>
        public static bool TogglesActivation(SimulationTier from, SimulationTier to, bool deactivateWhenOff)
            => TogglesActivation(from, to, deactivateWhenOff, false);

        /// <summary>True when going from one tier to the other switches the agent object on or off.</summary>
        public static bool TogglesActivation(SimulationTier from, SimulationTier to, bool parkWhenOff, bool parkWhenDormant)
            => Parks(from, parkWhenOff, parkWhenDormant) != Parks(to, parkWhenOff, parkWhenDormant);

        /// <summary>True for the tiers in which an agent does not think (Off, Dormant).</summary>
        public static bool SuspendsThinking(SimulationTier tier) => tier >= SimulationTier.Off;
    }

    /// <summary>
    /// One agent's tier with hysteresis: up at once, down after a delay and never on screen. Forced tiers
    /// (Dormant, volume caps) bypass the delay and the screen through <see cref="Force"/>.
    /// </summary>
    public struct SimulationLodState
    {
        private SimulationTier pendingTier;
        private float demoteAt;
        private bool pending;

        public SimulationTier Tier { get; private set; }

        /// <summary>True while a demotion waits for its delay.</summary>
        public bool IsDemotionPending => pending;

        /// <summary>Returns true when <see cref="Tier"/> changed.</summary>
        public bool Resolve(SimulationTier desired, bool onScreen, float now, float demotionDelaySeconds)
        {
            if (desired <= Tier)
            {
                pending = false;
                if (desired == Tier) return false;
                Tier = desired;
                return true;
            }

            if (onScreen)
            {
                pending = false;
                return false;
            }

            if (!pending || pendingTier != desired)
            {
                pending = true;
                pendingTier = desired;
                demoteAt = now + demotionDelaySeconds;
                // Even with no delay a demotion is confirmed by a second evaluation.
                return false;
            }

            if (now < demoteAt) return false;
            pending = false;
            Tier = desired;
            return true;
        }

        /// <summary>Sets the tier at once, whatever the direction, the screen and the delay. True when it changed.</summary>
        public bool Force(SimulationTier tier)
        {
            pending = false;
            if (Tier == tier) return false;
            Tier = tier;
            return true;
        }

        /// <summary>Back to Full with nothing pending, for example when the system is switched off.</summary>
        public void Reset()
        {
            pending = false;
            Tier = SimulationTier.Full;
        }
    }
}
