using System;
using UnityEngine.Profiling;

namespace Easy.SimulationLod.Tests
{
    /// <summary>
    /// Counts managed allocations (Unity "GC.Alloc" profiler samples on the current thread) of a measured call.
    /// System.GC.GetAllocatedBytesForCurrentThread always reads 0 in the Editor's Mono runtime, so it cannot be used.
    /// </summary>
    internal static class AllocationProbe
    {
        public static long Measure(int iterations, Action measured)
        {
            Recorder recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            long total = 0;
            for (int i = 0; i < iterations; i++)
            {
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
