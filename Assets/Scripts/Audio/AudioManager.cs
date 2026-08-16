using System;
using UnityEngine;

namespace Willowstead.Audio
{
    public enum AudioChannel
    {
        Master,
        Weather,
        Music,
        SFX,
        Ambience
    }

    /// <summary>
    /// Central audio settings manager that persists individual audio channel volumes
    /// (Master, Weather, Music, SFX, Ambience) and applies them live to all audio systems.
    /// </summary>
    public static class AudioManager
    {
        private const string MasterKey = "vol_master";
        private const string WeatherKey = "vol_weather";
        private const string MusicKey = "vol_music";
        private const string SfxKey = "vol_sfx";
        private const string AmbienceKey = "vol_ambience";

        public static float MasterVolume { get; private set; } = 1f;
        public static float WeatherVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static float AmbienceVolume { get; private set; } = 1f;

        public static event Action OnVolumesChanged;

        static AudioManager()
        {
            LoadVolumes();
        }

        public static void LoadVolumes()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            WeatherVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(WeatherKey, 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            AmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceKey, 1f));

            ApplyMaster();
        }

        public static float GetVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Master: return MasterVolume;
                case AudioChannel.Weather: return WeatherVolume;
                case AudioChannel.Music: return MusicVolume;
                case AudioChannel.SFX: return SfxVolume;
                case AudioChannel.Ambience: return AmbienceVolume;
                default: return 1f;
            }
        }

        public static void SetVolume(AudioChannel channel, float volume01)
        {
            volume01 = Mathf.Clamp01(volume01);
            switch (channel)
            {
                case AudioChannel.Master:
                    MasterVolume = volume01;
                    PlayerPrefs.SetFloat(MasterKey, volume01);
                    ApplyMaster();
                    break;
                case AudioChannel.Weather:
                    WeatherVolume = volume01;
                    PlayerPrefs.SetFloat(WeatherKey, volume01);
                    break;
                case AudioChannel.Music:
                    MusicVolume = volume01;
                    PlayerPrefs.SetFloat(MusicKey, volume01);
                    break;
                case AudioChannel.SFX:
                    SfxVolume = volume01;
                    PlayerPrefs.SetFloat(SfxKey, volume01);
                    break;
                case AudioChannel.Ambience:
                    AmbienceVolume = volume01;
                    PlayerPrefs.SetFloat(AmbienceKey, volume01);
                    break;
            }
            PlayerPrefs.Save();
            OnVolumesChanged?.Invoke();
        }

        private static void ApplyMaster()
        {
            AudioListener.volume = MasterVolume;
        }

        public static void ResetToDefaults()
        {
            SetVolume(AudioChannel.Master, 1f);
            SetVolume(AudioChannel.Weather, 1f);
            SetVolume(AudioChannel.Music, 1f);
            SetVolume(AudioChannel.SFX, 1f);
            SetVolume(AudioChannel.Ambience, 1f);
        }
    }
}
