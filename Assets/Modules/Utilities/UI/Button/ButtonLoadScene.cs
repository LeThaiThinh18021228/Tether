using Framework;
using UnityEngine;
namespace Utilities
{
    public class ButtonLoadScene : ButtonBase
    {
        [SerializeField] ESceneName eSceneValue;
        [SerializeField] GameObject loadingPopup;

        protected override void Button_OnClicked()
        {
            base.Button_OnClicked();
            SceneController.Instance.Load(eSceneValue, loadingPopup);
        }
    }
}