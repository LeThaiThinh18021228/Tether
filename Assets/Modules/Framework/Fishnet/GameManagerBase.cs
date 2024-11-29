using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework.FishNet;
using System;
public enum GameState
{
    NONSTARTED,
    STARTED,
}
public class GameManagerBase<T> : SingletonNetwork<T> where T : NetworkBehaviour
{
    public readonly SyncVar<GameState> State = new SyncVar<GameState>(GameState.NONSTARTED);
    public Predicate<bool> GameEndCondition;
    public Predicate<bool> GameStartCondition;

    private void Update()
    {
        if (!IsServerInitialized) return;
        if ((GameStartCondition?.Invoke(true)).GetValueOrDefault() && State.Value == GameState.NONSTARTED)
        {
            State.Value = GameState.STARTED;
        }
        if ((GameEndCondition?.Invoke(true)).GetValueOrDefault() && State.Value == GameState.STARTED)
        {
            State.Value = GameState.NONSTARTED;
        }
    }
}
