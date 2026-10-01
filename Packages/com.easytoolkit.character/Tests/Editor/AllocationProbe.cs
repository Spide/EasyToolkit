using System;
using UnityEngine.Profiling;

namespace Easy.Character.Tests
{
    /// <summary>
    /// Counts managed allocations (Unity "GC.Alloc" profiler samples on the
    /// current thread) made by a hot-path call. The prepare callback (state
    /// resets, event drivers) runs outside the measured window; only the
    /// measured action is counted. Warm-up iterations absorb JIT, first-use
    /// caches and pool growth.
    /// System.GC.GetAllocatedBytesForCurrentThread always reports a zero delta
    /// in the Editor's Mono runtime, so it cannot be used for this.
    /// </summary>
    internal static class AllocationProbe
    {
        public const int WarmUpIterations = 64;
        public const int MeasuredIterations = 240;

        public static long Measure(Action<int> prepare, Action measured)
        {
            for (int i = 0; i < WarmUpIterations; i++)
            {
                prepare?.Invoke(i);
                measured();
            }

            Recorder recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            long total = 0;
            for (int i = 0; i < MeasuredIterations; i++)
            {
                prepare?.Invoke(WarmUpIterations + i);
                recorder.enabled = true;
                try
                {
                    measured();
                }
                finally
                {
                    recorder.enabled = false;
                }

                total += recorder.sampleBlockCount;
            }

            return total;
        }
    }
}
