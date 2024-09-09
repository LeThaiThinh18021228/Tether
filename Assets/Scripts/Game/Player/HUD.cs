using Framework;
using MasterServerToolkit.Bridges.FishNetworking.Character;
using TMPro;
using UnityEngine;

public class HUD : SingletonMono<HUD>
{
    [SerializeField] TextMeshProUGUI currencyHUDText;
    // Start is called before the first frame update
    private void OnEnable()
    {
        Player.OnClientCharacterSpawnedEvent += Player_OnFirstObjectSpawned;
        Player.OnCharacterDestroyedEvent += Player_OnFirstObjectSpawned;

    }
    private void OnDisable()
    {
        Player.OnClientCharacterSpawnedEvent -= Player_OnFirstObjectSpawned;
        Player.OnCharacterDestroyedEvent -= Player_OnFirstObjectDespawned;
    }
    private void Player_OnFirstObjectSpawned(PlayerCharacter player)
    {
        if (!player.IsOwner) return;
        Player _player = player as Player;
        _player.Data.Currency.OnChange += Currency_OnChange;
        currencyHUDText.text = _player.Data.Currency.Value.ToString();
    }
    private void Player_OnFirstObjectDespawned(PlayerCharacter player)
    {
        if (!player.IsOwner) return;
        Player _player = player as Player;
        _player.Data.Currency.OnChange -= Currency_OnChange;
    }
    public void Currency_OnChange(int prev, int next, bool asServer)
    {
        if (!asServer)
        {
            currencyHUDText.text = next.ToString();
        }
    }
}
