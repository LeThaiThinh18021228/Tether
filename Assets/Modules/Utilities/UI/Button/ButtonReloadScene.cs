using Framework;
using UnityEngine;
namespace Utilities
{
    public class ButtonReloadScene : ButtonBase
    {
        [SerializeField] GameObject loadingUI;
        protected override void Button_OnClicked()
        {
            base.Button_OnClicked();
            SceneController.Instance.Reload(loadingUI);
        }
    }
}