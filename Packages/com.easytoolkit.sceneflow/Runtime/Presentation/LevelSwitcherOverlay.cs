using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Placeholder level menu (main menu stand-in or development level switcher).
    /// Lists catalog levels, optionally filtered by category, and requests loads
    /// through the GLOBAL <see cref="ILevelLoader"/>. Replace with project UI.
    /// </summary>
    public sealed class LevelSwitcherOverlay : MonoBehaviour
    {
        [SerializeField]
        private string title = "Levels";

        [SerializeField, Tooltip("Empty = all categories.")]
        private string categoryFilter = string.Empty;

        [SerializeField]
        private bool centered = true;

        [SerializeField, Tooltip("Starts collapsed behind a small toggle; typical for debug tools.")]
        private bool collapsible;

        [SerializeField]
        private bool showReloadButton;

        private ILevelLoader loader;
        private bool expanded;
        private string lastMessage = string.Empty;

        private void OnEnable()
        {
            expanded = !collapsible;
            SceneFlowContexts.TryResolveGlobal(out loader);
        }

        private void OnGUI()
        {
            if (loader == null && !SceneFlowContexts.TryResolveGlobal(out loader))
                return;

            if (collapsible)
            {
                if (GUI.Button(new Rect(10f, Screen.height - 40f, 90f, 30f), expanded ? "Close" : title))
                    expanded = !expanded;
                if (!expanded)
                    return;
            }

            LevelCatalog catalog = loader.Catalog;
            int count = catalog != null ? catalog.Levels.Count : 0;
            float width = 260f;
            float height = 70f + count * 34f + (showReloadButton ? 34f : 0f);
            Rect area = centered
                ? new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height)
                : new Rect(10f, Screen.height - height - 50f, width, height);

            GUILayout.BeginArea(area, title, GUI.skin.window);
            GUI.enabled = loader.IsInitialized && !loader.IsTransitioning;
            for (int i = 0; i < count; i++)
            {
                LevelDefinition level = catalog.Levels[i];
                if (level == null
                    || (!string.IsNullOrEmpty(categoryFilter) && level.Category != categoryFilter))
                {
                    continue;
                }

                bool active = level.LevelKey == loader.ActiveLevelKey;
                if (GUILayout.Button(active ? $"> {level.DisplayName}" : level.DisplayName))
                    Load(new LevelLoadRequest(level.LevelKey));
            }

            if (showReloadButton && GUILayout.Button("Reload current"))
                Load(new LevelLoadRequest(loader.ActiveLevelKey, forceReload: true));

            GUI.enabled = true;
            if (!string.IsNullOrEmpty(lastMessage))
                GUILayout.Label(lastMessage);
            GUILayout.EndArea();
        }

        private async void Load(LevelLoadRequest request)
        {
            LevelLoadResult result = await loader.LoadLevelAsync(request);
            lastMessage = result.Succeeded ? string.Empty : result.ToString();
        }
    }
}
