
using UnityEngine;
using Easy.Logging;
using Easy.Pooling;
using System;

namespace Easy
{
    public class EasyToolkitPoolExamples : MonoBehaviour
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(EasyToolkitPoolExamples), Color.red);

        void Start()
        {
            // create gameobject with monobehaviour to pool
            var gameobject = new GameObject("Projectile");
            var templateProjectile = gameobject.AddComponent<Projectile>();

            // create setup function that all object will use whent retrieved form pool 
            // no need to activate gameobject
            Action<Projectile> setupMethod = (Projectile p) => { p.Damage = UnityEngine.Random.Range(1, 10); };

            // create Pool of projectiles with max pool size (All pooled object over 5 wil be destroyed)
            // this will disable templateProjectile
            var pool = new GameObjectPool<Projectile>(templateProjectile, setupMethod , 5);

            // create 3 Instances of projectile with random damage
            // USE this instend of GameOjbect.Instantiate()
            Projectile p1 = pool.Get();
            Projectile p2 = pool.Get();
            Projectile p3 = pool.Get();

            LOGGER.Log("DAMAGE p1 {0} p2 {1} p3 {2}", p1.Damage, p2.Damage, p3.Damage);

            // this will disable all projectile gameobjects
            // USE this instend of GameOjbect.Destroy()
            pool.Push(p1, p2, p3);

            // theese three are reusing these previous three without new memory alocations but newly setted up
            p1 = pool.Get();
            p2 = pool.Get();
            p3 = pool.Get();

            LOGGER.Log("DAMAGE p1 {0} p2 {1} p3 {2}", p1.Damage, p2.Damage, p3.Damage);
        }

        public class Projectile : MonoBehaviour {
            public int Damage { get; set; }
        }
    }
}