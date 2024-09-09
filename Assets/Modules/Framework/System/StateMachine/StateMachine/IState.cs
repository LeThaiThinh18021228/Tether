namespace Framework
{
    public interface IState<T> where T : StateMachineBase<T>
    {
        T Context { get; set; }
        void OnStart();
        void OnUpdate();
        void OnStop();
    }
}