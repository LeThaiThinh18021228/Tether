using FishNet;
using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Object;
using Framework;
using Framework.FishNet;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bot
{
    public class BotManager : NetworkBehaviour
    {
        public readonly List<BotPlayer> bots = new();
        int initBot;
        RoomServerManager roomServerManager;
        [SerializeField] GameObject botPrefab;
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!roomServerManager)
            {
                roomServerManager = InstanceFinder.ServerManager.GetComponent<RoomServerManager>();
            }
            roomServerManager.OnRoomRegisteredEvent.AddListener(OnRoomRegistered);
            roomServerManager.OnTerminatedRoom.AddListener(OnTerminatedRoom);
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            initBot = Mst.Args.AsInt(Mst.Args.Names.RoomBotNumner);

        }
        private void OnRoomRegistered(RoomController roomController)
        {
            initBot = roomController.Options.CustomOptions.AsInt(Mst.Args.Names.RoomBotNumner);
            for (int i = 0; i < initBot; i++)
            {
                SpawnBotObject(null);
                //SpawnClientBotProcess();
            }
        }
        private void OnTerminatedRoom(RoomController roomController)
        {
            
        }
        void SpawnClientBotProcess()
        {
            // Create process args string
            var processArguments = new MstProperties();
            processArguments.Set(Mst.Args.Names.IsBotClient, true);
            processArguments.Set(Mst.Args.Names.GameId, roomServerManager.RoomController.RoomId);
            ProcessManager.RunProcess(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName, "Builds/App/Win/ClientRoom/ClientRoom.exe"), processArguments.ToReadableString(" ", " "));
        }
        public void SpawnBotObject(NetworkConnection conn)
        {
            botPrefab.GetComponent<Player>().IsBot = true;
            var bot = botPrefab.InstantiateNetworked<BotPlayer>(conn);
            bots.Add(bot);
        }
    }
}

