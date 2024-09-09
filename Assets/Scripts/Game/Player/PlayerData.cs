using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[System.Serializable]
public class PlayerData : NetworkBehaviour
{
    public readonly SyncVar<string> Name = new();
    public readonly SyncVar<int> Currency = new();
    public readonly SyncVar<int> Level = new(0);
    public readonly SyncVar<Color> Color = new();
    public void Init()
    {
        Name.Value = "Unset";
        Currency.Value = 100;
        Level.Value = 0;
        Color.Value = Random.ColorHSV();
    }
    public void AddCurrrency(int addedValue)
    {
        int value = addedValue + Currency.Value;
        if (value > 1000)
        {
            Currency.Value = 1000;
        }
        else if (value < 0)
        {
            Currency.Value = 0;
        }
        else
        {
            Currency.Value = value;
        }
    }
    public void SetCurrrency(int value)
    {
        if (value > 1000)
        {
            Currency.Value = 1000;
        }
        else if (value < 0)
        {
            Currency.Value = 0;
        }
        else
        {
            Currency.Value = value;
        }
    }
}
