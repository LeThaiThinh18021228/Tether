namespace Framework
{
    public abstract class StateMachineBase<T> where T : StateMachineBase<T>
    {
        public IState<T> State { get; private set; }
        public StateMachineBase(IState<T> state)
        {
            this.State = state;
        }
        public virtual void ChangeState(IState<T> state)
        {
            this.State.OnStop();
            this.State = state;
            this.State.OnStart();
        }

        public void Update()
        {
            State.OnUpdate();
        }
    }
}
