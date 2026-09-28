using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Stable identity of one loadable level. Callers load levels by
    /// <see cref="LevelKey"/>, never by Unity scene name.
    /// </summary>
    [CreateAssetMenu(menuName = "Easy Toolkit/Scene Flow/Level Definition", fileName = "LevelDefinition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        private string levelKey;

        [SerializeField, Tooltip("Scene name as registered in Build Settings.")]
        private string sceneName;

        [SerializeField, Tooltip("Free-form grouping such as Menu, Gameplay or Debug. Used by menus and tools.")]
        private string category = "Gameplay";

        [SerializeField]
        private string displayName;

        [SerializeField, TextArea]
        private string description;

        public string LevelKey => levelKey;
        public string SceneName => sceneName;
        public string Category => category ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? levelKey : displayName;
        public string Description => description ?? string.Empty;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(levelKey)
            && !string.IsNullOrWhiteSpace(sceneName);

        public void Configure(
            string key,
            string scene,
            string levelCategory = "Gameplay",
            string levelDisplayName = null,
            string levelDescription = null)
        {
            levelKey = key;
            sceneName = scene;
            category = levelCategory;
            displayName = levelDisplayName;
            description = levelDescription;
        }
    }
}
