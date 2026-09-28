using System;
using System.Collections.Generic;
using System.Linq;
using Easy.Logging;
using UnityEngine;

namespace Easy.DI
{
    public class DIContext
    {
        private static readonly EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(DIContext), Color.Lerp(Color.yellow, Color.blue, 0.5f));

        private static readonly Dictionary<string, DIContainer> containers = new Dictionary<string, DIContainer>();

        public static event Action<string> OnContextBinded;

        public static DIContainer CreateContainer(string name, params string[] parents)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Container name cannot be null or empty", nameof(name));

            var parentContainers = Array.ConvertAll(parents ?? Array.Empty<string>(), containerName =>
            {
                if (!containers.TryGetValue(containerName, out DIContainer container))
                    throw new Exception($"container with name: '{containerName}' does not exist");

                return container;
            });

            var container = new DIContainer(name, parentContainers);
            if (containers.ContainsKey(container.Name))
                throw new InvalidOperationException($"Container with name '{container.Name}' already exists.");

            containers.Add(container.Name, container);
            LOGGER.Log("Container '{0}' created", container.Name);
            return container;
        }

        public static void Bind(string containerName, object instance, string name = null)
        {
            if (!containers.TryGetValue(containerName, out DIContainer container))
                throw new Exception($"Container '{containerName}' does not exist");

            container.Bind(instance, name);
        }

        public static bool TryResolve<T>(string containerName, out T result, string byName = null)
        {
            if (containerName == null)
            {
                foreach (var container in OrderedContainers())
                {
                    if (container.TryResolve(out result, byName))
                        return true;
                }

                result = default;
                return false;
            }

            if (!containers.TryGetValue(containerName, out DIContainer selected))
            {
                result = default;
                return false;
            }

            return selected.TryResolve(out result, byName);
        }

        public static T Resolve<T>(string containerName, string byName = null)
        {
            if (TryResolve(containerName, out T result, byName))
                return result;

            throw new Exception($"Cannot resolve '{typeof(T)}' from container '{containerName ?? "<global>"}' byName '{byName}'.");
        }

        public static void Clear(string container)
        {
            if (!containers.ContainsKey(container))
                throw new Exception($"Container '{container}' does not exists");

            containers[container].Clear();
            containers.Remove(container);
        }

        public static void ContextBinded(string sceneContext)
        {
            OnContextBinded?.Invoke(sceneContext);
        }

        private static IEnumerable<DIContainer> OrderedContainers()
        {
            if (containers.TryGetValue(MainContext.NAME, out DIContainer global))
                yield return global;

            foreach (var pair in containers.OrderBy(p => p.Key))
            {
                if (pair.Key == MainContext.NAME)
                    continue;

                yield return pair.Value;
            }
        }
    }
}
