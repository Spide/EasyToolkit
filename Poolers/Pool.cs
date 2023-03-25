using System;
using System.Collections.Generic;
namespace Easy.Pooling
{
    public class Pool<T> : IPool<T>
    {
        protected Stack<T> pool;

        protected Func<T> factoryMethod;
        protected Func<T, T> hidrateMethod;
        protected Func<T, T> snoozeMethod;
        protected Action<T> destroyMethod;

        protected int limit;

        public Pool(Func<T> factoryMethod, int limit)
        {
            pool = new Stack<T>(limit);
            this.factoryMethod = factoryMethod;
            this.limit = limit;
        }

        public Pool(IFactory<T> factory, int limit) : this(factory.Create, limit)
        {
        }

        public Pool(Func<T> factoryMethod, Func<T, T> hidrateMethod, Func<T, T> snoozeMethod, Action<T> destroyMethod, int limit) : this(factoryMethod, limit)
        {
            this.hidrateMethod = hidrateMethod;
            this.snoozeMethod = snoozeMethod;
            this.destroyMethod = destroyMethod;
        }

        public virtual void Clear()
        {
            if (destroyMethod != null)
            {
                foreach (var item in pool)
                {
                    destroyMethod(item);
                }
            }

            pool.Clear();
        }

        public T Get()
        {
            if (pool.Count > 0)
            {
                return hidrateMethod != null ? hidrateMethod.Invoke(pool.Pop()) : pool.Pop();
            }
            else
            {
                return hidrateMethod != null ? hidrateMethod.Invoke(factoryMethod.Invoke()) : factoryMethod.Invoke();
            }
        }

        public T[] Get(int count)
        {
            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Get();
            }
            return result;
        }

        public void Push(params T[] items)
        {
            Array.ForEach(items, Push);
        }

        protected void Push(T item)
        {
            if (pool.Count < limit)
            {
                pool.Push(snoozeMethod != null ? snoozeMethod.Invoke(item) : item);
            } else
            {
                destroyMethod?.Invoke(item);
            }
        }
    }
}