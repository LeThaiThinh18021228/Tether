using DG.Tweening;
using Framework;
namespace Utilities
{
    public class SMSceneFadeIn : IState<SMSceneTransition>
    {
        public SMSceneTransition Context { get => SceneController.Instance.SMTransition; set { } }
        public void OnStart()
        {
            // Load scene async
            Context.SceneAsync = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(SceneController.Instance.NextScene.ToString());
            Context.SceneAsync.allowSceneActivation = false;

            //Play fade in tween
            //FadeIn();
            float duration = SceneTransitionConfigSO.LoadDuration;
            if (Context.LoadingPopup)
            {
                Context.LoadingPopup = Context.LoadingPopup.gameObject.Create(SceneController.Instance.transform).GetComponent<PopupBehaviour>();
                duration += Context.LoadingPopup.OpenDuration;
            }
            //Wait until animation is end
            Context.Tween?.Kill();
            Context.Tween = DOVirtual.DelayedCall(duration, () =>
            {
                SceneController.Instance.SMTransition.ChangeState(new SMSceneLoadingTransition());
                Context.SceneAsync.allowSceneActivation = true;
            }, true);
        }

        public void OnStop()
        {
        }

        public void OnUpdate()
        {
        }
    }
}