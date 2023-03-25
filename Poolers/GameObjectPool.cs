using System;
using UnityEngine;

namespace Easy.Pooling
{
    public class GameObjectPool<T> : Pool<T> where T : Component
    {
        public GameObjectPool(T objectToPool, Action<T> reset, int limit) : base(
            () => GameObject.Instantiate<T>(objectToPool), 
            (T hidrate) => { reset?.Invoke(hidrate); hidrate.gameObject.SetActive(true); return hidrate; }, 
            (T snooz) => { snooz.gameObject.SetActive(false); return snooz; }, 
            (T destroy) => GameObject.Destroy(destroy.gameObject), 
            limit)
        {
            snoozeMethod.Invoke(objectToPool);
        }

        public GameObjectPool(T[] objectToPool, Action<T> reset, int limit) : base(
            () => GameObject.Instantiate<T>(objectToPool[UnityEngine.Random.Range(0, objectToPool.Length)]),
            (T hidrate) => {  reset?.Invoke(hidrate); hidrate.gameObject.SetActive(true); return hidrate; },
            (T snooz) => { snooz.gameObject.SetActive(false); return snooz; }, 
            (T destroy) => GameObject.Destroy(destroy.gameObject), 
            limit)
        {
           Array.ForEach(objectToPool, (i) => snoozeMethod.Invoke(i));
        }
    }
}
