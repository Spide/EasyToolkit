using System.Collections.Generic;

namespace Easy.State
{
    public class ObservableState : ObservableState<string> {}
    public class ObservableState<K> : GenericState<K>
    {
        public GenrericStateListener<K> Listener { get; }

        public ObservableState() : base()
        {
            Listener = new GenrericStateListener<K>(this);
        }
    }
}