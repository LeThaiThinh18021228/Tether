using FishNet.Object;
using Framework;
using UnityEngine;
namespace Bot
{
    public class BotBehaviour : NetworkBehaviour
    {
        public int botId;
        [SerializeField]Player player;
        // Start is called before the first frame update
        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("BotCreated");
            Vector3 des = MapManager.RandomPositionInsideMap();
            player.Movable.Dir.OnChange += Dir_OnChangeServer;
            player.Movable.SetDes(des);
        }
        protected void Dir_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
        {
            if (asServer)
            {
                if (next == Vector3.zero)
                {
                    Vector3 des = MapManager.RandomPositionInsideMap();
                    PDebug.Log($"Bot {botId} moving from {transform.position} to {des}");
                    player.Movable.SetDes(des);
                }
            }
        }
    }
}

