using Framework.HSPDIMAlgo;

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

    protected void Update()
    {
        if (!IsServerInitialized) return;
        Modified = new(Modified.X || (player.Movable.Dir.Value.x != 0)
            , Modified.Y || (player.Movable.Dir.Value.z != 0)
            , Modified.Z || (player.Movable.Dir.Value.y != 0));
        if (Modified != Vector3Bool.@false && HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
        {
            HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
        }
    }

}
