using System;

namespace Easy.Save
{
    [Serializable]
    public class VersionedWrapper
    {
        public string typeId;
        public int version;
        public string payload;
    }
}
