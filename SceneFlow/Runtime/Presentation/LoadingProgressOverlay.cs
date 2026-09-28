using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>
    /// Placeholder loading screen for the loading scene. Draws the GLOBAL
    /// <see cref="LoadingProgress"/> with IMGUI so the kit needs no UI package.
    /// Replace it with a project-owned view that reads the same service.
    /// IMGUI allocates per frame; acceptable only because the loading scene is
    /// short-lived.
    /// </summary>
    public sealed class LoadingProgressOverlay : MonoBehaviour
    {
        [SerializeField]
        private Color background = new(0.05f, 0.06f, 0.08f, 1f);

        [SerializeField]
        private Color bar = new(0.35f, 0.75f, 1f, 1f);

        private LoadingProgress progress;
        private Texture2D pixel;

        private void OnEnable()
        {
            SceneFlowContexts.TryResolveGlobal(out progress);
            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        private void OnDisable()
        {
            if (pixel != null)
                Destroy(pixel);
        }

        private void OnGUI()
        {
            if (progress == null && !SceneFlowContexts.TryResolveGlobal(out progress))
                return;

            GUI.depth = -1000;
            Rect screen = new(0f, 0f, Screen.width, Screen.height);
            Fill(screen, background);

            LoadingProgressState state = progress.Current;
            float width = Screen.width * 0.5f;
            Rect track = new((Screen.width - width) * 0.5f, Screen.height * 0.6f, width, 8f);
            Fill(track, new Color(1f, 1f, 1f, 0.15f));
            Fill(new Rect(track.x, track.y, track.width * state.Normalized, track.height), bar);
            GUI.Label(new Rect(track.x, track.y - 28f, track.width, 24f), state.CurrentStep);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }
    }
}
