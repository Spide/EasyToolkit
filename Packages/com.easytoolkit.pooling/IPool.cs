namespace Easy.Pooling
{
    public interface IPool<T>
    {
        T Get();
        T[] Get(int count);
        void Push(params T[] items);
        void Clear();

    }
}