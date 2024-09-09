using FishNet;
using FishNet.Object;
using MasterServerToolkit.MasterServer;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace SessionManagement {
    public class PlayerSpawner : FishNet.Component.Spawning.PlayerSpawner
    {
        [SerializeField] NetworkObject botPrefab;
        private static RoomServerManager roomServerManager;
        public static RoomServerManager RoomServerManager
        {
            get
            {
                if (roomServerManager == null)
                    roomServerManager = InstanceFinder.ServerManager.GetComponent<RoomServerManager>();
                return roomServerManager;
            }
        }
        protected override NetworkObject SetPrefab()
        {
            Debug.Log("Is bot:" + RoomServerManager.IsBot);
            if (RoomServerManager.IsBot)
            {
                return botPrefab;
            }
            return base.SetPrefab();
        }
    }
}

