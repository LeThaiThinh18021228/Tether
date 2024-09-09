using FishNet.Connection;
using FishNet.Object;
using Framework;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public class Currency : NetworkBehaviour
{
    public int value;
    public CurrencyGenerator generator;
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, "Player", System.StringComparison.Ordinal))
        {
            Despawn(NetworkConfig.DespawnType);
            other.gameObject.GetComponent<Player>().Data.AddCurrrency(value);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
        if (generator != null)
            generator.currencyList.Remove(this);
    }
}
