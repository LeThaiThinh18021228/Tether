﻿#define TAPTIC

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework
{
    public class PGameMaster : MonoBehaviour
    {
        public static event Callback<bool> OnGamePaused;
        public static event Callback OnGameQuit;
        public static event Callback OnClearData;
        public static event Callback OnSceneChanged;

        #region Runtime Init
#if (!UNITY_SERVER || GAME_SERVER) || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            GameObject obj = new GameObject(typeof(PGameMaster).ToString());
            obj.AddComponent<PGameMaster>();
            obj.AddComponent<PQuickAction>();
            InitVibration();
        }
#endif
        #endregion

        #region MonoBehaviour

        void Awake()
        {
#if UNITY_IOS
            if (UnityEngine.iOS.Device.lowPowerModeEnabled)
                Application.targetFrameRate = 30;
            else
                Application.targetFrameRate = 60;
#else
            Application.targetFrameRate = 200;
#endif

            DontDestroyOnLoad(gameObject);

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneManager_ActiveSceneChanged;
        }

        void OnApplicationPause(bool pause)
        {
            OnGamePaused?.Invoke(pause);
        }

        void OnApplicationQuit()
        {
            OnGameQuit?.Invoke();
        }

        #endregion

        #region Init

        static void InitVibration()
        {
#if TAPTIC
            Taptic.Taptic.tapticOn = PDataSettings.VibrationEnabled;

            PDataSettings.VibrationEnabledData.OnChanged += (enabled) => { Taptic.Taptic.tapticOn = enabled; };
#endif
        }

        #endregion

        #region Public Static

        public static void ClearData()
        {
            if (!Application.isPlaying)
                PDebug.Log("This action only works in PLAY MODE!");

            OnClearData?.Invoke();
        }

        #endregion

        void SceneManager_ActiveSceneChanged(Scene arg0, Scene arg1)
        {
            OnSceneChanged?.Invoke();
        }
    }
}