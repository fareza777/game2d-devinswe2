using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Audio
{
    /// <summary>
    /// Four channels: music (crossfaded), ambience (a quiet looping bed under the music), sfx (pooled
    /// one-shots) and voice (one line at a time, because a new line always interrupts the previous one).
    /// The service also owns the game's only AudioListener, so every scene is heard without each scene
    /// having to remember to add one.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        const int SfxVoices = 8;
        const float MusicFade = 1.6f;
        const float AmbienceShare = 0.55f;

        AudioSource[] musicSources;
        AudioSource[] sfxSources;
        AudioSource voiceSource;
        AudioSource ambienceSource;
        Coroutine ambienceRoutine;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        int activeMusic;
        int nextSfx;
        Coroutine musicRoutine;
        float musicVolume = 0.7f;
        float sfxVolume = 0.9f;

        public bool IsVoicePlaying => voiceSource && voiceSource.isPlaying;

        public void Initialize(Core.Settings settings)
        {
            musicSources = new[] { CreateSource("Music A", true), CreateSource("Music B", true) };
            sfxSources = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
                sfxSources[i] = CreateSource($"Sfx {i}", false);
            voiceSource = CreateSource("Voice", false);
            ambienceSource = CreateSource("Ambience", true);
            gameObject.AddComponent<AudioListener>();
            ApplyVolumes(settings);
        }

        AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        public void ApplyVolumes(Core.Settings settings)
        {
            musicVolume = settings.MusicVolume;
            if (musicSources != null && musicSources[activeMusic].isPlaying)
                musicSources[activeMusic].volume = musicVolume;
            sfxVolume = settings.SfxVolume;
            foreach (AudioSource source in sfxSources)
                source.volume = settings.SfxVolume;
            voiceSource.volume = settings.VoiceVolume;
            if (ambienceSource && ambienceRoutine == null)
                ambienceSource.volume = sfxVolume * AmbienceShare;
        }

        /// <summary>Loads a clip from Resources/{folder}/{name} once and keeps it; logs a missing clip once.</summary>
        AudioClip Clip(string folder, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string path = $"{folder}/{name}";
            if (!clips.TryGetValue(path, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(path);
                if (!clip)
                    Debug.LogWarning($"[Oathfire] Missing sound Resources/{path}");
                clips[path] = clip;
            }
            return clip;
        }

        public string CurrentMusic => musicSources != null && musicSources[activeMusic].clip ? musicSources[activeMusic].clip.name : string.Empty;
        public string CurrentAmbience => ambienceSource && ambienceSource.clip ? ambienceSource.clip.name : string.Empty;

        public void PlayMusic(string name) => PlayMusic(Clip("Music", name));

        public void PlaySfx(string name, float volume = 1f, float pitchJitter = 0.06f) =>
            PlaySfx(Clip("Sfx", name), pitchJitter, volume);

        /// <summary>Crossfades the ambience bed to another loop; null or empty fades it out.</summary>
        public void PlayAmbience(string name)
        {
            AudioClip clip = string.IsNullOrEmpty(name) ? null : Clip("Ambience", name);
            if (ambienceSource.clip == clip)
                return;
            if (ambienceRoutine != null)
                StopCoroutine(ambienceRoutine);
            ambienceRoutine = StartCoroutine(AmbienceRoutine(clip));
        }

        IEnumerator AmbienceRoutine(AudioClip clip)
        {
            float start = ambienceSource.volume;
            for (float t = 0f; t < MusicFade && ambienceSource.isPlaying; t += Time.unscaledDeltaTime)
            {
                ambienceSource.volume = Mathf.Lerp(start, 0f, t / MusicFade);
                yield return null;
            }
            ambienceSource.Stop();
            ambienceSource.clip = clip;
            if (clip)
            {
                ambienceSource.Play();
                float target = sfxVolume * AmbienceShare;
                for (float t = 0f; t < MusicFade; t += Time.unscaledDeltaTime)
                {
                    ambienceSource.volume = Mathf.Lerp(0f, target, t / MusicFade);
                    yield return null;
                }
                ambienceSource.volume = target;
            }
            ambienceRoutine = null;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (!clip || musicSources[activeMusic].clip == clip)
                return;
            if (musicRoutine != null)
                StopCoroutine(musicRoutine);
            musicRoutine = StartCoroutine(CrossfadeRoutine(clip));
        }

        IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            AudioSource from = musicSources[activeMusic];
            activeMusic = 1 - activeMusic;
            AudioSource to = musicSources[activeMusic];

            to.clip = clip;
            to.volume = 0f;
            to.Play();

            for (float t = 0f; t < MusicFade; t += Time.unscaledDeltaTime)
            {
                float k = t / MusicFade;
                to.volume = Mathf.Lerp(0f, musicVolume, k);
                from.volume = Mathf.Lerp(musicVolume, 0f, k);
                yield return null;
            }

            to.volume = musicVolume;
            from.Stop();
            from.clip = null;
            musicRoutine = null;
        }

        public void StopMusic()
        {
            foreach (AudioSource source in musicSources)
                source.Stop();
        }

        public void PlaySfx(AudioClip clip, float pitchJitter = 0.06f, float volume = 1f)
        {
            if (!clip)
                return;
            AudioSource source = sfxSources[nextSfx];
            nextSfx = (nextSfx + 1) % SfxVoices;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }

        public float PlayVoice(AudioClip clip)
        {
            voiceSource.Stop();
            if (!clip)
                return 0f;
            voiceSource.clip = clip;
            voiceSource.Play();
            return clip.length;
        }

        public void StopVoice() => voiceSource.Stop();
    }
}
