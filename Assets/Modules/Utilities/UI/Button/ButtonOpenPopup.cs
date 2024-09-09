using Framework;
using UnityEngine;

namespace Utilities
{
    public class ButtonOpenPopup : ButtonBase
    {
        [SerializeField] GameObject _popup;

        public event Callback<PopupBehaviour> OnSpawnPopup;

        protected override void Button_OnClicked()
        {
            base.Button_OnClicked();

            if (_popup != null)
            {
                OpenPopup(_popup);
            }

        }

        protected virtual void HandleSpawnPopup(PopupBehaviour popupBehaviour)
        {

        }

        public void OpenPopup(GameObject popup)
        {
            PopupBehaviour _popup = PopupHelper.Create(popup);
            OnSpawnPopup?.Invoke(_popup);

            HandleSpawnPopup(_popup);
        }
    }
}