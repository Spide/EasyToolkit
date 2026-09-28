using System;
using System.Collections.Generic;
using System.IO;
using Easy.Save.Migrations;
using Easy.Save.Serialization;
using UnityEngine;

namespace Easy.Save
{
    public static class SaveSystem
    {
        private static readonly string defaultBasePath = Path.Combine(
            Application.persistentDataPath,
            "saves"
        );
        private static readonly Dictionary<Type, ISerializer> serializers = new();
        private static readonly Dictionary<Type, object> migrators = new();
        private static readonly Dictionary<string, SaveTypeRegistration> saveTypesById = new();
        private static readonly Dictionary<Type, SaveTypeRegistration> saveTypesByType = new();
        private static string basePath = defaultBasePath;

        public static string BasePath => basePath;

        public static void Setup()
        {
            serializers.Clear();
            migrators.Clear();
            saveTypesById.Clear();
            saveTypesByType.Clear();

            Register(new IntSerializer());
            Register(new FloatSerializer());
            Register(new BoolSerializer());
            Register(new StringSerializer());
        }

        public static void SetBasePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Save path cannot be empty.", nameof(path));

            basePath = path;
        }

        public static void ResetBasePath()
        {
            basePath = defaultBasePath;
        }

        public static void Register<T>(ISerializer<T> serializer, IMigration<T> migrator = null)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            serializers[typeof(T)] = serializer;
            if (migrator != null)
                migrators[typeof(T)] = migrator;
        }

        public static void RegisterSaveType<T>(
            ISerializer<T> serializer = null,
            IMigration<T> migrator = null
        )
        {
            var attribute = GetSaveTypeAttribute(typeof(T));

            if (serializer != null)
                Register(serializer, migrator);
            else if (!TryGetTyped<T>(out serializer))
                throw new Exception($"Missing serializer for type {typeof(T)}");

            if (migrator == null && migrators.TryGetValue(typeof(T), out var rawMigrator))
                migrator = (IMigration<T>)rawMigrator;

            int latestVersion = migrator?.LatestVersion ?? attribute.Version;
            if (latestVersion != attribute.Version)
            {
                throw new InvalidOperationException(
                    $"Save type {attribute.SaveTypeId} declares version {attribute.Version}, but its migrator latest version is {latestVersion}."
                );
            }

            var registration = new SaveTypeRegistration(
                attribute.SaveTypeId,
                typeof(T),
                attribute.Version,
                value => serializer.Serialize((T)value),
                (json, version) =>
                    migrator != null ? migrator.FromJson(json, version) : serializer.Deserialize(json)
            );

            if (saveTypesById.TryGetValue(attribute.SaveTypeId, out var existingById)
                && existingById.DataType != typeof(T))
            {
                throw new InvalidOperationException(
                    $"Save type id '{attribute.SaveTypeId}' is already registered for {existingById.DataType}."
                );
            }

            saveTypesById[attribute.SaveTypeId] = registration;
            saveTypesByType[typeof(T)] = registration;
        }

        public static bool TryResolveSaveType(string typeId, out Type dataType)
        {
            if (saveTypesById.TryGetValue(typeId, out var registration))
            {
                dataType = registration.DataType;
                return true;
            }

            dataType = null;
            return false;
        }

        public static bool TryGetTyped<T>(out ISerializer<T> serializer)
        {
            if (serializers.TryGetValue(typeof(T), out var raw) && raw is ISerializer<T> typed)
            {
                serializer = typed;
                return true;
            }

            serializer = new GenericJsonSerializer<T>();
            serializers[typeof(T)] = serializer;
            return true;
        }

        public static string GetPath(string key)
        {
            ValidateKey(key);
            return Path.Combine(basePath, key + ".json");
        }

        public static void Save<T>(string key, T data)
        {
            if (!TryGetTyped<T>(out var serializer))
                throw new Exception($"Missing serializer for type {typeof(T)}");

            saveTypesByType.TryGetValue(typeof(T), out var saveType);

            int version =
                saveType?.Version
                ?? (
                    migrators.TryGetValue(typeof(T), out var rawMig)
                        ? ((IMigration<T>)rawMig).LatestVersion
                        : 1
                );

            var wrapper = new VersionedWrapper
            {
                typeId = saveType?.TypeId,
                version = version,
                payload = serializer.Serialize(data),
            };

            string path = GetPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteAllTextSafely(path, JsonUtility.ToJson(wrapper));
        }

        public static T Load<T>(string key)
        {
            string path = GetPath(key);
            if (!File.Exists(path))
                return default;

            var wrapper = ReadWrapper(key, path);

            if (saveTypesByType.TryGetValue(typeof(T), out var saveType))
            {
                if (string.IsNullOrWhiteSpace(wrapper.typeId))
                    throw new InvalidDataException($"Save file '{key}' is missing save type id.");

                if (wrapper.typeId != saveType.TypeId)
                {
                    throw new InvalidDataException(
                        $"Save file '{key}' contains save type '{wrapper.typeId}', but {saveType.TypeId} was requested."
                    );
                }

                return (T)saveType.Deserialize(wrapper.payload, wrapper.version);
            }

            if (!TryGetTyped<T>(out var serializer))
                throw new Exception($"No serializer registered for {typeof(T)}");

            if (migrators.TryGetValue(typeof(T), out var rawMig))
                return ((IMigration<T>)rawMig).FromJson(wrapper.payload, wrapper.version);

            if (wrapper.version != 1)
                throw new Exception(
                    $"Cannot load {typeof(T)}: version {wrapper.version} requires migration."
                );

            return serializer.Deserialize(wrapper.payload);
        }

        public static object LoadRegistered(string key)
        {
            string path = GetPath(key);
            if (!File.Exists(path))
                return null;

            var wrapper = ReadWrapper(key, path);

            if (string.IsNullOrWhiteSpace(wrapper.typeId))
                throw new InvalidDataException($"Save file '{key}' is missing save type id.");

            if (!saveTypesById.TryGetValue(wrapper.typeId, out var saveType))
                throw new InvalidDataException($"No save type registered for '{wrapper.typeId}'.");

            return saveType.Deserialize(wrapper.payload, wrapper.version);
        }

        public static T LoadRegistered<T>(string key)
        {
            var loaded = LoadRegistered(key);
            if (loaded == null)
                return default;

            if (loaded is T typed)
                return typed;

            throw new InvalidDataException(
                $"Save file '{key}' resolved to {loaded.GetType()}, not requested type {typeof(T)}."
            );
        }

        public static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Save key cannot be empty.", nameof(key));

            if (Path.IsPathRooted(key))
                throw new ArgumentException("Save key must be relative.", nameof(key));

            string normalized = key.Replace('\\', '/');
            string[] segments = normalized.Split('/');
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                    throw new ArgumentException($"Save key '{key}' contains an empty path segment.", nameof(key));

                if (segment == "." || segment == "..")
                    throw new ArgumentException($"Save key '{key}' cannot contain relative path segments.", nameof(key));

                if (segment.IndexOfAny(invalidFileNameChars) >= 0)
                    throw new ArgumentException($"Save key '{key}' contains invalid file name characters.", nameof(key));
            }
        }

        private static VersionedWrapper ReadWrapper(string key, string path)
        {
            VersionedWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<VersionedWrapper>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"Save file '{key}' is not valid JSON.", exception);
            }

            if (wrapper == null || wrapper.version < 0 || wrapper.payload == null)
                throw new InvalidDataException($"Save file '{key}' is missing versioned payload data.");

            return wrapper;
        }

        private static void WriteAllTextSafely(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.tmp");

            File.WriteAllText(tempPath, content);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(path);
                File.Move(tempPath, path);
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private static SaveTypeAttribute GetSaveTypeAttribute(Type type)
        {
            var attribute = (SaveTypeAttribute)
                Attribute.GetCustomAttribute(type, typeof(SaveTypeAttribute));

            if (attribute == null)
                throw new InvalidOperationException($"{type} is missing SaveTypeAttribute.");

            return attribute;
        }

        private sealed class SaveTypeRegistration
        {
            private readonly Func<object, string> serialize;
            private readonly Func<string, int, object> deserialize;

            public SaveTypeRegistration(
                string typeId,
                Type dataType,
                int version,
                Func<object, string> serialize,
                Func<string, int, object> deserialize
            )
            {
                TypeId = typeId;
                DataType = dataType;
                Version = version;
                this.serialize = serialize;
                this.deserialize = deserialize;
            }

            public string TypeId { get; }
            public Type DataType { get; }
            public int Version { get; }
            public string Serialize(object value) => serialize(value);
            public object Deserialize(string json, int version) => deserialize(json, version);
        }
    }
}
