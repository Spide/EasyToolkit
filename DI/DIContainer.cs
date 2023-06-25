using System;
using System.Collections.Generic;
using System.Reflection;
using Easy.Logging;
using UnityEngine;

namespace Easy.DI
{
    public class DIContainer
    {
        protected static EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(DIContainer), Color.Lerp(Color.gray, Color.blue, 0.5f), "[DIContainer]");

        private readonly Dictionary<string, object> container = new Dictionary<string, object>();

        private readonly DIContainer[] parentContainers;

        public DIContainer(string name, params DIContainer[] containers)
        {
            Name = name;
            parentContainers = containers;
        }

        public string Name { get; }

        public void Bind(object instance, string name = null)
        {
            container.Add(name ?? instance.GetType().ToString(), instance);
        }
        public void Bind<T>(string name = null)
        {
            try
            {
                ConstructorInfo[] ctors = typeof(T).GetConstructors();
                var ctor = ctors[0];
                var parameters = Array.ConvertAll(ctor.GetParameters(), (pa) =>
                {
                    var attr = pa.GetCustomAttribute<ResolveBy>();
                    return attr != null ? Resolve<object>(attr.Name) : Resolve<object>(pa.ParameterType);
                });

                object instance = ctor.Invoke(parameters);

                Bind(instance, name);
            }
            catch (System.Exception e)
            {
                LOGGER.LogError("Cannot resolve type {0} for {1} ", typeof(T) , e);
            }
        }

        public T Resolve<T>(string byName = null)
        {
            if (byName != null)
            {
                if (container.TryGetValue(byName, out object result))
                    return (T)result;

                foreach (var container in parentContainers)
                {
                    try
                    {
                        return container.Resolve<T>(byName);
                    }
                    catch (System.Exception e)
                    {
                        // parent resolve should not log errors
                        LOGGER.Log("parent resolve \"{0}\" ", e);
                    }
                }

                throw new Exception(string.Format("Cannot resolve by name: \"{0}\" ", byName));
            }
            else
            {
                Type type = typeof(T);
                try
                {
                    return Resolve<T>(type);
                }
                catch (System.Exception e)
                {
                    // parent resolve should not log errors
                    LOGGER.LogFormat("cannot resolve  \"{0}\"", e);
                }

                throw new Exception(string.Format("Cannot resolve by type: {0} or by name: {1}", type, byName));
            }
        }

        public T Resolve<T>(Type type)
        {
            foreach (var item in container.Values)
            {
                if (item.GetType() == type || type.IsAssignableFrom(item.GetType()))
                    return (T)item;
            }

            foreach (var container in parentContainers)
            {
                try
                {
                    return container.Resolve<T>(type);
                }
                catch (System.Exception e)
                {
                    // parent resolve should not log errors
                    LOGGER.Log("cannot resolve from parent \"{0}\" - \"{1}\"", container.Name, type);
                }
            }

            throw new Exception(string.Format("Cannot resolve by type: \"{0}\"", type));
        }

        public void Clear()
        {
            container.Clear();
        }
    }
}