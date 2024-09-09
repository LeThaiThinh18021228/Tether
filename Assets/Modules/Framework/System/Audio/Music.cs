using System;

namespace Framework
{
    public enum MusicType
    {
        INGAME,
        MAINMENU,
        RADAR,
        END,
    }
    [Serializable]
    public struct MusicConfig
    {
        public MusicType type;
        public ClipConfig clipConfig;
    }
}