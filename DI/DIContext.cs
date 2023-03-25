using System;
using System.Collections.Generic;
using Easy.Logging;
using UnityEngine;

namespace Easy.DI
{
    public class DIContext
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(DIContext), Color.Lerp(Color.yellow, Color.blue, 0.5f));

        private static readonly Dictionary<string, DIContainer> containers = new Dictionary<string, DIContainer>();

        public static event Action<String> OnContextBinded;

        public static DIContainer CreateContainer(string name, params string[] parents)
        {
            var parentContainers = Array.ConvertAll(parents, containerName =>
            {
                if (!containers.TryGetValue(containerName, out DIContainer container))
                    throw new Exception(string.Format("container with name: \"{0}\" does not exist ", containerName));

                return container;
            });

            var container = new DIContainer(name, parentContainers);
            try
            {
                containers.Add(container.Name, container);
            }
            catch (System.ArgumentException e)
            {
                LOGGER.LogError("Container with name \"{0}\" already exist. You may loaded same scene twice \n \"{1}\" ", container.Name, e);
            }

            LOGGER.Log("Container \"{0}\" created", container.Name);
            return container;
        }

        public static void Bind(string containerName, object instance, string name = null)
        {
            if (!containers.TryGetValue(containerName, out DIContainer container))
                throw new System.Exception("Container does not exists");

            container.Bind(instance, name);
        }

        public static T Resolve<T>(string containerName, string byName = null)
        {
            if (containerName == null)
            {
                foreach (var container in containers.Values)
                {
                    try
                    {
                        return container.Resolve<T>(byName);
                    }
                    catch (System.Exception e)
                    {
                        // global resolve should not log warnings
                        LOGGER.Log("cannot resolve \"{0}\"", e.Message);
                        continue;
                    }
                }

                throw new Exception(string.Format("Cannot resolve by type: \"{0}\" or by name: \"{1}\"", typeof(T), byName));
            }
            else
            {
                if (!containers.ContainsKey(containerName))
                    throw new System.Exception("Container \""+containerName+"\" does not exists");

                return containers[containerName].Resolve<T>(byName);
            }
        }

        public static void Clear(string container)
        {
            if (!containers.ContainsKey(container))
                throw new System.Exception(string.Format("Container \"{0}\" does not exists", container));

            containers[container].Clear();

            containers.Remove(container);
        }

        public static void ContextBinded(String sceneContext)
        {
            OnContextBinded?.Invoke(sceneContext);
        }
    }
}