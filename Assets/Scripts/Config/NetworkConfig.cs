using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework;
using UnityEngine;

public class NetworkConfig : SingletonScriptableObjectModulized<NetworkConfig>
{
#if (!UNITY_SERVER || GAME_SERVER) && UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_instance == null)
        {
            Instance.ToString();
        }
    }
#endif
    [SerializeField] private DespawnType despawnType; public static DespawnType DespawnType { get { return Instance.despawnType; } }
    [SerializeField] private SyncTypeSettings syncTypeSettingsClientAuthorized; public static SyncTypeSettings SyncTypeSettingsClientAuthorized { get { return Instance.syncTypeSettingsClientAuthorized; } }
}
