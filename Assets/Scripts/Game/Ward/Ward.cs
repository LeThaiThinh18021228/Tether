using DigitalRuby.LightningBolt;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework;
using Framework.FishNet;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ward : NetworkBehaviour
{
    public Dictionary<Ward, Link> Links = new Dictionary<Ward, Link>();
    [SerializeField] MeshRenderer mesh;
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
    public Link CreateLink(Ward ward)
    {
        Link link = VFXFactory.Electric.InstantiateNetworked<Link>(Owner, WardRoot.Instance.transform);
        link.interval = 5;
        link.duration = 3;
        link.SetPosition(this, ward);
        this.transform.parent = WardRoot.Instance.transform;
        ward.transform.parent = WardRoot.Instance.transform;
        return link;
    }
}
