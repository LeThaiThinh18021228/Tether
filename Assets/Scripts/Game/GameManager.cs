using Bot;
using FishNet;
using Framework.HSPDIMAlgo;
using HSPDIMAlgo;
using MasterServerToolkit.MasterServer;
using UnityEngine;

public class GameManager : GameManagerBase<GameManager>
{
    public HSPDIM HSPDIM;
    public MapManager MapManager;
    public CurrencyGenerator CurrencyGenerator;
    public BotManager BotManager;
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
        Instance.GameStartCondition += (x) => { return BotManager.isSpawnBotCompleted; };
        GameManager.Instance.State.OnChange += HSPDIM.InitMappingAndMatching;
    }
}
