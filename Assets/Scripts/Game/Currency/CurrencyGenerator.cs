using DG.Tweening;
using FishNet.Object;
using Framework;
using Framework.FishNet;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyGenerator : NetworkBehaviour
{
    [SerializeField] GameObject currencyPrefab;
    [SerializeField] int maxCurrency = 4;
    public ObservableList<Currency> currencies = new(new List<Currency>());

    public override void OnStartServer()
    {
        base.OnStartServer();
        currencies = new(new List<Currency>());
        for (int i = 0; i < maxCurrency; i++)
        {
            SpawnCurrency();
        }
        currencies.OnChanged += CurrencyList_OnChanged;
    }

    private void CurrencyList_OnChanged(Currency currency, int index, Operation op)
    {
        switch (op)
        {
            case Operation.Add:
                currencies.Add(currency);
                break;
            case Operation.Modify:
                break;
            case Operation.Remove:
                if (currencies.Value.Count < maxCurrency)
                {
                    DOVirtual.DelayedCall(0.5f, () => SpawnCurrency());
                }
                break;
            case Operation.Clear:
                break;
            default:
                break;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        currencies.OnChanged -= CurrencyList_OnChanged;
    }

    [Server]
    public void SpawnCurrency()
    {
        currencyPrefab.InstantiateNetworked<Currency>(null, transform, MapManager.RandomPositionInsideMap());
    }
}
