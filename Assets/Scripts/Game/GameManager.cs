using Bot;
using Framework.FishNet;
using MasterServerToolkit.MasterServer;
using UnityEngine;

public class GameManager : SingletonNetwork<GameManager>
{
    public MapManager MapManager;
    public CurrencyGenerator CurrencyGenerator;
    public BotManager BotManager;
    public GameObject WardRoot;
    public RoomServerManager RoomServerManager;
    public override void OnStartServer()
    {
        base.OnStartServer();
        RoomServerManager = ServerManager.GetComponent<RoomServerManager>();
    }
}
