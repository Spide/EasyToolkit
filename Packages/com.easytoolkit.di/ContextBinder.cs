using UnityEngine;

namespace Easy.DI
{
    public abstract class ContextBinder : MonoBehaviour
    {
        public abstract void Bind(DIContainer container);
    }

}

