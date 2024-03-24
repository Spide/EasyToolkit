using System;
using UnityEngine;

namespace Easy.DI
{
    public class MonoBehaviourContextBinder : ContextBinder
    {
        public MonoBehaviour[] bind;
        public override void Bind(DIContainer container)
        {
            Array.ForEach(bind, item => container.Bind(item));
        }
    }

}

