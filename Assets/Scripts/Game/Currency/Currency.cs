using FishNet.Object;
using UnityEngine;

public class Currency : NetworkBehaviour
{
    public int value;
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, "Player", System.StringComparison.Ordinal))
        {
            Despawn(NetworkConfig.DespawnType);
            other.gameObject.GetComponent<Player>().CollectCurrency(value);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
        if (GameManager.Instance)
            GameManager.Instance.CurrencyGenerator.currencies.Remove(this);
    }
}
