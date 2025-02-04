using FishNet.Object;
using Framework.HSPDIMAlgo;
using HSPDIMAlgo;
using UnityEngine;

public class Currency : NetworkBehaviour
{
    public int value;
    public HSPDIMEntity HSPDIMEntity;
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, "Player", System.StringComparison.Ordinal))
        {
            Player player = other.GetComponent<Player>();
            //if (player.Movable.Dir.Value != Vector3.zero)
            {
                Despawn(NetworkConfig.DespawnType);
                other.gameObject.GetComponent<Player>().CollectCurrency(value);
            }
        }
    }
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (IsServerInitialized)
        {
            HSPDIMEntity = GetComponent<HSPDIMEntity>();
        }
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        GameManager.Instance.CurrencyGenerator.currencies.Add(this);
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
        if (GameManager.Instance)
            GameManager.Instance.CurrencyGenerator.currencies.Remove(this);
    }
}
