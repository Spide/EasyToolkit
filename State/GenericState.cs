using System;
using System.Collections.Generic;
using System.Text;

namespace Easy.State
{
    public class GenericState<K> : IState<K>
    {
        protected Dictionary<K, object> data;

        public event Action<K, object> OnChange;


        public GenericState()
        {
            data = new Dictionary<K, object>();
        }
        public GenericState(Dictionary<K, object> from)
        {
            data = from;
        }

        public int Get(K resource)
        {
            return Get<int>(resource);
        }

        public R Get<R>(K resource)
        {
            try
            {
                if (data.TryGetValue(resource, out object value))
                    return (R)value;
                else
                    return default;
            }
            catch (InvalidCastException)
            {
                UnityEngine.Debug.LogFormat("Resource {0} cannot be casted to {1} current value is {2} ", resource, typeof(R), data[resource]);
                throw;
            }
        }

        public bool Has(K resource)
        {
            return data.ContainsKey(resource);
        }

        public int Add(K resource, int value)
        {
            int cvalue = Get(resource);
            Set(resource, cvalue + value);

            return value;
        }

        public void Set(K resource, object value)
        {
            data[resource] = value;

            if (OnChange != null)
                OnChange.Invoke(resource, value);
        }

        public ICollection<K> Keys
        {
            get => data.Keys;
        }


        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var item in data)
            {

                result.Append("[");
                result.Append(item.Key);
                result.Append("] = ");
                result.Append(item.Value.ToString());
                if (item.Value.GetType().IsArray)
                {
                    result.Append("{");

                    var array = (object[])item.Value;
                    for (int i = 0; i < array.Length; i++)
                    {
                        result.Append(array[i].ToString());
                        if (i != array.Length)
                            result.Append(" | ");
                    }
                    result.Append("}");
                }
                result.Append(" \n ");
            }
            return result.ToString();
        }


    }
}