using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// Enables a set of Behaviours only up to a tier: with <see cref="EnabledUpTo"/> = Full they run at Full only,
    /// with Minimal at Full and Minimal. For components that have no LOD of their own (a sensor, an effect, a
    /// look-at). Their authored enabled state is restored when the agent returns within the tier.
    /// </summary>
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Behaviour Toggle")]
    public sealed class SimulationBehaviourToggle : MonoBehaviour, ISimulationTierListener
    {
        [SerializeField] private Behaviour[] targets = new Behaviour[0];
        [SerializeField] private SimulationTier enabledUpTo = SimulationTier.Full;

        private bool[] authored;
        private bool disabledByLod;

        public Behaviour[] Targets
        {
            get => targets;
            set
            {
                targets = value ?? new Behaviour[0];
                authored = null;
                disabledByLod = false;
            }
        }

        public SimulationTier EnabledUpTo { get => enabledUpTo; set => enabledUpTo = value; }

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            if (change.Parked) return;
            bool run = !change.Suspended && change.Current <= enabledUpTo;
            if (!run && !disabledByLod)
            {
                if (authored == null || authored.Length != targets.Length) authored = new bool[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] == null) continue;
                    authored[i] = targets[i].enabled;
                    targets[i].enabled = false;
                }

                disabledByLod = true;
            }
            else if (run && disabledByLod)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null && authored != null && i < authored.Length) targets[i].enabled = authored[i];
                }

                disabledByLod = false;
            }
        }
    }
}
