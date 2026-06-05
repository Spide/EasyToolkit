using System.Globalization;

namespace Easy.Save.Serialization
{
    public sealed class IntSerializer : ISerializer<int>
    {
        public string Serialize(int value) => value.ToString(CultureInfo.InvariantCulture);
        public int Deserialize(string json) => int.Parse(json, CultureInfo.InvariantCulture);
    }

    public sealed class FloatSerializer : ISerializer<float>
    {
        public string Serialize(float value) => value.ToString(CultureInfo.InvariantCulture);
        public float Deserialize(string json) => float.Parse(json, CultureInfo.InvariantCulture);
    }

    public sealed class BoolSerializer : ISerializer<bool>
    {
        public string Serialize(bool value) => value ? "true" : "false";
        public bool Deserialize(string json) => bool.Parse(json);
    }

    public sealed class StringSerializer : ISerializer<string>
    {
        public string Serialize(string value) => value ?? string.Empty;
        public string Deserialize(string json) => json;
    }
}
