using Framework;
using System;
using UnityEngine;
namespace Utilities
{
    [Serializable]
    public class SceneTransitionConfigSO : SingletonScriptableObjectModulized<SceneTransitionConfigSO>
    {
        [Header("Config")]
        [SerializeField] float _loadDuration = 0.1f; public static float LoadDuration => Instance._loadDuration;
    }
}

