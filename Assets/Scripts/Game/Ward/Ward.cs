using FishNet.Object;
using Framework;
using Framework.FishNet;
using System.Collections.Generic;
using UnityEngine;

public class Ward : NetworkBehaviour
{
    public Dictionary<Ward, Link> Links = new Dictionary<Ward, Link>();
    [SerializeField] MeshRenderer mesh;
    public Player Player { get; private set; }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner)
        {
            mesh.material.color = new Color(0.52f, 0.78f, 0.37f);
        }
        else
        {
            mesh.material.color = new Color(0.78f, 0.4f, 0.37f);
        }
    }

    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    public Link CreateLink(Ward ward, Player player)
    {
        this.Player = player;
        Link link = VFXFactory.Electric.InstantiateNetworked<Link>(Owner, GameManager.Instance.WardRoot.transform);
        link.Init(this, ward, player, 5, 3);
        return link;
    }
}
