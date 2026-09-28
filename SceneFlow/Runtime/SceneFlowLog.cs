using Easy.Logging;
using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>Single Easy Logging channel for scene flow diagnostics.</summary>
    internal static class SceneFlowLog
    {
        private static EasyLogger logger;

        public static EasyLogger Logger =>
            logger ??= LoggerFactory.GetLogger(
                typeof(SceneFlowLog),
                new Color(0.35f, 0.75f, 1f),
                "SCENEFLOW");
    }
}
