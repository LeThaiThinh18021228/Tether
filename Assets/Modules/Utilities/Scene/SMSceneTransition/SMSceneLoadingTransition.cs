using Framework;

namespace Utilities
{
    public class SMSceneLoadingTransition : IState<SMSceneTransition>
    {
        public SMSceneTransition Context { get => SceneController.Instance.SMTransition; set { } }

        public void OnStart()
        {
        }

        public void OnStop()
        {
        }

        public void OnUpdate()
        {
            if (Context.SceneAsync.isDone)
            {
                Context.ChangeState(new SMSceneFadeOut());
                SceneController.Instance.SMScene.ChangeState(SceneController.Instance.NextScene);
            }
        }
    }
}
