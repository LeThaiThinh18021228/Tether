using Framework;
using Utilities;

public class SMScene : SMSceneBase
{
#if (!UNITY_SERVER || GAME_SERVER)
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneController.Instance.SMScene = new SMScene(new SMSceneLoading());
    }
#endif
    public SMScene(IState<SMSceneBase> state) : base(state)
    {
    }

    public override void ChangeState(ESceneName scene)
    {
        CurScene = scene;
        switch (scene)
        {
            case ESceneName.Loading:
                ChangeState(new SMSceneLoading());
                break;
            case ESceneName.Auth:
                ChangeState(new SMSceneAuth());
                break;
            case ESceneName.Home:
                ChangeState(new SMSceneHome());
                break;
            case ESceneName.CoreIngame:
                ChangeState(new SMSceneCoreIngame());
                break;
            case ESceneName.GameSelection:
                ChangeState(new SMSceneGameSelection());
                break;
            default:
                break;
        }
    }
}
