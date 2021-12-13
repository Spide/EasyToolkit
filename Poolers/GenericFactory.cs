using System;
namespace Easy.Pooling
{
    public abstract class GenericFactory<T> : IFactory<T>
    {
        protected Func<T> FactoryMethod { get; }
        protected GenericFactory(Func<T> factoryMethod)
        {
            this.FactoryMethod = factoryMethod;
        }

        public T Create()
        {
            return FactoryMethod.Invoke();
        }

    }
}