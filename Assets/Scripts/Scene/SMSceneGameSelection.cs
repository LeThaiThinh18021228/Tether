using Framework;
namespace Utilities
{
    public class SMSceneGameSelection : IState<SMSceneBase>
    {
        public SMSceneBase Context { get => SceneController.Instance.SMScene; set { } }

        public void OnStart()
        {
        }

        public void OnStop()
        {
        }

        public void OnUpdate()
        {
        }

    }

}