using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Makes direct Play Mode self-healing: entering Play Mode from a catalogued
    /// level scene (or a player build that starts in one) creates the initializer,
    /// which then loads the missing persistent scenes around that level.
    /// </summary>
    public static class SceneFlowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBeforeSceneLoad()
        {
            EnsureInitializerForDirectLevelPlay();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterSceneLoad()
        {
            // Some Editor/player configurations only expose the first scene
            // through SceneManager after load; keep both hooks.
            EnsureInitializerForDirectLevelPlay();
        }

        private static void EnsureInitializerForDirectLevelPlay()
        {
#if UNITY_2022_2_OR_NEWER
            if (Object.FindAnyObjectByType<SceneFlowInitializer>(FindObjectsInactive.Include) != null)
                return;
#else
            if (Object.FindObjectOfType<SceneFlowInitializer>(true) != null)
                return;
#endif

            SceneFlowProfile profile = Resources.Load<SceneFlowProfile>(SceneFlowProfile.ResourcePath);
            if (profile == null || profile.LevelCatalog == null)
                return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!profile.LevelCatalog.TryGetBySceneName(scene.name, out LevelDefinition level))
                    continue;

                SceneFlowInitializer.QueueStartup(new LevelLoadRequest(level.LevelKey));
                CreateInitializer();
                return;
            }

            // Playing a persistent role scene directly starts the default level around it.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string sceneName = SceneManager.GetSceneAt(i).name;
                for (int j = 0; j < profile.PersistentScenes.Count; j++)
                {
                    if (profile.PersistentScenes[j].SceneName == sceneName)
                    {
                        CreateInitializer();
                        return;
                    }
                }
            }
        }

        private static void CreateInitializer()
        {
            new GameObject("SCENE_FLOW_INITIALIZER").AddComponent<SceneFlowInitializer>();
        }
    }
}
