using UnityEngine;

namespace Framework
{
    public class InitializationConfig : SingletonScriptableObject<InitializationConfig>
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
        [SerializeField] private int initCoin; public static int InitCoin { get { return Instance.initCoin; } }
        [SerializeField] private int initGem; public static int InitGem { get { return Instance.initGem; } }

    }

}
