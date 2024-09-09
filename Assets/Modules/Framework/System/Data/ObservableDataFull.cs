using UnityEngine;

namespace Framework
{
    [System.Serializable]
    public class ObservableDataFull<T>
    {
        [SerializeField] T _value;

        public T Value
        {
            get { return _value; }
            set
            {
                if (!_value.Equals(value))
                {
                    T oldValue = _value;
                    _value = value;
                    OnChanged?.Invoke(oldValue, value);
                }
            }
        }

        public Callback<T, T> OnChanged;

        public ObservableDataFull(T defaultValue)
        {
            _value = defaultValue;
        }
    }
}