using UnityEngine;

namespace Easy.Save.Serialization
{
    public class GenericJsonSerializer<T> : ISerializer<T>
    {
        public string Serialize(T value) => JsonUtility.ToJson(value);
        public T Deserialize(string json) => JsonUtility.FromJson<T>(json);
    }
}
