using System;

namespace Easy.Save
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class SaveTypeAttribute : Attribute
    {
        public SaveTypeAttribute(string domain, string name, int version)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Save type domain cannot be empty.", nameof(domain));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Save type name cannot be empty.", nameof(name));

            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version), "Save type version must be positive.");

            Domain = domain;
            Name = name;
            Version = version;
        }

        public string Domain { get; }
        public string Name { get; }
        public int Version { get; }
        public string SaveTypeId => $"{Domain}.{Name}";
    }
}
