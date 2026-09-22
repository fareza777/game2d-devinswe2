using UnityEngine;

namespace Oathfire.Core
{
    /// <summary>
    /// Single access point for the persistent services. Created by GameBootstrap, or lazily when a scene is
    /// played directly from the editor, so no scene depends on load order.
    /// </summary>
    public static class GameServices
    {
        static GameObject root;

        public static Localization.LocalizationService Localization { get; private set; }
        public static Audio.AudioService Audio { get; private set; }
        public static Save.SaveService Save { get; private set; }
        public static SceneFlow Flow { get; private set; }
        public static Settings Settings { get; private set; }
        public static Ads.AdsService Ads { get; private set; }

        public static void EnsureCreated()
        {
            if (root)
                return;

            root = new GameObject("[Oathfire Services]");
            Object.DontDestroyOnLoad(root);

            Settings = new Settings();
            Settings.Load();

            Localization = root.AddComponent<Localization.LocalizationService>();
            Localization.Initialize(Settings.Language);

            Audio = root.AddComponent<Audio.AudioService>();
            Audio.Initialize(Settings);
            root.AddComponent<Audio.SoundDirector>().Initialize(Audio);

            Save = new Save.SaveService();
            Flow = root.AddComponent<SceneFlow>();

            Ads = root.AddComponent<Ads.AdsService>();
            Ads.Initialize();
        }
    }
}
