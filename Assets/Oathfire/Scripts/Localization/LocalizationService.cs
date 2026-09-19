using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Localization
{
    /// <summary>
    /// Loads Resources/Localization/&lt;lang&gt;.json (flat key → string) and serves lookups.
    /// Missing keys return the key itself so gaps are obvious in game instead of silently blank.
    /// </summary>
    public class LocalizationService : MonoBehaviour
    {
        public const string DefaultLanguage = "en";
        public static readonly string[] SupportedLanguages = { "id", "en" };

        readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        readonly Dictionary<string, string> fallback = new Dictionary<string, string>();

        public string Language { get; private set; } = DefaultLanguage;
        public event Action LanguageChanged;

        [Serializable]
        class Entry
        {
            public string key;
            public string value;
        }

        [Serializable]
        class Table
        {
            public Entry[] entries;
        }

        public void Initialize(string language)
        {
            LoadInto(fallback, DefaultLanguage);
            SetLanguage(language);
        }

        public void SetLanguage(string language)
        {
            Language = Array.IndexOf(SupportedLanguages, language) >= 0 ? language : DefaultLanguage;
            LoadInto(strings, Language);
            LanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (strings.TryGetValue(key, out string value) || fallback.TryGetValue(key, out value))
                return value;
            Debug.LogWarning($"[Localization] Missing key '{key}' ({Language})");
            return key;
        }

        public string Format(string key, params object[] args) => string.Format(Get(key), args);

        static void LoadInto(Dictionary<string, string> target, string language)
        {
            target.Clear();
            var asset = Resources.Load<TextAsset>($"Localization/{language}");
            if (!asset)
            {
                Debug.LogError($"[Localization] Missing table Resources/Localization/{language}.json");
                return;
            }

            var table = JsonUtility.FromJson<Table>(asset.text);
            if (table?.entries == null)
                return;
            foreach (Entry entry in table.entries)
                target[entry.key] = entry.value;
        }
    }
}
