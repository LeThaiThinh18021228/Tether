using Bot;
using FishNet;
using Framework.HSPDIMAlgo;
using MasterServerToolkit.MasterServer;
using UnityEngine;

public class GameManager : GameManagerBase<GameManager>
{
    public HSPDIM HSPDIM;
    public MapManager MapManager;
    public BotManager BotManager;
    public CurrencyGenerator CurrencyGenerator;
    public GameObject WardRoot;

    [SerializeField] protected RoomServerManager roomServerManager;
    public RoomServerManager RoomServerManager
    {
        get
        {
            if (!roomServerManager) Instance.roomServerManager = InstanceFinder.ServerManager.GetComponent<RoomServerManager>();
            return Instance.roomServerManager;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GameStartCondition = (a) => BotManager.isSpawnBotCompleted;
        GameManager.Instance.State.OnChange += HSPDIM.OnGameStart;
    }
}
