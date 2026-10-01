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

        /// <summary>
        /// Adds <paramref name="value"/> to the numeric value stored under
        /// <paramref name="resource"/> and stores the sum as a <c>float</c>.
        /// A missing key counts as 0. A stored numeric value of any built-in
        /// numeric type (int, float, double, long, ...) is converted to float
        /// first. Any other stored type throws <see cref="InvalidCastException"/>
        /// (logged, like <see cref="Get{R}(K)"/>) and leaves the state unchanged.
        /// Because the sum is stored as float, a later <c>Get(key)</c> (int) on that
        /// key throws; use <c>Get&lt;float&gt;(key)</c>. Raises <see cref="OnChange"/>.
        /// </summary>
        /// <returns>The new total stored under the key.</returns>
        public float Plus(K resource, float value)
        {
            float current = 0f;
            if (TryGetValue(resource, out object stored))
            {
                if (!TryConvertToFloat(stored, out current))
                {
                    UnityEngine.Debug.LogFormat("Resource {0} cannot be casted to {1} current value is {2} ", resource, typeof(float), stored);
                    throw new InvalidCastException(
                        "Resource " + resource + " holds " + (stored == null ? "null" : stored.GetType().Name) + ", not a number.");
                }
            }

            float total = current + value;
            Set(resource, total);
            return total;
        }

        private static bool TryConvertToFloat(object stored, out float result)
        {
            switch (stored)
            {
                case float f: result = f; return true;
                case int i: result = i; return true;
                case double d: result = (float)d; return true;
                case long l: result = l; return true;
                case short sh: result = sh; return true;
                case byte b: result = b; return true;
                case sbyte sb: result = sb; return true;
                case ushort us: result = us; return true;
                case uint ui: result = ui; return true;
                case ulong ul: result = ul; return true;
                case decimal m: result = (float)m; return true;
                default: result = 0f; return false;
            }
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