namespace Easy.Pooling
{
    public interface IFactory<T>
    {
        T Create();
    }
}