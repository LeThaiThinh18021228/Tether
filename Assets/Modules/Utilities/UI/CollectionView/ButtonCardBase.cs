using UnityEngine;
using UnityEngine.UI;

namespace Utilities
{
    [RequireComponent(typeof(Button))]
    public abstract class ButtonCardBase<T> : CardBase<T> where T : IDataUnit<T>
    {
        [SerializeField] protected Button button;

        protected void Awake()
        {
            button.onClick.AddListener(Card_OnClicked);
        }
        protected abstract void Card_OnClicked();
        protected void OnDestroy()
        {
            button.onClick.RemoveListener(Card_OnClicked);

        }
    }
}
