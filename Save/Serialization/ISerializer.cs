namespace Easy.Save.Serialization
{
    public interface ISerializer
    {
    }

    public interface ISerializer<T> : ISerializer
    {
        string Serialize(T value);
        T Deserialize(string json);
    }
}
