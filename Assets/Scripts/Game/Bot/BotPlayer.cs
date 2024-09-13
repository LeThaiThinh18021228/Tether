using UnityEngine;
namespace Bot
{
    public class BotPlayer : Player
    {
        public override bool IsBot { get; set; } = true;
        public int Id { get; set; } = -1;
        // Start is called before the first frame update
        public override void OnStartServer()
        {
            base.OnStartServer();
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
                    Movable.SetDes(des);
                }
            }
        }
    }
}

