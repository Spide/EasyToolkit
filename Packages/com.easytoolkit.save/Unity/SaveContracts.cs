namespace Easy.Save.Unity
{
    public interface IWorldSaveContributor<TSave, TCaptureContext, TRestoreContext>
    {
        void Capture(TSave save, TCaptureContext context);
        void Restore(TSave save, TRestoreContext context);
    }

    public interface IWorldSaveScenePreparer<TSave>
    {
        void PrepareForWorldSaveCapture();
        void PrepareForWorldSaveRestore(TSave save);
    }

    public interface IWorldSaveLifecycleListener<TSave>
    {
        void BeforeWorldSaveCapture(TSave save);
        void AfterWorldSaveCapture(TSave save);
        void BeforeWorldSaveRestore(TSave save);
        void AfterWorldSaveRestore(TSave save);
    }

    public interface IRuntimeSaveObject
    {
        string SaveCollectionId { get; }
        string StableId { get; }
    }

    public interface IWorldSaveObjectReconstructor<TSave>
    {
        void ReconstructMissingSaveObjects(TSave save);
    }
}
