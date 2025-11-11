using Framework.HSPDIMAlgo;
using UnityEngine;
namespace Bot
{
    public class BotPlayer : Player
    {
        private BotAgent agent;
        private HSPDIMPlayer HSPDIMEntity;
        public override bool IsBot { get; set; } = true;
        public int Id { get; set; } = -1;
        // Start is called before the first frame update
        public override void OnStartServer()
        {
            base.OnStartServer();
            agent = GetComponent<BotAgent>();
            HSPDIMEntity = GetComponent<HSPDIMPlayer>();
            GameManager.Instance.State.OnChange += GameState_OnChange;
        }

        private void GameState_OnChange(GameState prev, GameState next, bool asServer)
        {
            if (!asServer) return;
            if (next == GameState.NONSTARTED) return;
            Vector3 des = MapManager.RandomPositionInsideMap(new Vector3(10,10,10));
            Movable.SetDes(des);
        }

        protected override void Dir_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
        {
            base.Dir_OnChangeServer(prev, next, asServer);
            if (agent.enabled) return;
            if (asServer)
            {
                if (next == Vector3.zero)
                {
                    Vector3 des = MapManager.RandomPositionInsideMap(new Vector3(10, 10, 10));
                    Movable.SetDes(des);
                }
            }
        }
    }
}

