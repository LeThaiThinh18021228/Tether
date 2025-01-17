using DG.Tweening;
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
        [SerializeField] GameObject botPrefab;
        public bool isSpawnBotCompleted = false;
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            GameManager.Instance.RoomServerManager.OnRoomRegisteredEvent.AddListener(OnRoomRegistered);
            GameManager.Instance.RoomServerManager.OnTerminatedRoom.AddListener(OnTerminatedRoom);
        }
        public override void OnStartServer()
        {
            base.OnStartServer();

        }
        private void OnRoomRegistered(RoomController roomController)
        {
            initBot = GameManager.Instance.RoomServerManager.RoomController.Options.CustomOptions.AsInt(Mst.Args.Names.RoomBotNumner);
#if !UNITY_SERVER && UNITY_EDITOR
            if (IsServerInitialized)
            {
                initBot = 5;
            }
#endif
            for (int i = 0; i < initBot; i++)
            {
                SpawnBotObject(MapManager.RandomPositionInsideMap() / 2);
                //SpawnBotObject(new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)));
                //SpawnClientBotProcess();
            }
            DOVirtual.DelayedCall(5, () => { isSpawnBotCompleted = true; });

        }
        private void OnTerminatedRoom(RoomController roomController)
        {

        }
        void SpawnClientBotProcess()
        {
            // Create process args string
            var processArguments = new MstProperties();
            processArguments.Set(Mst.Args.Names.IsBotClient, true);
            processArguments.Set(Mst.Args.Names.GameId, GameManager.Instance.RoomServerManager.RoomController.RoomId);
            ProcessManager.RunProcess(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName, "Builds/App/Win/ClientRoom/ClientRoom.exe"), processArguments.ToReadableString(" ", " "));
        }
        public void SpawnBotObject(Vector3 pos)
        {
            botPrefab.GetComponent<Player>().IsBot = true;
            var bot = botPrefab.InstantiateNetworked<BotPlayer>(null, transform, pos);
            bot.Id = bots.Count;
            bots.Add(bot);
        }
    }
}

