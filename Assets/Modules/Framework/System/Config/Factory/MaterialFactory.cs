using UnityEngine;
namespace Framework
{
    public class MaterialFactory : SingletonScriptableObject<MaterialFactory>
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
