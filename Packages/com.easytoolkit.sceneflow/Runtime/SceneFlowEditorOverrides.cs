namespace Easy.SceneFlow
{
    /// <summary>
    /// Editor-only startup override stored in EditorPrefs (per machine, never in
    /// builds). Set it from Tools/Easy Toolkit/Scene Flow/Play Mode Overrides.
    /// </summary>
    public static class SceneFlowEditorOverrides
    {
        public const string EnabledKey = "Easy.SceneFlow.Overrides.Enabled";
        public const string LevelKeyKey = "Easy.SceneFlow.Overrides.LevelKey";
        public const string EntryPointKey = "Easy.SceneFlow.Overrides.EntryPoint";

        public static bool TryGetStartupRequest(out LevelLoadRequest request)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EnabledKey, false))
            {
                string levelKey = UnityEditor.EditorPrefs.GetString(LevelKeyKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(levelKey))
                {
                    request = new LevelLoadRequest(
                        levelKey,
                        UnityEditor.EditorPrefs.GetString(EntryPointKey, LevelLoadRequest.DefaultEntryPointId));
                    return true;
                }
            }
#endif
            request = default;
            return false;
        }
    }
}
