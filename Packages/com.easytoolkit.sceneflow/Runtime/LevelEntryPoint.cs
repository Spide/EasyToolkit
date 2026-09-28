using UnityEngine;

namespace Easy.SceneFlow
{
    /// <summary>Named arrival pose inside a level. Every level needs a "default" entry.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelEntryPoint : MonoBehaviour
    {
        [SerializeField]
        private string entryPointId = LevelLoadRequest.DefaultEntryPointId;

        public string EntryPointId => entryPointId;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        public void Configure(string id)
        {
            entryPointId = id;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Vector3 position = transform.position;
            Gizmos.DrawWireSphere(position + Vector3.up * 0.1f, 0.35f);
            Gizmos.DrawLine(position + Vector3.up * 0.1f, position + Vector3.up * 0.1f + transform.forward);
        }
    }
}
