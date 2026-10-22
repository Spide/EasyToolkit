using System;

namespace Easy.SimulationLod
{
    /// <summary>
    /// A thinker (behaviour tree, brain) asks this before a timed think. False: the think waits for a later
    /// frame (its timer is not advanced, so it is first in line). Implemented by <see cref="SimulationLodDriver"/>.
    /// </summary>
    public interface ISimulationThinkBudget
    {
        bool TryBeginThink(bool minimal);
        void EndThink();
    }

    /// <summary>
    /// Per-frame budgets of the simulation LOD: thinks, object activation changes and the size of the
    /// round-robin slice. Pure: no Unity types, no allocation; time comes in as timestamps.
    /// </summary>
    public sealed class SimulationFrameBudget
    {
        private readonly long ticksPerMillisecond;
        private int frame = int.MinValue;
        private long thinkStart;
        private bool thinking;

        public SimulationFrameBudget(long ticksPerMillisecond)
        {
            if (ticksPerMillisecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerMillisecond));
            this.ticksPerMillisecond = ticksPerMillisecond;
        }

        public int Frame => frame;
        public int ThinksThisFrame { get; private set; }
        public int MinimalThinksThisFrame { get; private set; }
        public long ThinkTicksThisFrame { get; private set; }
        public int ActivationChangesThisFrame { get; private set; }

        /// <summary>Thinks refused since creation (each retries in a later frame).</summary>
        public long DeferredThinks { get; private set; }

        /// <summary>Non-urgent activation changes refused since creation (each retries later).</summary>
        public long DeferredActivationChanges { get; private set; }

        /// <summary>The highest number of thinks granted in one frame.</summary>
        public int PeakThinksPerFrame { get; private set; }

        /// <summary>Starts a new frame's counters when <paramref name="frameNumber"/> changed.</summary>
        public void BeginFrame(int frameNumber)
        {
            if (frameNumber == frame) return;
            frame = frameNumber;
            ThinksThisFrame = MinimalThinksThisFrame = ActivationChangesThisFrame = 0;
            ThinkTicksThisFrame = 0;
            thinking = false;
        }

        /// <summary>A full solve pass is one frame of activation changes, whatever the frame number.</summary>
        public void ResetActivationChanges() => ActivationChangesThisFrame = 0;

        public bool TryBeginThink(int frameNumber, bool minimal, long timestamp, in SimulationLodSettings settings)
        {
            BeginFrame(frameNumber);
            if (settings.MaxThinksPerFrame > 0 && ThinksThisFrame >= settings.MaxThinksPerFrame
                || minimal && settings.MaxMinimalThinksPerFrame > 0 && MinimalThinksThisFrame >= settings.MaxMinimalThinksPerFrame
                // The first think of a frame always runs, so one slow tree cannot starve every other thinker.
                || settings.ThinkBudgetMilliseconds > 0f && ThinksThisFrame > 0
                   && ThinkTicksThisFrame >= (long)(settings.ThinkBudgetMilliseconds * ticksPerMillisecond))
            {
                DeferredThinks++;
                return false;
            }

            ThinksThisFrame++;
            if (minimal) MinimalThinksThisFrame++;
            if (ThinksThisFrame > PeakThinksPerFrame) PeakThinksPerFrame = ThinksThisFrame;
            thinkStart = timestamp;
            thinking = true;
            return true;
        }

        public void EndThink(long timestamp)
        {
            if (!thinking) return;
            thinking = false;
            long elapsed = timestamp - thinkStart;
            if (elapsed > 0) ThinkTicksThisFrame += elapsed;
        }

        /// <summary>
        /// True when an activation change may happen now. An urgent change always may (and still counts); any
        /// other waits once the frame's budget is spent.
        /// </summary>
        public bool TryChangeActivation(bool urgent, in SimulationLodSettings settings)
        {
            if (!urgent && !HasActivationBudget(in settings))
            {
                DeferredActivationChanges++;
                return false;
            }

            ActivationChangesThisFrame++;
            return true;
        }

        /// <summary>True while a non-urgent activation change would still be granted this frame.</summary>
        public bool HasActivationBudget(in SimulationLodSettings settings)
            => settings.MaxActivationChangesPerFrame <= 0 || ActivationChangesThisFrame < settings.MaxActivationChangesPerFrame;

        /// <summary>
        /// How many of <paramref name="count"/> agents to re-evaluate this frame so that each is seen about once
        /// per <paramref name="periodSeconds"/>, never more than <paramref name="maxPerFrame"/> (0: no cap). With
        /// the cap reached the cycle simply takes longer.
        /// </summary>
        public static int SliceSize(int count, float deltaSeconds, float periodSeconds, int maxPerFrame)
        {
            if (count <= 0) return 0;
            int slice = periodSeconds <= 0f || !(deltaSeconds > 0f) || deltaSeconds >= periodSeconds
                ? count
                : (int)Math.Ceiling(count * (double)deltaSeconds / periodSeconds);
            if (slice < 1) slice = 1;
            if (maxPerFrame > 0 && slice > maxPerFrame) slice = maxPerFrame;
            return slice > count ? count : slice;
        }
    }
}
