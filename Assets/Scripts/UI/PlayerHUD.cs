using Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class PlayerHUD : CacheMonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] SpriteRenderer currencyHUDImage;
    [SerializeField] TextMeshPro currencyHUDText;
    [SerializeField] TextMeshPro nameHUDText;
    [SerializeField] TextMeshPro levelHUDText;
    void OnEnable()
    {
        player.Data.Currency.OnChange += Currency_OnChange;
        player.CurrencyAwait.OnChange += CurrencyAwait_OnChange;
        player.Data.Level.OnChange += Level_OnChange;
        player.Data.Name.OnChange += Name_OnChange;
    }

    private void CurrencyAwait_OnChange(int prev, int next, bool asServer)
    {
        currencyHUDImage.transform.SetScaleX((player.Data.Currency.Value - next) / 1000f);
        currencyHUDText.text = $"{(player.Data.Currency.Value - next)}/1000";
    }

    public void Currency_OnChange(int prev, int next, bool asServer)
    {
        if (!asServer)
        {
            currencyHUDImage.transform.SetScaleX((player.Data.Currency.Value - player.CurrencyAwait.Value) / 1000f);
            currencyHUDText.text = $"{(player.Data.Currency.Value - player.CurrencyAwait.Value)}/1000";
        }
    }
    public void Level_OnChange(int prev, int next, bool asServer)
    {
        if (!asServer)
        {
            levelHUDText.text = $"Level {next}" .ToString();
        }
    }
    public void Name_OnChange(string prev, string next, bool asServer)
    {
        if (!asServer)
        {
            nameHUDText.text = next.ToString();
        }
    }
}
