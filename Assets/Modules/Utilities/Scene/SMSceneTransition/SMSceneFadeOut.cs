using DG.Tweening;
using Framework;
namespace Utilities
{
    public class SMSceneFadeOut : IState<SMSceneTransition>
    {
        public SMSceneTransition Context { get => SceneController.Instance.SMTransition; set { } }

        public void OnStart()
        {
            //Play fade out tween
            //FadeOut();
            float duration = 0;
            if (Context.LoadingPopup)
            {
                Context.LoadingPopup.Close();
                duration += Context.LoadingPopup.CloseDuration;
            }
            //Wait until animation is end
            Context.Tween?.Kill();
            Context.Tween = DOVirtual.DelayedCall(duration, () =>
            {
                SceneController.Instance.SMTransition.ChangeState(new SMSceneIdle());
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