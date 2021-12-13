using System;
using UnityEngine;

namespace Easy.Pooling
{
    public class GameObjectPool<T> : Pool<T> where T : MonoBehaviour
    {
        public GameObjectPool(T objectToPool, Action<T> reset, int limit) : base(() => GameObject.Instantiate<T>(objectToPool), (T hidrate) => { reset?.Invoke(hidrate); hidrate.gameObject.SetActive(true); return hidrate; }, (T snooz) => { snooz.gameObject.SetActive(false); return snooz; }, (T destroy) => GameObject.Destroy(destroy.gameObject), limit)
        {
            snoozeMethod.Invoke(objectToPool);
        }
    }
}
