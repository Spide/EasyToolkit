using UnityEditor;
using UnityEngine;

namespace Easy.SimulationLod.Editor
{
    /// <summary>Scene view gizmos: volume bounds coloured by state with an occupant label, agent tiers when selected.</summary>
    internal static class SimulationLodGizmos
    {
        private static readonly Color InactiveColor = new Color(0.35f, 0.55f, 1f, 0.9f);
        private static readonly Color WakingColor = new Color(1f, 0.6f, 0.1f, 0.95f);
        private static readonly Color ActiveColor = new Color(0.3f, 0.9f, 0.4f, 0.9f);

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        private static void DrawVolume(SimulationVolume volume, GizmoType type)
        {
            bool selected = (type & GizmoType.Selected) != 0;
            Color color = volume.IsWaking ? WakingColor : volume.IsActive ? ActiveColor : InactiveColor;
            if (!selected) color.a *= 0.5f;
            Gizmos.color = color;

            Collider shapeCollider = volume.Shape == SimulationVolumeShape.Collider ? volume.ShapeCollider : null;
            if (shapeCollider == null && volume.Shape == SimulationVolumeShape.Collider) shapeCollider = volume.GetComponent<Collider>();
            Matrix4x4 previous = Gizmos.matrix;
            if (shapeCollider is BoxCollider box)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (shapeCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(volume.WorldCenter, volume.WorldBounds.extents.x);
            }
            else if (shapeCollider != null)
            {
                Bounds bounds = shapeCollider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            else
            {
                Gizmos.matrix = volume.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(volume.Center, volume.Size);
                if (selected)
                {
                    Color fill = color;
                    fill.a = 0.06f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(volume.Center, volume.Size);
                }
            }

            Gizmos.matrix = previous;

            string state = volume.IsWaking ? $"waking ({volume.PendingWakeCount} left)"
                : volume.IsActive ? "active" : $"inactive: {volume.InactiveTier}";
            Handles.color = color;
            Handles.Label(volume.WorldBounds.center + Vector3.up * volume.WorldBounds.extents.y,
                $"{volume.name}\n{state}\n{volume.OccupantCount} occupants");

            if (!selected) return;
            Gizmos.color = color;
            Vector3 center = volume.WorldCenter;
            for (int i = 0; i < volume.Occupants.Count; i++)
            {
                SimulationAgent agent = volume.Occupants[i];
                if (agent != null) Gizmos.DrawLine(center, agent.transform.position);
            }
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawAgent(SimulationAgent agent, GizmoType type)
        {
            if (!Application.isPlaying) return;
            Bounds bounds = agent.ViewBounds;
            Gizmos.color = agent.Tier switch
            {
                SimulationTier.Full => ActiveColor,
                SimulationTier.Minimal => Color.yellow,
                SimulationTier.Off => Color.gray,
                _ => InactiveColor,
            };
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Handles.Label(bounds.center + Vector3.up * bounds.extents.y, agent.IsParked ? $"{agent.Tier} (parked)" : agent.Tier.ToString());
        }
    }

    [CustomEditor(typeof(SimulationLodDriver), true)]
    internal sealed class SimulationLodDriverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var driver = (SimulationLodDriver)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Managed", driver.ManagedCount.ToString());
            EditorGUILayout.LabelField("Observers", driver.Observers.Count.ToString());
            EditorGUILayout.LabelField("Full / Minimal / Off / Dormant",
                $"{driver.FullCount} / {driver.MinimalCount} / {driver.OffCount} / {driver.DormantCount}");
            EditorGUILayout.LabelField("Volumes", driver.Volumes.Count.ToString());
            SimulationFrameBudget budget = driver.Budget;
            EditorGUILayout.LabelField("Thinks this frame (peak)", $"{budget.ThinksThisFrame} ({budget.PeakThinksPerFrame})");
            EditorGUILayout.LabelField("Deferred thinks / activations", $"{budget.DeferredThinks} / {budget.DeferredActivationChanges}");
            if (driver.IsWakingAll) EditorGUILayout.HelpBox("Waking all parked agents...", MessageType.Info);
            Repaint();
        }
    }
}
