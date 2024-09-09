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
    public ObservableList<Currency> currencyList = new(new List<Currency>());

    public override void OnStartServer()
    {
        base.OnStartServer();
        currencyList = new(new List<Currency>());
        for (int i = 0; i < maxCurrency; i++)
        {
            SpawnCurrency();
        }
        currencyList.OnChanged += CurrencyList_OnChanged;
    }

    private void CurrencyList_OnChanged(Currency currency, int index, Operation op)
    {
        switch (op)
        {
            case Operation.Add:
                currencyList.Add(currency);
                break;
            case Operation.Modify:
                break;
            case Operation.Remove:
                if (currencyList.Value.Count < maxCurrency)
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
        currencyList.OnChanged -= CurrencyList_OnChanged;
    }

    [Server]
    public void SpawnCurrency()
    {
        Currency currency = currencyPrefab.InstantiateNetworked<Currency>(null, transform, MapManager.RandomPositionInsideMap(), Quaternion.identity);
        currency.generator = this;
    }
}
