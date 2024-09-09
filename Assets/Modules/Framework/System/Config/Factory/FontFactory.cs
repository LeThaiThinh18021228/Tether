using UnityEngine;

namespace Framework
{
    public class FontFactory : SingletonScriptableObject<FontFactory>
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
    }
}