using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.State
{
    [Serializable]
    public class SerializableState<T> : GenericState<T>, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private readonly List<T> keyData = new List<T>();

        [SerializeField, HideInInspector]
        private readonly List<object> valueData = new List<object>();

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            this.Clear();
            for (int i = 0; i < this.keyData.Count && i < this.valueData.Count; i++)
            {
                this[this.keyData[i]] = this.valueData[i];
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            this.keyData.Clear();
            this.valueData.Clear();

            foreach (var item in this)
            {
                this.keyData.Add(item.Key);
                this.valueData.Add(item.Value);
            }
        }

        public interface ISerializableStateEntity
        {
            T GetEntityKey  {get;}
            object GetEntityData {get;}
        }
    }

}