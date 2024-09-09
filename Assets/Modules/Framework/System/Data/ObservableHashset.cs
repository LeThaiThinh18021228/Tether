using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    public enum Operation
    {
        Add,
        Modify,
        Remove,
        Clear
    }
    [System.Serializable]
    public class ObservableHashset<T>
    {
        [SerializeField] HashSet<T> _value;
        public HashSet<T> Value
        {
            get { return _value; }
            set { _value = value;}
        }

        public Callback<T, Operation> OnChanged;
        public Callback OnClear;

        public ObservableHashset(HashSet<T> defaultValue)
        {
            _value = defaultValue;
        }

        public void Add(T data)
        {
            _value.Add(data);
            OnChanged?.Invoke(data, Operation.Add);
        }
 
        public void RemoveAll(Predicate<T> predicate)
        {
            foreach (var data in _value)
            {
                if (predicate(data) && _value.Remove(data))
                {
                    OnChanged?.Invoke(data, Operation.Remove);
                }
            }
        }
        public void Remove(T data)
        {
            _value.Remove(data);
            OnChanged?.Invoke(data, Operation.Remove);
        }

        public void Clear()
        {
            _value.Clear();
            OnClear?.Invoke();
        }
    }
}