using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Framework
{
    public enum SoundType
    {
        MAINTOWER_PROJECTILE_FIRE,
        ENEMY1_ATTACK
    }
    [Serializable]
    public struct ClipConfig
    {
        [HorizontalGroup("ClipConfig"), LabelWidth(50)]
        public AudioClip clip;
        [HorizontalGroup("ClipConfig"), LabelWidth(50), Range(0f, 1f)]
        public float volumn;
    }
    [Serializable]
    public struct SoundConfig
    {
        public SoundType type;
        public ClipConfig[] clipConfigs;
        [HorizontalGroup("SoundConfig"), LabelWidth(50), Tooltip("0 is 2D sound and 1 is 3D sound"), Range(0f, 1f)]
        public float spatial;
        [HorizontalGroup("SoundConfig"), LabelWidth(50)]
        public bool isFollow;
        [HorizontalGroup("SoundConfig"), LabelWidth(110), Tooltip("Value <= 0 mean no limit active sound")]
        public int maxActiveSound;
    }
}