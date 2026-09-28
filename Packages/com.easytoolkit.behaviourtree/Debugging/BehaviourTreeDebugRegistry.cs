using System;
using System.Collections.Generic;

namespace Easy.BehaviourTree
{
    public static class BehaviourTreeDebugRegistry
    {
        private static readonly Dictionary<object, BehaviourTreeDebugInfo> state = new Dictionary<object, BehaviourTreeDebugInfo>();
        private static readonly Dictionary<object, long> latestTreeRuns = new Dictionary<object, long>();
        private static long nextTreeRunId;

        public static IReadOnlyDictionary<object, BehaviourTreeDebugInfo> State => state;

        public static BehaviourTreeDebugInfo Get(object node)
        {
            if (node == null)
                return null;

            state.TryGetValue(node, out var info);
            return info;
        }

        public static void Clear()
        {
            state.Clear();
            latestTreeRuns.Clear();
            nextTreeRunId = 0;
        }

        public static bool WasVisitedInLatestRun(BehaviourTreeDebugInfo info)
        {
            if (info == null || info.TreeId == null)
                return false;

            return latestTreeRuns.TryGetValue(info.TreeId, out var latestRunId)
                && info.LastTreeRunId == latestRunId;
        }

        internal static void RecordStarted(object node, object treeId, bool startsTreeRun)
        {
            if (node == null)
                return;

            if (treeId == null)
                treeId = node;

            if (startsTreeRun || !latestTreeRuns.ContainsKey(treeId))
                latestTreeRuns[treeId] = ++nextTreeRunId;

            var info = GetOrCreate(node);
            info.TreeId = treeId;
            info.LastTreeRunId = latestTreeRuns[treeId];
            info.IsExecuting = true;
            info.LastStartedAt = NowMs();
            info.LastException = null;
        }

        internal static Result RecordFinished(object node, Result result)
        {
            if (node == null)
                return result;

            var info = GetOrCreate(node);
            info.LastResult = result;
            info.RunCount++;
            info.LastDurationMs = Math.Max(0, NowMs() - info.LastStartedAt);
            info.LastException = null;
            info.IsExecuting = false;
            return result;
        }

        internal static void RecordException(object node, Exception exception)
        {
            if (node == null)
                return;

            var info = GetOrCreate(node);
            info.RunCount++;
            info.LastDurationMs = Math.Max(0, NowMs() - info.LastStartedAt);
            info.LastException = exception.GetType().Name + ": " + exception.Message;
            info.IsExecuting = false;
        }

        private static BehaviourTreeDebugInfo GetOrCreate(object node)
        {
            if (!state.TryGetValue(node, out var info))
            {
                info = new BehaviourTreeDebugInfo();
                state.Add(node, info);
            }

            return info;
        }

        private static double NowMs()
        {
            return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond;
        }
    }
}
