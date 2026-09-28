using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Easy.SceneFlow.Editor
{
    /// <summary>Per-machine Play Mode startup override (level key and entry point).</summary>
    public sealed class SceneFlowOverridesWindow : EditorWindow
    {
        [MenuItem("Tools/Easy Toolkit/Scene Flow/Play Mode Overrides")]
        private static void Open()
        {
            GetWindow<SceneFlowOverridesWindow>("Scene Flow");
        }

        private void OnGUI()
        {
            SceneFlowProfile profile = Resources.Load<SceneFlowProfile>(SceneFlowProfile.ResourcePath);
            if (profile == null || profile.LevelCatalog == null)
            {
                EditorGUILayout.HelpBox($"No profile at Resources/{SceneFlowProfile.ResourcePath}.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "When enabled, Play Mode from the Initialization scene starts this level instead of the profile default. "
                + "Playing a level scene directly always starts that level.",
                MessageType.Info);

            bool enabled = EditorGUILayout.Toggle("Override startup level", EditorPrefs.GetBool(SceneFlowEditorOverrides.EnabledKey, false));
            EditorPrefs.SetBool(SceneFlowEditorOverrides.EnabledKey, enabled);

            IReadOnlyList<LevelDefinition> levels = profile.LevelCatalog.Levels;
            string[] labels = new string[levels.Count];
            string currentKey = EditorPrefs.GetString(SceneFlowEditorOverrides.LevelKeyKey, profile.DefaultLevelKey);
            int selected = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                labels[i] = levels[i] != null ? $"{levels[i].DisplayName} ({levels[i].LevelKey})" : "<missing>";
                if (levels[i] != null && levels[i].LevelKey == currentKey)
                    selected = i;
            }

            using (new EditorGUI.DisabledScope(!enabled || levels.Count == 0))
            {
                selected = EditorGUILayout.Popup("Level", selected, labels);
                if (levels.Count > 0 && levels[selected] != null)
                    EditorPrefs.SetString(SceneFlowEditorOverrides.LevelKeyKey, levels[selected].LevelKey);

                string entry = EditorGUILayout.TextField(
                    "Entry point",
                    EditorPrefs.GetString(SceneFlowEditorOverrides.EntryPointKey, LevelLoadRequest.DefaultEntryPointId));
                EditorPrefs.SetString(SceneFlowEditorOverrides.EntryPointKey, entry);
            }
        }
    }
}
