using UnityEngine;

namespace Framework
{
    public class VFXFactory : SingletonScriptableObject<VFXFactory>
    {
#if UNITY_EDITOR && (!UNITY_SERVER || GAME_SERVER)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (_instance == null)
            {
                Instance.ToString();
            }
        }
#endif
        [SerializeField] private GameObject electric; public static GameObject Electric { get { return Instance.electric; } }

    }
}

