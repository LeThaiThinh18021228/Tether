using Framework;
using System;
using UnityEngine;
namespace Utilities
{
    public class SceneController : SingletonMono<SceneController>
    {
        public SMSceneTransition SMTransition { get; private set; }
        public SMSceneBase SMScene { get; set; }

        public ESceneName NextScene;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            SMTransition = new SMSceneTransition(new SMSceneIdle());
        }

        void Update()
        {
            SMTransition.Update();
            SMScene.Update();
        }

        #endregion

        #region Public
        public void Load(ESceneName eSceneValue, GameObject loadingPopup = null, Action<ESceneName> onLoaded = null)
        {
            if (SMTransition.State is not SMSceneIdle)
            {
                PDebug.Log("[{0}, {1}] A scene is loading, can't execute load scene command!", SMScene.State.GetType(), SMTransition.State.GetType());
                return;
            }
            SMTransition.OnLoaded += onLoaded;
            if (loadingPopup) SMTransition.LoadingPopup = loadingPopup.GetComponent<PopupBehaviour>();
            NextScene = eSceneValue;
            SMTransition.ChangeState(new SMSceneFadeIn());
        }
        public void Reload(GameObject loadingPopup = null)
        {
            Load(SMScene.CurScene, loadingPopup);
        }

        public void Construct()
        {
            // Construct state machine

            //_fadein += ()=> gameObject.SetChildrenRecursively<Image>((img) => { img.DOFade(1, SceneTransitionConfigSO.FadeInDuration); });
            //_fadeout += ()=> GetComponent<Image>().DOFade(0,SceneTransitionConfigSO.FadeOutDuration);
        }

        #endregion
    }
}