using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Easy.SceneFlow
{
    /// <summary>Thin async wrapper over additive <see cref="SceneManager"/> operations.</summary>
    public sealed class SceneLoadService
    {
        private readonly HashSet<Scene> sceneSnapshot = new();

        public bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return true;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public bool CanLoadScene(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName)
                || IsSceneLoaded(sceneName)
                || Application.CanStreamedLevelBeLoaded(sceneName);
        }

        public async Task<bool> LoadAdditiveIfMissingAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || IsSceneLoaded(sceneName))
                return true;

            if (!CanLoadScene(sceneName))
            {
                SceneFlowLog.Logger.LogError("Scene '{0}' is not in Build Settings.", sceneName);
                return false;
            }

            if (!await RunAsync(StartLoad(sceneName)))
                return false;
            return IsSceneLoaded(sceneName);
        }

        /// <summary>Loads another instance of an already loaded scene and returns the new instance.</summary>
        public async Task<Scene> LoadAdditionalInstanceAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
                return default;

            sceneSnapshot.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                sceneSnapshot.Add(SceneManager.GetSceneAt(i));

            if (!await RunAsync(StartLoad(sceneName)))
                return default;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded && candidate.name == sceneName && !sceneSnapshot.Contains(candidate))
                    return candidate;
            }

            return default;
        }

        public Task<bool> UnloadIfLoadedAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return Task.FromResult(true);

            return UnloadAsync(SceneManager.GetSceneByName(sceneName));
        }

        public async Task<bool> UnloadAsync(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return true;

            if (!await RunAsync(SceneManager.UnloadSceneAsync(scene)))
                return false;
            return !scene.IsValid() || !scene.isLoaded;
        }

        private static AsyncOperation StartLoad(string sceneName)
        {
            try
            {
                return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                SceneFlowLog.Logger.LogException(exception, "Could not start loading '{0}'.", sceneName);
                return null;
            }
        }

        private static async Task<bool> RunAsync(AsyncOperation operation)
        {
            if (operation == null)
                return false;

            while (!operation.isDone)
                await Task.Yield();
            return true;
        }
    }
}
