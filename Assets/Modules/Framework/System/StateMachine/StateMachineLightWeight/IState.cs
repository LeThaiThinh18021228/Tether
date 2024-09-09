namespace Framework
{
    public interface IState
    {
        void Init();
        void OnStart();
        void OnUpdate();
        void OnStop();
    }
}