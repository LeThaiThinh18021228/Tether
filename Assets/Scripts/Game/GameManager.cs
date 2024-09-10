using Bot;
using FishNet.Object;
using Framework.FishNet;
using MasterServerToolkit.Bridges.FishNetworking.Character;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : SingletonNetwork<GameManager>
{
    public MapManager MapManager;
    public CurrencyGenerator CurrencyGenerator;
    public BotManager BotManager;
    public RoomServerManager RoomServerManager;
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
}
