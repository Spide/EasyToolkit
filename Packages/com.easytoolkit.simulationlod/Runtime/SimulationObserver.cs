using System.Collections.Generic;
using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// Marks a transform (a hero, a player, a bot) around which the world runs fully. Every enabled observer is
    /// used by a <see cref="SimulationLodDriver"/> with "Include Observer Components" on, and passes a
    /// <see cref="SimulationVolume"/> trigger filter that requires an observer. An observer is never simulated.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Observer")]
    public sealed class SimulationObserver : MonoBehaviour
    {
        private static readonly List<SimulationObserver> active = new List<SimulationObserver>();

        /// <summary>Enabled observers, in enabling order.</summary>
        public static IReadOnlyList<SimulationObserver> Active => active;

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();
    }
}
