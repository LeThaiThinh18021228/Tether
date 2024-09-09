using UnityEngine;

namespace Framework
{
    public class ApplicationConfig : SingletonScriptableObject<ApplicationConfig>
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
        [SerializeField] private string bundleId; public static string BundleId { get { return Instance.bundleId; } }

    }

}
