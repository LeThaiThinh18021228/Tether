using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
namespace SessionManagement
{
    public class CreateNewRoom : MonoBehaviour
    {
        [SerializeField] TMP_InputField roomName;
        [SerializeField] TMP_InputField maxPlayer;
        [SerializeField] TMP_InputField botNumber;
        [SerializeField] TMP_InputField password;
        public void CreateRoom()
        {
            if (string.IsNullOrEmpty(roomName.text) && string.IsNullOrEmpty(maxPlayer.text)) return;
            var spawnOptions = new MstProperties();
            spawnOptions.Add(Mst.Args.Names.RoomMaxConnections, ushort.Parse(maxPlayer.text));
            spawnOptions.Add(Mst.Args.Names.RoomName, roomName.text);
            spawnOptions.Add(Mst.Args.Names.RoomRegion, null);
            if (!string.IsNullOrEmpty(password.text))
                spawnOptions.Add(Mst.Args.Names.RoomPassword, password.text);
            spawnOptions.Add(Mst.Args.Names.RoomIsPrivate, false);
            spawnOptions.Add(Mst.Args.Names.RoomBotNumner, botNumber.text);

            // Custom options that will be given to room as command-line arguments
            var customSpawnOptions = new MstProperties();

            // Start new game server/room instance
            MatchmakingBehaviour.Instance.CreateNewRoom(spawnOptions, customSpawnOptions, null, password.text);
        }
    }

}