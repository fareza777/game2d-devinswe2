using System;

namespace Oathfire.Cinematic
{
    /// <summary>Data for a panel-based cinematic, generated from Docs/opening_cinematic.json.</summary>
    [Serializable]
    public class CinematicData
    {
        public string id;
        public string musicClip;
        public string nextScene;
        public CinematicPanel[] panels;
    }

    [Serializable]
    public class CinematicPanel
    {
        public string id;
        /// <summary>Sprite in Resources/Cinematic/&lt;image&gt;.</summary>
        public string image;
        public CinematicLine[] lines;
        /// <summary>Slow push-in/pan, in normalized screen units per panel.</summary>
        public float zoomFrom = 1.04f;
        public float zoomTo = 1.12f;
        public float panFromY;
        public float panToY = 0.04f;
        /// <summary>Warm fire glows layered over the print (normalized 0..1 from bottom-left).</summary>
        public CinematicGlow[] glows;
        /// <summary>Skeletal watchers fading in among the trees (normalized positions).</summary>
        public CinematicWatcher[] watchers;
        public bool emberParticles;
    }

    [Serializable]
    public class CinematicLine
    {
        public string speaker;
        public string textKey;
        public string voiceClip;
        /// <summary>Extra hold after the voice line, in seconds.</summary>
        public float hold = 0.6f;
    }

    [Serializable]
    public class CinematicGlow
    {
        public float x;
        public float y;
        public float radius = 0.18f;
        public float intensity = 1f;
        public bool flicker = true;
    }

    [Serializable]
    public class CinematicWatcher
    {
        public float x;
        public float y;
        public float scale = 1f;
        /// <summary>Seconds into the panel when this silhouette fades in.</summary>
        public float appearAt;
    }
}
