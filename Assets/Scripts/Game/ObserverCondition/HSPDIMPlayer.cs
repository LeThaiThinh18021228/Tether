using HSPDIMAlgo;
using UnityEngine;

public class HSPDIMPlayer : HSPDIMEntity
{
    Player player;
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (IsServerInitialized)
        {
            player = GetComponent<Player>();
        }
    }
    protected override void Update()
    {
        base.Update();
        if (!IsServerInitialized) return;
        if (HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
        {
            if (player.Movable.Dir.Value != Vector3.zero)
            {
                HSPDIM.Instance.subRanges.Add(SubRange);
                HSPDIM.Instance.upRanges.Add(UpRange);
                SubRange.modified = new(
                    SubRange.modified.X || (player.Movable.Dir.Value.x != 0),
                    SubRange.modified.Y || (player.Movable.Dir.Value.z != 0),
                    SubRange.modified.Z || (player.Movable.Dir.Value.y != 0));
                UpRange.modified = new(
                    UpRange.modified.X || (player.Movable.Dir.Value.x != 0),
                    UpRange.modified.Y || (player.Movable.Dir.Value.z != 0),
                    UpRange.modified.Z || (player.Movable.Dir.Value.y != 0));
            }
        }
    }

}
