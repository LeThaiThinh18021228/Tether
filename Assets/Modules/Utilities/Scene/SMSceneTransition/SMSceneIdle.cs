using Framework;
namespace Utilities
{
    public class SMSceneIdle : IState<SMSceneTransition>
    {
        public SMSceneTransition Context { get => SceneController.Instance.SMTransition; set { } }

        public void OnStart()
        {
            Context.OnLoaded?.Invoke(SceneController.Instance.SMScene.CurScene);
        }

        public void OnStop()
        {
        }

        public void OnUpdate()
        {
        }

    }

}