using UnityEngine;

namespace Oathfire.Core
{
    /// <summary>Player options that persist between sessions (language, volumes, text speed, subtitles).</summary>
    public class Settings
    {
        const string LanguageKey = "oathfire.language";
        const string MusicKey = "oathfire.volume.music";
        const string SfxKey = "oathfire.volume.sfx";
        const string VoiceKey = "oathfire.volume.voice";
        const string TextSpeedKey = "oathfire.text.speed";
        const string SubtitlesKey = "oathfire.subtitles";
        const string ShakeKey = "oathfire.shake";
        const string HandedKey = "oathfire.lefthanded";
        const string QuestArrowKey = "oathfire.questarrow";

        /// <summary>English is the game's default; Indonesian is opt-in from the options sheet.</summary>
        public string Language { get; private set; } = "en";
        public float MusicVolume { get; private set; } = 0.7f;
        public float SfxVolume { get; private set; } = 0.9f;
        public float VoiceVolume { get; private set; } = 1f;
        /// <summary>Characters per second for dialogue typing; 0 means instant.</summary>
        public float TextSpeed { get; private set; } = 45f;
        public bool Subtitles { get; private set; } = true;

        /// <summary>Camera shake on hits. Off for players who find it uncomfortable.</summary>
        public bool ScreenShake { get; private set; } = true;

        /// <summary>Puts the joystick under the right thumb instead of the left.</summary>
        public bool LeftHanded { get; private set; } = false;

        /// <summary>The arrow that points toward the next quest objective.</summary>
        public bool QuestArrow { get; private set; } = true;

        public void Load()
        {
            Language = PlayerPrefs.GetString(LanguageKey, "en");
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, SfxVolume);
            VoiceVolume = PlayerPrefs.GetFloat(VoiceKey, VoiceVolume);
            TextSpeed = PlayerPrefs.GetFloat(TextSpeedKey, TextSpeed);
            Subtitles = PlayerPrefs.GetInt(SubtitlesKey, 1) == 1;
            ScreenShake = PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            LeftHanded = PlayerPrefs.GetInt(HandedKey, 0) == 1;
            QuestArrow = PlayerPrefs.GetInt(QuestArrowKey, 1) == 1;
        }

        public void SetLanguage(string language)
        {
            Language = language;
            PlayerPrefs.SetString(LanguageKey, language);
            GameServices.Localization.SetLanguage(language);
            PlayerPrefs.Save();
        }

        public void SetVolumes(float music, float sfx, float voice)
        {
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            VoiceVolume = Mathf.Clamp01(voice);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.SetFloat(VoiceKey, VoiceVolume);
            PlayerPrefs.Save();
            GameServices.Audio.ApplyVolumes(this);
        }

        public void SetTextSpeed(float charactersPerSecond)
        {
            TextSpeed = Mathf.Max(0f, charactersPerSecond);
            PlayerPrefs.SetFloat(TextSpeedKey, TextSpeed);
            PlayerPrefs.Save();
        }

        public void SetScreenShake(bool enabled)
        {
            ScreenShake = enabled;
            PlayerPrefs.SetInt(ShakeKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetLeftHanded(bool enabled)
        {
            LeftHanded = enabled;
            PlayerPrefs.SetInt(HandedKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetQuestArrow(bool enabled)
        {
            QuestArrow = enabled;
            PlayerPrefs.SetInt(QuestArrowKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetSubtitles(bool enabled)
        {
            Subtitles = enabled;
            PlayerPrefs.SetInt(SubtitlesKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
