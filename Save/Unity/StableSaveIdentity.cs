using System;
using UnityEngine;

namespace Easy.Save.Unity
{
    [DisallowMultipleComponent]
    public abstract class StableSaveIdentity : MonoBehaviour
    {
        [SerializeField]
        private string stableId;

        public string StableId
        {
            get
            {
                EnsureStableId();
                return stableId;
            }
        }

        protected virtual void Awake()
        {
            EnsureStableId();
        }

        public void EnsureStableId()
        {
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = Guid.NewGuid().ToString("N");
        }

        public void RestoreStableId(string savedStableId)
        {
            if (!string.IsNullOrWhiteSpace(savedStableId))
                stableId = savedStableId;
        }
    }
}
