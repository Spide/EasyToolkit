using Easy.BehaviourTree;

namespace Easy.SimulationLod.BehaviourTree
{
    /// <summary>
    /// Ticks a behaviour tree at an interval scaled by the agent's tier: the base interval at Full, the interval
    /// times <see cref="SimulationAgent.MinimalThinkScale"/> at Minimal, never at Off, Dormant or while suspended.
    /// Each timed tick asks the driver's frame think budget first; a refused tick keeps its due time and runs in a
    /// later frame (first in line). Plain C#, allocation-free per tick.
    /// <para>
    /// Going to sleep (Off without parking, Dormant) stops the tree (<c>Stop()</c>), like an interrupted plan. A
    /// park keeps the tree as it is, so the agent resumes after <see cref="SimulationAgent.IsUnparking"/>; a
    /// suspension (withdrawal) pauses it without calling Stop(), so no gameplay callback runs.
    /// </para>
    /// </summary>
    public sealed class SimulationTreeTicker<T, V> : ISimulationTierListener where T : IBlackboard<V>
    {
        private float nextTickTime;
        private float intervalSeconds;

        public SimulationTreeTicker(INode<T, V> root, float intervalSeconds)
        {
            Root = root;
            IntervalSeconds = intervalSeconds;
        }

        public INode<T, V> Root { get; set; }

        /// <summary>The interval at Full. 0 ticks every frame (Minimal then ticks every frame too).</summary>
        public float IntervalSeconds
        {
            get => intervalSeconds;
            set => intervalSeconds = value < 0f ? 0f : value;
        }

        /// <summary>The current interval multiplier (1 at Full).</summary>
        public float ThinkScale { get; private set; } = 1f;

        /// <summary>True while the tier forbids ticking.</summary>
        public bool Suspended { get; private set; }

        /// <summary>The budget asked before each timed tick; set from the tier changes. Null: unbudgeted.</summary>
        public ISimulationThinkBudget Budget { get; set; }

        public Result LastResult { get; private set; } = Result.FAILED;

        /// <summary>Ticks run since creation.</summary>
        public int TickCount { get; private set; }

        /// <summary>Times the tree was stopped by going to sleep.</summary>
        public int StopCount { get; private set; }

        /// <summary>The time of the next timed tick.</summary>
        public float NextTickTime => nextTickTime;

        /// <summary>Ticks the tree when it is due, not suspended and the budget allows. True when it ticked.</summary>
        public bool Tick(float now)
        {
            if (Suspended || Root == null || now < nextTickTime) return false;
            ISimulationThinkBudget budget = Budget;
            if (budget != null && !budget.TryBeginThink(ThinkScale > 1f)) return false;
            nextTickTime = now + intervalSeconds * ThinkScale;
            try
            {
                LastResult = Root.Run();
            }
            finally
            {
                budget?.EndThink();
            }

            TickCount++;
            return true;
        }

        /// <summary>Makes the next <see cref="Tick"/> run as soon as it is allowed.</summary>
        public void TickSoon() => nextTickTime = float.MinValue;

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            Budget = change.ThinkBudget;
            float scale = change.ThinkIntervalScale < 1f ? 1f : change.ThinkIntervalScale;
            if (change.Parked)
            {
                // A park pauses without Stop(): the tree resumes where it was after the unpark.
                ThinkScale = scale;
                Suspended = true;
                return;
            }

            bool suspend = change.ThinkSuspended;
            bool wake = Suspended && !suspend || scale < ThinkScale;
            if (suspend && !Suspended && !change.Suspended && Root != null)
            {
                Root.Stop();
                StopCount++;
            }

            ThinkScale = scale;
            Suspended = suspend;
            if (wake) TickSoon();
        }
    }
}
