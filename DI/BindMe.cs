using UnityEngine;

namespace Easy.DI
{
    public class BindMe : MonoBehaviour
    {
        public string As;
        public string contextName;
        private void Awake()
        {
            DIContext.Bind(contextName != "" ? contextName : gameObject.scene.name, gameObject, As);
        }
    }
}