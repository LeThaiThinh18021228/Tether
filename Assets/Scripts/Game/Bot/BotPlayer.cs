using FishNet.Object;
using Framework;
using UnityEngine;
namespace Bot
{
    public class BotPlayer : Player
    {
        public override bool IsBot { get; set; } = true;
        public int botId;
        // Start is called before the first frame update
        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("BotCreated");
            Vector3 des = MapManager.RandomPositionInsideMap();
            Movable.SetDes(des);
        }
        protected override void Dir_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
        {
            base.Dir_OnChangeServer(prev, next, asServer);
            if (asServer)
            {
                if (next == Vector3.zero)
                {
                    Vector3 des = MapManager.RandomPositionInsideMap();
                    PDebug.Log($"Bot {botId} moving from {transform.position} to {des}");
                    Movable.SetDes(des);
                }
            }
        }
    }
}

