using TMPro;
using UnityEngine;

namespace Oathfire.Localization
{
    /// <summary>Drops a localized string into a TMP label and keeps it current when the language changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] string key;

        TMP_Text label;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        void Awake() => label = GetComponent<TMP_Text>();

        void OnEnable()
        {
            Core.GameServices.EnsureCreated();
            Core.GameServices.Localization.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (Core.GameServices.Localization)
                Core.GameServices.Localization.LanguageChanged -= Refresh;
        }

        void Refresh()
        {
            if (!label)
                label = GetComponent<TMP_Text>();
            label.text = Core.GameServices.Localization.Get(key);
        }
    }
}
