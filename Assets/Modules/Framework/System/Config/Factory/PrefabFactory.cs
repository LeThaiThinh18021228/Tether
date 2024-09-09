using UnityEngine;
namespace Framework
{
    public class PrefabFactory : SingletonScriptableObject<PrefabFactory>
    {
#if !UNITY_SERVER || GAME_SERVER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
#if UNITY_EDITOR
            if (_instance == null)
            {
                Instance.ToString();
            }
#endif
            DontDestroyOnloadManager.Create();
        }
#endif
        #region Defaut
        [SerializeField] private GameObject dontDestroyOnloadManager; public static GameObject DontDestroyOnloadManager { get { return Instance.dontDestroyOnloadManager; } }
        [SerializeField] private GameObject textPrefab; public static GameObject TextPrefab { get { return Instance.textPrefab; } }
        [SerializeField] private GameObject audioSourcePrefab; public static GameObject AudioSourcePrefab { get { return Instance.audioSourcePrefab; } }
        #endregion
        [SerializeField] private GameObject wardModel; public static GameObject WardModel { get { return Instance.wardModel; } }
        [SerializeField] private GameObject ward; public static GameObject Ward { get { return Instance.ward; } }
    }
}

