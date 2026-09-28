namespace Easy.BehaviourTree
{
    public sealed class BehaviourTreeDebugInfo
    {
        public Result? LastResult { get; internal set; }
        public int RunCount { get; internal set; }
        public double LastStartedAt { get; internal set; }
        public double LastDurationMs { get; internal set; }
        public string LastException { get; internal set; }
        public bool IsExecuting { get; internal set; }
        public long LastTreeRunId { get; internal set; }
        internal object TreeId { get; set; }
    }
}
