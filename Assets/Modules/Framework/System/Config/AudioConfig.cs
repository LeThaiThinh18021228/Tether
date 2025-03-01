using System;
using UnityEngine;

namespace Framework
{
    public class AudioConfig : SingletonScriptableObject<AudioConfig>
    {
#if (!UNITY_SERVER || GAME_SERVER) || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (_instance == null)
            {
                Instance.ToString();
            }
        }
#endif
        [SerializeField] private MusicConfigDictionary musicConfigs; public static MusicConfigDictionary MusicConfigs { get { return Instance.musicConfigs; } }
        [SerializeField] private SoundConfigDictionary soundConfigs; public static SoundConfigDictionary SoundConfigs { get { return Instance.soundConfigs; } }
    }


    [Serializable]
    public class MusicConfigDictionary : SerializedDictionary<MusicType, MusicConfig> { }
    [Serializable]
    public class SoundConfigDictionary : SerializedDictionary<SoundType, SoundConfig> { }
}
