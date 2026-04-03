using UnityEngine;

namespace Easy.DI
{
    public class BindMe : MonoBehaviour
    {
        public string As;
        public string contextName;

        private void Awake()
        {
            var targetContext = string.IsNullOrWhiteSpace(contextName) ? gameObject.scene.name : contextName;
            DIContext.Bind(targetContext, gameObject, As);
        }
    }
}
