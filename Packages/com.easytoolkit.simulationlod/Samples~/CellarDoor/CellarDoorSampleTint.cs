using UnityEngine;

namespace Easy.SimulationLod.Samples
{
    /// <summary>A tier listener that tints the renderer by tier, through a property block (no material copies).</summary>
    [DisallowMultipleComponent]
    public sealed class CellarDoorSampleTint : MonoBehaviour, ISimulationTierListener
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;

        public void OnSimulationTierChanged(in SimulationTierChange change)
        {
            var target = GetComponent<Renderer>();
            if (target == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color color = change.Current switch
            {
                SimulationTier.Full => Color.green,
                SimulationTier.Minimal => Color.yellow,
                SimulationTier.Off => Color.grey,
                _ => new Color(0.3f, 0.45f, 1f),
            };
            target.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }
    }
}
