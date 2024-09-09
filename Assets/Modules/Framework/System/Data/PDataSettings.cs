using UnityEngine;

namespace Framework
{
    public class PDataSettings : PDataBlock<PDataSettings>
    {
        [SerializeField] ObservableData<bool> _soundEnabled;
        [SerializeField] ObservableData<bool> _musicEnabled;
        [SerializeField] ObservableData<bool> _vibrationEnabled;

        public static bool SoundEnabled { get { return Instance._soundEnabled.Value; } set { Instance._soundEnabled.Value = value; } }
        public static bool MusicEnabled { get { return Instance._musicEnabled.Value; } set { Instance._musicEnabled.Value = value; } }
        public static bool VibrationEnabled { get { return Instance._vibrationEnabled.Value; } set { Instance._vibrationEnabled.Value = value; } }

        public static ObservableData<bool> SoundEnabledData { get { return Instance._soundEnabled; } }
        public static ObservableData<bool> MusicEnabledData { get { return Instance._musicEnabled; } }
        public static ObservableData<bool> VibrationEnabledData { get { return Instance._vibrationEnabled; } }

        protected override void Init()
        {
            base.Init();

            _soundEnabled = _soundEnabled == null ? new ObservableData<bool>(true) : _soundEnabled;
            _musicEnabled = _musicEnabled == null ? new ObservableData<bool>(true) : _musicEnabled;
            _vibrationEnabled = _vibrationEnabled == null ? new ObservableData<bool>(true) : _vibrationEnabled;
        }
    }
}