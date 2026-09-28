using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow.Editor
{
    /// <summary>
    /// Editor validation of the composition: profile, catalog, Build Settings
    /// membership and order, and level scene authoring (one LevelContext with a
    /// default entry point). Usable from the menu and from EditMode tests.
    /// </summary>
    public static class SceneFlowValidator
    {
        [MenuItem("Tools/Easy Toolkit/Scene Flow/Validate Composition")]
        private static void ValidateFromMenu()
        {
            var errors = new List<string>();
            Validate(Resources.Load<SceneFlowProfile>(SceneFlowProfile.ResourcePath), errors, inspectLevelScenes: true);
            if (errors.Count == 0)
            {
                Debug.Log("Scene flow composition is valid.");
                return;
            }

            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(errors[i]);
        }

        public static bool Validate(SceneFlowProfile profile, List<string> errors, bool inspectLevelScenes)
        {
            int initialCount = errors.Count;
            if (profile == null)
            {
                errors.Add($"Missing Resources/{SceneFlowProfile.ResourcePath}.asset.");
                return false;
            }

            if (!profile.TryValidate(out string profileError))
                errors.Add(profileError);

            var buildScenes = new Dictionary<string, int>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int enabledIndex = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled)
                    continue;
                buildScenes[System.IO.Path.GetFileNameWithoutExtension(scenes[i].path)] = enabledIndex;
                enabledIndex++;
            }

            if (!buildScenes.TryGetValue(profile.InitializationScene, out int initIndex) || initIndex != 0)
                errors.Add($"Initialization scene '{profile.InitializationScene}' must be the first enabled Build Settings scene.");
            RequireInBuild(profile.LoadingScene, buildScenes, errors);
            for (int i = 0; i < profile.PersistentScenes.Count; i++)
                RequireInBuild(profile.PersistentScenes[i].SceneName, buildScenes, errors);

            if (profile.LevelCatalog != null)
            {
                IReadOnlyList<LevelDefinition> levels = profile.LevelCatalog.Levels;
                for (int i = 0; i < levels.Count; i++)
                {
                    if (levels[i] == null)
                        continue;
                    RequireInBuild(levels[i].SceneName, buildScenes, errors);
                    if (inspectLevelScenes)
                        InspectLevelScene(levels[i], scenes, errors);
                }
            }

            return errors.Count == initialCount;
        }

        private static void RequireInBuild(string sceneName, Dictionary<string, int> buildScenes, List<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(sceneName) && !buildScenes.ContainsKey(sceneName))
                errors.Add($"Scene '{sceneName}' is not an enabled Build Settings scene.");
        }

        private static void InspectLevelScene(LevelDefinition level, EditorBuildSettingsScene[] scenes, List<string> errors)
        {
            string path = null;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(scenes[i].path) == level.SceneName)
                    path = scenes[i].path;
            }

            if (path == null)
                return;

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                int contexts = 0;
                LevelContext context = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    LevelContext[] found = roots[i].GetComponentsInChildren<LevelContext>(true);
                    contexts += found.Length;
                    if (found.Length > 0)
                        context = found[0];
                }

                if (contexts != 1)
                {
                    errors.Add($"Level scene '{level.SceneName}' must contain exactly one LevelContext (found {contexts}).");
                    return;
                }

                bool hasDefault = false;
                foreach (LevelEntryPoint entry in context.GetComponentsInChildren<LevelEntryPoint>(true))
                    hasDefault |= entry.EntryPointId == LevelLoadRequest.DefaultEntryPointId;
                if (!hasDefault)
                    errors.Add($"Level scene '{level.SceneName}' has no default LevelEntryPoint under its LevelContext.");
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
