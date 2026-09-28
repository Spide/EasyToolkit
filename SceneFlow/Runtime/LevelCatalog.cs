using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>Repository of loadable levels with key and scene uniqueness validation.</summary>
    [CreateAssetMenu(menuName = "Easy Toolkit/Scene Flow/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField]
        private List<LevelDefinition> levels = new();

        public IReadOnlyList<LevelDefinition> Levels => levels;

        public bool TryGetByKey(string levelKey, out LevelDefinition level)
        {
            if (!string.IsNullOrWhiteSpace(levelKey))
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    LevelDefinition candidate = levels[i];
                    if (candidate != null
                        && string.Equals(candidate.LevelKey, levelKey, StringComparison.Ordinal))
                    {
                        level = candidate;
                        return true;
                    }
                }
            }

            level = null;
            return false;
        }

        public bool TryGetBySceneName(string sceneName, out LevelDefinition level)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    LevelDefinition candidate = levels[i];
                    if (candidate != null
                        && string.Equals(candidate.SceneName, sceneName, StringComparison.Ordinal))
                    {
                        level = candidate;
                        return true;
                    }
                }
            }

            level = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDefinition candidate = levels[i];
                if (candidate == null || !candidate.IsValid)
                {
                    error = $"Level catalog '{name}' entry {i} is missing or has no key/scene.";
                    return false;
                }

                if (!keys.Add(candidate.LevelKey))
                {
                    error = $"Level catalog '{name}' has duplicate level key '{candidate.LevelKey}'.";
                    return false;
                }

                if (!scenes.Add(candidate.SceneName))
                {
                    error = $"Level catalog '{name}' has duplicate level scene '{candidate.SceneName}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(params LevelDefinition[] definitions)
        {
            levels.Clear();
            if (definitions != null)
                levels.AddRange(definitions);
        }

        public void AddIfMissing(LevelDefinition definition)
        {
            if (definition != null && !levels.Contains(definition))
                levels.Add(definition);
        }
    }
}
