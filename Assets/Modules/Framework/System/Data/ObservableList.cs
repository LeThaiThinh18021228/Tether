using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    [System.Serializable]
    public class ObservableList<T>
    {
        [SerializeField] IList<T> _value;
        public IList<T> Value
        {
            get { return _value; }
            set { _value = value;}
        }

        public Callback<T,int, Operation> OnChanged;
        public Callback OnClear;

        public ObservableList(IList<T> defaultValue)
        {
            _value = defaultValue;
        }

        public void Add(T data)
        {
            _value.Add(data);
            OnChanged?.Invoke(data, _value.Count, Operation.Add);
        }
 
        public void RemoveAll(Predicate<T> predicate)
        {
            foreach (var data in _value)
            {
                int index = _value.IndexOf(data);
                if (predicate(data) && _value.Remove(data))
                {
                    OnChanged?.Invoke(data, index, Operation.Remove);
                }
            }
        }
        public void Remove(T data)
        {
            _value.Remove(data);
            int index = _value.IndexOf(data);
            OnChanged?.Invoke(data, index, Operation.Remove);
        }

        public void Clear()
        {
            _value.Clear();
            OnClear?.Invoke();
        }
    }
}