using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// Culls an Animator by tier: its authored culling mode at Full, <see cref="minimalCulling"/> at Minimal and
    /// <see cref="offCulling"/> at Off, Dormant and on suspension. Put it on the agent object (or a child).
    /// </summary>
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Animator Listener")]
    public sealed class SimulationAnimatorListener : MonoBehaviour, ISimulationTierListener
    {
        [Tooltip("Empty: the first Animator on this object or its children.")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimatorCullingMode minimalCulling = AnimatorCullingMode.CullUpdateTransforms;
        [SerializeField] private AnimatorCullingMode offCulling = AnimatorCullingMode.CullCompletely;

        private AnimatorCullingMode fullCulling;
        private bool captured;

        public Animator Animator
        {
            get
            {
                if (animator == null) animator = GetComponentInChildren<Animator>(true);
                return animator;
            }
            set
            {
                animator = value;
                captured = false;
            }
        }

        public AnimatorCullingMode MinimalCulling { get => minimalCulling; set => minimalCulling = value; }
        public AnimatorCullingMode OffCulling { get => offCulling; set => offCulling = value; }

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            Animator target = Animator;
            if (target == null || change.Parked) return;
            if (!captured)
            {
                captured = true;
                fullCulling = target.cullingMode;
            }

            target.cullingMode = change.ThinkSuspended ? offCulling
                : change.Current == SimulationTier.Minimal ? minimalCulling
                : fullCulling;
        }
    }
}
