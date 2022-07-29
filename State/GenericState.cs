using System;
using System.Collections.Generic;
using System.Text;

namespace Easy.State
{
    public class GenericState : GenericState<string>, IState<string>{}
    public class GenericState<K> : Dictionary<K,object>, IState<K>
    {
        public event Action<K, object> OnChange;

        public int Get(K resource)
        {
            return Get<int>(resource);
        }

        public R Get<R>(K resource)
        {
            try
            {
                if (TryGetValue(resource, out object value))
                    return (R)value;
                else
                    return default;
            }
            catch (InvalidCastException)
            {
                UnityEngine.Debug.LogFormat("Resource {0} cannot be casted to {1} current value is {2} ", resource, typeof(R), this[resource]);
                throw;
            }
        }

        public bool Has(K resource)
        {
            return ContainsKey(resource);
        }

        public int Plus(K resource, int value)
        {
            int cvalue = Get(resource);
            Set(resource, cvalue + value);

            return value;
        }

        public void Set(K resource, object value)
        {
            this[resource] = value;

            OnChange?.Invoke(resource, value);
        }


        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var item in this)
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