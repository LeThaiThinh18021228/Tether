using UnityEngine;

namespace Framework
{
    [System.Serializable]
    public class ObservableData<T>
    {
        [SerializeField] T _value;

        public T Value
        {
            get { return _value; }
            set
            {
                if (!_value.Equals(value))
                {
                    _value = value;
                    OnChanged?.Invoke(value);
                }
            }
        }

        public event Callback<T> OnChanged;

        public ObservableData(T defaultValue)
        {
            _value = defaultValue;
        }
    }
}