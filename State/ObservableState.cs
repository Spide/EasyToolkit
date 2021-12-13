using System.Collections.Generic;

namespace Easy.State
{
    public class ObservableState<K> : GenericState<K>
    {
        public GenrericStateListener<K> Listener { get; }

        public ObservableState() : base()
        {
            Listener = new GenrericStateListener<K>(this);
        }
        public ObservableState(Dictionary<K, object> from = null) : base(from)
        {
            Listener = new GenrericStateListener<K>(this);
        }
    }
}