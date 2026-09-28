using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow.Editor
{
    public sealed class SceneFlowScaffoldOptions
    {
        /// <summary>First-party root, e.g. "Assets/MyGame". Scenes go to Root/Scenes, the profile to Root/Resources/SceneFlow.</summary>
        public string RootFolder = "Assets/Game";
        public string MainMenuLevelKey = "main_menu";
        public string SampleLevelKey = "sample_level";
        public bool CreateSampleLevel = true;
        public bool CreateDebugScene = true;
    }

    public sealed class SceneFlowScaffoldReport
    {
        public readonly List<string> Created = new();
        public readonly List<string> Kept = new();
        public SceneFlowProfile Profile;

        public override string ToString()
        {
            return $"Scene flow scaffold: created {Created.Count} ({string.Join(", ", Created)}); kept {Kept.Count} existing.";
        }
    }

    /// <summary>
    /// Idempotent generator for the standard composition: Initialization,
    /// Loading, Systems, UI, optional Debug, a MainMenu level and a sample level,
    /// plus profile, catalog, level definitions and Build Settings order.
    /// Existing scenes and assets are never overwritten; only missing pieces are
    /// created and catalog/Build Settings membership is repaired.
    /// </summary>
    public static class SceneFlowScaffold
    {
        public const string InitializationScene = "Initialization";
        public const string LoadingScene = "Loading";
        public const string SystemsScene = "Systems";
        public const string UIScene = "UI";
        public const string DebugScene = "Debug";
        public const string MainMenuScene = "MainMenu";
        public const string SampleLevelScene = "SampleLevel";

        private const string RootPrefKey = "Easy.SceneFlow.Scaffold.Root";

        [MenuItem("Tools/Easy Toolkit/Scene Flow/Create Or Repair Composition...")]
        private static void CreateOrRepairFromMenu()
        {
            string current = EditorPrefs.GetString(RootPrefKey, "Assets/Game");
            string picked = EditorUtility.SaveFolderPanel("First-party root folder", current, string.Empty);
            if (string.IsNullOrEmpty(picked))
                return;

            string root = ToProjectRelative(picked);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Scene Flow", "Pick a folder inside this project's Assets folder.", "OK");
                return;
            }

            EditorPrefs.SetString(RootPrefKey, root);
            SceneFlowScaffoldReport report = CreateOrRepair(new SceneFlowScaffoldOptions { RootFolder = root });
            Debug.Log(report);
        }

        /// <summary>Batch entry: -executeMethod Easy.SceneFlow.Editor.SceneFlowScaffold.CreateOrRepairFromCommandLine -sceneFlowRoot Assets/MyGame</summary>
        public static void CreateOrRepairFromCommandLine()
        {
            string root = "Assets/Game";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-sceneFlowRoot")
                    root = args[i + 1];
            }

            Debug.Log(CreateOrRepair(new SceneFlowScaffoldOptions { RootFolder = root }));
        }

        public static SceneFlowScaffoldReport CreateOrRepair(SceneFlowScaffoldOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            string root = options.RootFolder.TrimEnd('/');
            if (!root.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException("Root folder must be inside Assets.", nameof(options));

            var report = new SceneFlowScaffoldReport();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return report;

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            string scenesFolder = $"{root}/Scenes";
            string levelsFolder = $"{scenesFolder}/Levels";
            string contentFolder = $"{root}/Content/Materials";
            string resourcesFolder = $"{root}/Resources/SceneFlow";
            EnsureFolder(scenesFolder);
            EnsureFolder(levelsFolder);
            EnsureFolder(contentFolder);
            EnsureFolder(resourcesFolder);

            // Data first so scenes can reference the profile.
            LevelCatalog catalog = LoadOrCreate<LevelCatalog>($"{scenesFolder}/LevelCatalog.asset", report);
            LevelDefinition mainMenu = LoadOrCreateLevel(
                $"{levelsFolder}/{MainMenuScene}/{MainMenuScene}.level.asset",
                options.MainMenuLevelKey, MainMenuScene, "Menu", "Main Menu", report);
            catalog.AddIfMissing(mainMenu);

            LevelDefinition sample = null;
            if (options.CreateSampleLevel)
            {
                sample = LoadOrCreateLevel(
                    $"{levelsFolder}/{SampleLevelScene}/{SampleLevelScene}.level.asset",
                    options.SampleLevelKey, SampleLevelScene, "Gameplay", "Sample Level", report);
                catalog.AddIfMissing(sample);
            }

            EditorUtility.SetDirty(catalog);

            string profilePath = $"{resourcesFolder}/SceneFlowProfile.asset";
            SceneFlowProfile profile = AssetDatabase.LoadAssetAtPath<SceneFlowProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SceneFlowProfile>();
                var persistent = new List<PersistentSceneEntry>
                {
                    new(SystemsScene, SceneFlowContextNames.Systems),
                    new(UIScene, SceneFlowContextNames.UI),
                };
                if (options.CreateDebugScene)
                    persistent.Add(new PersistentSceneEntry(DebugScene, SceneFlowContextNames.Debug, developmentOnly: true, required: false));

                profile.Configure(catalog, options.MainMenuLevelKey, InitializationScene, LoadingScene, persistent.ToArray());
                AssetDatabase.CreateAsset(profile, profilePath);
                report.Created.Add(profilePath);
            }
            else
            {
                report.Kept.Add(profilePath);
            }

            report.Profile = profile;
            Material ground = LoadOrCreateMaterial($"{contentFolder}/Ground.mat", new Color(0.42f, 0.46f, 0.4f), report);
            AssetDatabase.SaveAssets();

            var ordered = new List<string>();
            ordered.Add(CreateSceneIfMissing($"{scenesFolder}/{InitializationScene}.unity", report, () =>
            {
                var initializer = new GameObject("SceneFlowInitializer").AddComponent<SceneFlowInitializer>();
                SetObject(initializer, "profile", profile);
            }));
            ordered.Add(CreateSceneIfMissing($"{scenesFolder}/{LoadingScene}.unity", report, () =>
            {
                new GameObject("LoadingScreen").AddComponent<LoadingProgressOverlay>();
            }));
            ordered.Add(CreateSceneIfMissing($"{scenesFolder}/{SystemsScene}.unity", report, () =>
            {
                CreateRoleContext("SystemsContext", SceneFlowContextNames.Systems);
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 8f, -10f), Quaternion.Euler(35f, 0f, 0f));
            }));
            ordered.Add(CreateSceneIfMissing($"{scenesFolder}/{UIScene}.unity", report, () =>
            {
                CreateRoleContext("UIContext", SceneFlowContextNames.UI);
            }));
            if (options.CreateDebugScene)
            {
                ordered.Add(CreateSceneIfMissing($"{scenesFolder}/{DebugScene}.unity", report, () =>
                {
                    GameObject context = CreateRoleContext("DebugContext", SceneFlowContextNames.Debug);
                    LevelSwitcherOverlay switcher = context.AddComponent<LevelSwitcherOverlay>();
                    SetString(switcher, "title", "Levels");
                    SetBool(switcher, "centered", false);
                    SetBool(switcher, "collapsible", true);
                    SetBool(switcher, "showReloadButton", true);
                }));
            }

            ordered.Add(CreateSceneIfMissing($"{levelsFolder}/{MainMenuScene}/{MainMenuScene}.unity", report, () =>
            {
                Transform level = CreateLevelRoot();
                CreateChild(level, "EntryPoint").gameObject.AddComponent<LevelEntryPoint>();
                LevelSwitcherOverlay menu = CreateChild(level, "Menu").gameObject.AddComponent<LevelSwitcherOverlay>();
                SetString(menu, "title", "Main Menu");
                SetString(menu, "categoryFilter", "Gameplay");
            }));

            if (options.CreateSampleLevel)
            {
                ordered.Add(CreateSceneIfMissing($"{levelsFolder}/{SampleLevelScene}/{SampleLevelScene}.unity", report, () =>
                {
                    Transform level = CreateLevelRoot();
                    Light sun = CreateChild(level, "Directional Light").gameObject.AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                    GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    plane.name = "Ground";
                    plane.transform.SetParent(level, false);
                    plane.transform.localScale = new Vector3(3f, 1f, 3f);
                    plane.GetComponent<MeshRenderer>().sharedMaterial = ground;

                    CreateChild(level, "EntryPoint").gameObject.AddComponent<LevelEntryPoint>();
                    LevelSwitcherOverlay back = CreateChild(level, "BackToMenu").gameObject.AddComponent<LevelSwitcherOverlay>();
                    SetString(back, "title", "Menu");
                    SetString(back, "categoryFilter", "Menu");
                    SetBool(back, "centered", false);
                }));
            }

            UpdateBuildSettings(ordered);
            AssetDatabase.SaveAssets();
            RestoreSetup(previousSetup, ordered[0]);
            return report;
        }

        private static GameObject CreateRoleContext(string objectName, string contextName)
        {
            var context = new GameObject(objectName).AddComponent<SceneRoleContext>();
            SetString(context, "contextName", contextName);
            return context.gameObject;
        }

        private static Transform CreateLevelRoot()
        {
            return new GameObject("Level").AddComponent<LevelContext>().transform;
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static string CreateSceneIfMissing(string path, SceneFlowScaffoldReport report, Action populate)
        {
            if (File.Exists(path))
            {
                report.Kept.Add(path);
                return path;
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate();
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException($"Could not save scene '{path}'.");
            report.Created.Add(path);
            return path;
        }

        private static void UpdateBuildSettings(List<string> orderedScenePaths)
        {
            var result = new List<EditorBuildSettingsScene>();
            var owned = new HashSet<string>(orderedScenePaths, StringComparer.Ordinal);
            for (int i = 0; i < orderedScenePaths.Count; i++)
                result.Add(new EditorBuildSettingsScene(orderedScenePaths[i], true));

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (!owned.Contains(existing[i].path))
                    result.Add(existing[i]);
            }

            EditorBuildSettings.scenes = result.ToArray();
        }

        private static void RestoreSetup(SceneSetup[] previousSetup, string fallbackScene)
        {
            bool restorable = previousSetup != null && previousSetup.Length > 0;
            if (restorable)
            {
                for (int i = 0; i < previousSetup.Length; i++)
                {
                    if (string.IsNullOrEmpty(previousSetup[i].path) || !File.Exists(previousSetup[i].path))
                        restorable = false;
                }
            }

            if (restorable)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            else
                EditorSceneManager.OpenScene(fallbackScene, OpenSceneMode.Single);
        }

        private static LevelDefinition LoadOrCreateLevel(
            string path, string key, string sceneName, string category, string displayName, SceneFlowScaffoldReport report)
        {
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level != null)
            {
                report.Kept.Add(path);
                return level;
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(key, sceneName, category, displayName);
            AssetDatabase.CreateAsset(level, path);
            report.Created.Add(path);
            return level;
        }

        private static T LoadOrCreate<T>(string path, SceneFlowScaffoldReport report) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                report.Kept.Add(path);
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            report.Created.Add(path);
            return asset;
        }

        private static Material LoadOrCreateMaterial(string path, Color color, SceneFlowScaffoldReport report)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                report.Kept.Add(path);
                return material;
            }

            // Project-owned material on the active pipeline's default shader.
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Shader shader = pipeline != null && pipeline.defaultShader != null
                ? pipeline.defaultShader
                : Shader.Find("Standard");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            report.Created.Add(path);
            return material;
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string ToProjectRelative(string absolute)
        {
            string assets = Application.dataPath.Replace('\\', '/');
            absolute = absolute.Replace('\\', '/');
            if (absolute == assets)
                return "Assets";
            return absolute.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase)
                ? "Assets" + absolute.Substring(assets.Length)
                : null;
        }

        private static void SetString(UnityEngine.Object target, string property, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string property, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
