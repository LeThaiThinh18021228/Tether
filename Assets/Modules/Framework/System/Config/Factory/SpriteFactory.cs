using System.Collections.Generic;
using UnityEngine;
namespace Framework
{
    public class SpriteFactory : SingletonScriptableObject<SpriteFactory>
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
        [SerializeField] private Sprite[][] arrSprite; public static Sprite[][] ArrSprite { get { return Instance.arrSprite; } }
        [SerializeField] private Sprite[][] arrarrSprite; public static Sprite[][] ArrarrSprite { get { return Instance.arrarrSprite; } }
        [SerializeField] private List<Sprite[]> listArrSprite; public static List<Sprite[]> ListArrSprite { get { return Instance.listArrSprite; } }
        [SerializeField] private Sprite sprite; public static Sprite Sprite { get { return Instance.sprite; } }
    }

}