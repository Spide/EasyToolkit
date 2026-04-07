using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Easy.Logging;
using UnityEngine;

namespace Easy.DI
{
    public class DIContainer
    {
        protected static EasyLogger LOGGER = LoggerFactory.GetLogger(typeof(DIContainer), Color.Lerp(Color.gray, Color.blue, 0.5f));

        private readonly Dictionary<string, object> namedBindings = new Dictionary<string, object>();
        private readonly Dictionary<Type, object> typedBindings = new Dictionary<Type, object>();

        private readonly DIContainer[] parentContainers;

        public DIContainer(string name, params DIContainer[] containers)
        {
            Name = name;
            parentContainers = containers ?? Array.Empty<DIContainer>();
        }

        public string Name { get; }

        public void Bind(object instance, string name = null)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var key = name ?? instance.GetType().FullName;
            if (key == null)
                throw new InvalidOperationException($"Cannot generate key for type {instance.GetType()}");

            if (namedBindings.ContainsKey(key))
                throw new InvalidOperationException($"Binding with key '{key}' already exists in container '{Name}'.");

            namedBindings[key] = instance;
            RegisterTypeBinding(instance.GetType(), instance);
        }

        public void Bind<T>(string name = null)
        {
            var targetType = typeof(T);
            if (targetType.IsAbstract || targetType.IsInterface)
                throw new InvalidOperationException($"Cannot instantiate abstract/interface type '{targetType}'.");

            var ctor = SelectConstructor(targetType);
            if (ctor == null)
                throw new InvalidOperationException($"No suitable public constructor found for '{targetType}'.");

            object[] parameters = BuildConstructorParameters(ctor);
            object instance = ctor.Invoke(parameters);
            Bind(instance, name);
        }

        public void Bind<TService>(TService instance, string name = null) where TService : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Bind(instance as object, name);
            RegisterTypeBinding(typeof(TService), instance);
        }

        public bool TryResolve<T>(out T result, string byName = null)
        {
            if (byName != null)
                return TryResolveByName(out result, byName);

            return TryResolve(out result, typeof(T));
        }

        public T Resolve<T>(string byName = null)
        {
            if (TryResolve<T>(out var value, byName))
                return value;

            throw new Exception($"Cannot resolve type: '{typeof(T)}' byName: '{byName}' in container '{Name}'.");
        }

        public bool TryResolve<T>(out T result, Type type)
        {
            if (TryResolve(type, out object value))
            {
                result = (T)value;
                return true;
            }

            result = default;
            return false;
        }

        public T Resolve<T>(Type type)
        {
            if (TryResolve<T>(out var value, type))
                return value;

            throw new Exception(string.Format("Cannot resolve by type: '{0}' in container '{1}'", type, Name));
        }

        public bool TryResolve(Type type, out object result)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (TryResolveLocal(type, out result))
                return true;

            foreach (var container in parentContainers)
            {
                if (container.TryResolve(type, out result))
                    return true;
            }

            result = null;
            return false;
        }

        public void Clear()
        {
            namedBindings.Clear();
            typedBindings.Clear();
        }

        private bool TryResolveLocal(Type requestedType, out object result)
        {
            if (typedBindings.TryGetValue(requestedType, out result))
                return true;

            foreach (var pair in typedBindings)
            {
                if (requestedType.IsAssignableFrom(pair.Key))
                {
                    result = pair.Value;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private bool TryResolveByName<T>(out T result, string key)
        {
            if (namedBindings.TryGetValue(key, out var value))
            {
                result = (T)value;
                return true;
            }

            foreach (var container in parentContainers)
            {
                if (container.TryResolveByName(out result, key))
                    return true;
            }

            result = default;
            return false;
        }

        private void RegisterTypeBinding(Type type, object instance)
        {
            if (!typedBindings.ContainsKey(type))
                typedBindings[type] = instance;

            foreach (var item in type.GetInterfaces())
            {
                if (!typedBindings.ContainsKey(item))
                    typedBindings[item] = instance;
            }

            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (!typedBindings.ContainsKey(baseType))
                    typedBindings[baseType] = instance;
                baseType = baseType.BaseType;
            }
        }

        private ConstructorInfo SelectConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length == 0)
                return null;

            var attributed = constructors.Where(c => c.GetCustomAttribute<InjectAttribute>() != null).ToArray();
            if (attributed.Length > 1)
                throw new InvalidOperationException($"Type '{type}' has multiple constructors marked with [Inject].");

            if (attributed.Length == 1)
                return attributed[0];

            ConstructorInfo selected = null;
            var bestCount = -1;

            foreach (var ctor in constructors)
            {
                if (!CanBuild(ctor))
                    continue;

                var count = ctor.GetParameters().Length;
                if (count > bestCount)
                {
                    selected = ctor;
                    bestCount = count;
                }
            }

            return selected ?? constructors.OrderByDescending(c => c.GetParameters().Length).First();
        }

        private bool CanBuild(ConstructorInfo ctor)
        {
            foreach (var parameter in ctor.GetParameters())
            {
                var attr = parameter.GetCustomAttribute<ResolveBy>();
                var canResolve = attr != null
                    ? TryResolve(typeof(object), out _, attr.Name)
                    : TryResolve(parameter.ParameterType, out _);

                if (!canResolve && !parameter.IsOptional)
                    return false;
            }

            return true;
        }

        private object[] BuildConstructorParameters(ConstructorInfo ctor)
        {
            return Array.ConvertAll(ctor.GetParameters(), ResolveParameter);
        }

        private object ResolveParameter(ParameterInfo parameter)
        {
            var attr = parameter.GetCustomAttribute<ResolveBy>();
            if (attr != null)
            {
                if (TryResolve(typeof(object), out object namedValue, attr.Name))
                    return namedValue;

                if (parameter.IsOptional)
                    return parameter.DefaultValue;

                throw new InvalidOperationException($"Cannot resolve parameter '{parameter.Name}' by name '{attr.Name}'.");
            }

            if (TryResolve(parameter.ParameterType, out object value))
                return value;

            if (parameter.IsOptional)
                return parameter.DefaultValue;

            throw new InvalidOperationException($"Cannot resolve parameter '{parameter.Name}' of type '{parameter.ParameterType}'.");
        }

        private bool TryResolve(Type expectedType, out object result, string byName)
        {
            if (namedBindings.TryGetValue(byName, out var named))
            {
                if (expectedType.IsInstanceOfType(named) || expectedType == typeof(object))
                {
                    result = named;
                    return true;
                }
            }

            foreach (var container in parentContainers)
            {
                if (container.TryResolve(expectedType, out result, byName))
                    return true;
            }

            result = null;
            return false;
        }
    }
}
