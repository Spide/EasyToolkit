using System;
using System.Collections.Generic;

namespace Easy.Save.Migrations
{
    public interface IMigration<T>
    {
        int LatestVersion { get; }
        T FromJson(string json, int version);
    }

    public class Migration<T> : IMigration<T>
    {
        private readonly Dictionary<int, Func<string, T>> migrations = new();

        public int LatestVersion { get; private set; }

        public void Register(int version, Func<string, T> migrate)
        {
            migrations[version] = migrate ?? throw new ArgumentNullException(nameof(migrate));
            LatestVersion = Math.Max(LatestVersion, version);
        }

        public T FromJson(string json, int version)
        {
            if (version > LatestVersion)
                throw new Exception($"Cannot load {typeof(T)}: save version {version} is newer than supported version {LatestVersion}.");

            if (!migrations.TryGetValue(version, out var migrate))
                throw new Exception($"Missing migration for {typeof(T)} save version {version}.");

            return migrate(json);
        }
    }
}
