using Framework;
using System;
using UnityEngine;
namespace Framework
{
    [Serializable]
    public class PoolConfig : SingletonScriptableObject<PoolConfig>
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
        [SerializeField] private int defaultInitPoolGO; public static int DefaultInitPoolGO { get { return Instance.defaultInitPoolGO; } }
        [SerializeField] private int defaultInitPoolSound; public static int DefaultInitPoolSound { get { return Instance.defaultInitPoolSound; } }
        [SerializeField] private PoolConfigDictionary initPoolGO; public static PoolConfigDictionary InitPool { get { return Instance.initPoolGO; } }

    }
}
[Serializable]
public class PoolConfigDictionary : SerializedDictionary<GameObject, int> { }
