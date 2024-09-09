using Framework;

namespace Utilities
{
    public class SMSceneHome : IState<SMSceneBase>
    {
        public SMSceneBase Context { get => SceneController.Instance.SMScene; set { } }

        public void Init()
        {
        }

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
