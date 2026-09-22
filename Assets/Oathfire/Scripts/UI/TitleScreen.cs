using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// Title screen: woodcut key art, the burning title, and the run of choices. Continue only appears when
    /// a save exists, and the options sheet carries language, volumes, text speed and subtitles.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        const string OpeningScene = "Opening";
        static readonly Color Ink = UiTheme.Ink;
        static readonly Color Bone = UiTheme.Bone;
        static readonly Color BoneDim = UiTheme.BoneDim;
        static readonly Color Ember = UiTheme.Ember;

        [SerializeField] Sprite keyArt;
        [SerializeField] Sprite glowSprite;
        [SerializeField] Sprite logo;
        [SerializeField] Sprite buttonPlate;
        [SerializeField] AudioClip menuMusic;

        RectTransform menuHolder;
        RectTransform optionsSheet;
        RectTransform continueSheet;
        RectTransform continueRows;
        Image fireGlow;
        readonly List<Button> buttons = new List<Button>();

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        void Start()
        {
            if (menuMusic)
                Core.GameServices.Audio.PlayMusic(menuMusic);
            Core.GameServices.Ads?.EnsureBanner();
        }

        void OnDestroy()
        {
            Core.GameServices.Ads?.HideBanner();
        }

        void Update()
        {
            if (!fireGlow)
                return;
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 3.5f, 0f);
            fireGlow.color = new Color(1f, 0.62f, 0.22f, 0.5f * flicker);
            fireGlow.rectTransform.localScale = Vector3.one * (0.97f + 0.06f * flicker);
        }

        void NewGame()
        {
            Core.GameServices.Save.NewGame();
            Core.GameServices.Flow.LoadScene(OpeningScene);
        }

        /// <summary>Every written page is listed — the autosave first, then the three hand-saved slots.</summary>
        void Continue()
        {
            foreach (Transform child in continueRows)
                Destroy(child.gameObject);

            var save = Core.GameServices.Save;
            bool any = false;
            for (int slot = Save.SaveService.AutosaveSlot; slot < Save.SaveService.SlotCount; slot++)
            {
                Save.SaveData peek = save.Peek(slot);
                if (peek == null)
                    continue;
                int captured = slot;
                string title = slot == Save.SaveService.AutosaveSlot
                    ? Core.GameServices.Localization.Get("menu.autosave")
                    : Core.GameServices.Localization.Format("menu.slot", slot + 1);
                string meta = $"{peek.sceneName}  ·  {FormatWhen(peek.savedAtIso)}";
                AddSaveRow(title, meta, () => ContinueFrom(captured));
                any = true;
            }

            continueSheet.gameObject.SetActive(any);
        }

        void ContinueFrom(int slot)
        {
            if (!Core.GameServices.Save.Load(slot))
                return;
            Core.GameServices.Flow.LoadScene(Core.GameServices.Save.Current.sceneName);
        }

        static string FormatWhen(string iso) =>
            DateTime.TryParse(iso, out DateTime when) ? when.ToLocalTime().ToString("d MMM · HH:mm") : "";

        void AddSaveRow(string title, string meta, Action action)
        {
            var plate = NewImage("SaveRow", continueRows, null, new Color(0.13f, 0.12f, 0.09f, 0.95f));
            plate.raycastTarget = true;
            plate.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
            var button = plate.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.18f, 0.13f);
            colors.pressedColor = Ember;
            button.colors = colors;
            button.onClick.AddListener(() => action());

            var name = NewText("Name", plate.rectTransform, 38, Bone);
            name.text = title;
            name.fontStyle = FontStyles.SmallCaps;
            name.characterSpacing = 4f;
            name.alignment = TextAlignmentOptions.Left;
            name.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
            name.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var info = NewText("Meta", plate.rectTransform, 28, BoneDim);
            info.text = meta;
            info.alignment = TextAlignmentOptions.Left;
            info.rectTransform.anchorMin = new Vector2(0.05f, 0.05f);
            info.rectTransform.anchorMax = new Vector2(0.95f, 0.5f);
            info.rectTransform.offsetMin = info.rectTransform.offsetMax = Vector2.zero;
        }

        void BuildContinueSheet()
        {
            continueSheet = NewRect("ContinueSheet", transform);
            Stretch(continueSheet);
            var dim = NewImage("Dim", continueSheet, null, new Color(0.02f, 0.02f, 0.02f, 0.93f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => continueSheet.gameObject.SetActive(false));

            var sheet = NewImage("Sheet", continueSheet, null, new Color(0.09f, 0.1f, 0.08f, 0.98f));
            sheet.rectTransform.anchorMin = new Vector2(0.08f, 0.25f);
            sheet.rectTransform.anchorMax = new Vector2(0.92f, 0.75f);
            sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;
            sheet.raycastTarget = true;

            var heading = NewLocalizedText("Heading", sheet.rectTransform, 46, Ember, "menu.loadTitle");
            heading.fontStyle = FontStyles.SmallCaps;
            heading.characterSpacing = 6f;
            heading.rectTransform.anchorMin = new Vector2(0f, 0.86f);
            heading.rectTransform.anchorMax = new Vector2(1f, 0.98f);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;

            continueRows = NewRect("Rows", sheet.rectTransform);
            continueRows.anchorMin = new Vector2(0.06f, 0.16f);
            continueRows.anchorMax = new Vector2(0.94f, 0.84f);
            continueRows.offsetMin = continueRows.offsetMax = Vector2.zero;
            var rowsLayout = continueRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 14f;
            rowsLayout.childControlHeight = true;
            rowsLayout.childControlWidth = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = true;

            var close = NewImage("Close", sheet.rectTransform, null, new Color(0.14f, 0.13f, 0.1f, 1f));
            close.raycastTarget = true;
            close.rectTransform.anchorMin = new Vector2(0.25f, 0.02f);
            close.rectTransform.anchorMax = new Vector2(0.75f, 0.12f);
            close.rectTransform.offsetMin = close.rectTransform.offsetMax = Vector2.zero;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => continueSheet.gameObject.SetActive(false));
            NewLocalizedText("Label", close.rectTransform, 40, Bone, "menu.back");

            continueSheet.gameObject.SetActive(false);
        }

        void ToggleOptions(bool visible) => optionsSheet.gameObject.SetActive(visible);

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            var ground = NewImage("Ground", transform, null, Ink);
            Stretch(ground.rectTransform);

            // The woodcut fills its region without being stretched: the frame keeps the print's proportions
            // and crops the sides, where a stretched print would squash every tree and the moon with it.
            var artFrame = NewRect("KeyArtFrame", transform);
            artFrame.anchorMin = new Vector2(0f, 0.3f);
            artFrame.anchorMax = new Vector2(1f, 1f);
            artFrame.offsetMin = artFrame.offsetMax = Vector2.zero;
            var mask = artFrame.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;

            var art = NewImage("KeyArt", artFrame, keyArt, Color.white);
            art.preserveAspect = true;
            if (keyArt)
            {
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = keyArt.rect.width / keyArt.rect.height;
            }

            // The print fades into the dark instead of ending on a hard line halfway down the screen.
            var fade = NewImage("ArtFade", artFrame, VerticalFade(), Ink);
            fade.rectTransform.anchorMin = new Vector2(0f, 0f);
            fade.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;

            var scrim = NewImage("Scrim", transform, null, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Stretch(scrim.rectTransform);

            // A soft dark band behind the logo so the wordmark never fights the woodcut behind it.
            var band = NewImage("LogoBand", transform, glowSprite, new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            band.rectTransform.anchorMin = new Vector2(-0.2f, 0.6f);
            band.rectTransform.anchorMax = new Vector2(1.2f, 0.94f);
            band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;

            fireGlow = NewImage("TitleGlow", transform, glowSprite, new Color(1f, 0.62f, 0.22f, 0.5f));
            fireGlow.rectTransform.anchorMin = fireGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.79f);
            fireGlow.rectTransform.sizeDelta = new Vector2(1000f, 1000f);

            var logoImage = NewImage("Logo", transform, logo, Color.white);
            logoImage.preserveAspect = true;
            logoImage.rectTransform.anchorMin = new Vector2(0.03f, 0.68f);
            logoImage.rectTransform.anchorMax = new Vector2(0.97f, 0.9f);
            logoImage.rectTransform.offsetMin = logoImage.rectTransform.offsetMax = Vector2.zero;

            // The tagline sat in pale italic straight on the woodcut and could barely be read. It gets a soft
            // dark band of its own, full bone colour, and a thin ink outline.
            var tagBand = NewImage("TaglineBand", transform, glowSprite, new Color(Ink.r, Ink.g, Ink.b, 0.95f));
            tagBand.rectTransform.anchorMin = new Vector2(-0.1f, 0.6f);
            tagBand.rectTransform.anchorMax = new Vector2(1.1f, 0.69f);
            tagBand.rectTransform.offsetMin = tagBand.rectTransform.offsetMax = Vector2.zero;

            // Upright, larger and warmer: the thin italic sank into the print.
            var tagline = NewLocalizedText("Tagline", transform, 42, new Color(0.95f, 0.9f, 0.78f), "title.tagline");
            tagline.fontStyle = FontStyles.Normal;
            tagline.characterSpacing = 2f;
            tagline.outlineWidth = 0.22f;
            tagline.outlineColor = new Color32(8, 8, 6, 255);
            tagline.rectTransform.anchorMin = new Vector2(0.06f, 0.615f);
            tagline.rectTransform.anchorMax = new Vector2(0.94f, 0.675f);
            tagline.rectTransform.offsetMin = tagline.rectTransform.offsetMax = Vector2.zero;

            menuHolder = NewRect("Menu", transform);
            menuHolder.anchorMin = new Vector2(0.12f, 0.06f);
            menuHolder.anchorMax = new Vector2(0.88f, 0.4f);
            menuHolder.offsetMin = menuHolder.offsetMax = Vector2.zero;
            var layout = menuHolder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.LowerCenter;

            bool hasSave = Core.GameServices.Save.MostRecentSlot() != int.MinValue;
            if (hasSave)
                AddButton("menu.continue", Continue);
            AddButton("menu.newGame", NewGame);
            AddButton("menu.options", () => ToggleOptions(true));
            AddButton("menu.quit", Quit);

            BuildContinueSheet();
            BuildOptions();
        }

        void AddButton(string key, Action action)
        {
            var image = NewImage(key, menuHolder, buttonPlate, new Color(0.13f, 0.12f, 0.09f, 0.92f));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = new Vector2(0f, 110f);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 110f;
            element.preferredHeight = 110f;
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.18f, 0.13f);
            colors.pressedColor = Ember;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            buttons.Add(button);

            var label = NewLocalizedText("Label", image.rectTransform, 42, Bone, key);
            label.alignment = TextAlignmentOptions.Center;
            label.AsHeading(8f);
        }

        static Sprite fadeSprite;

        /// <summary>A 1×64 alpha ramp: clear at the top, solid at the bottom, tinted by the Image colour.</summary>
        static Sprite VerticalFade()
        {
            if (fadeSprite)
                return fadeSprite;
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                // Eased so the dark gathers near the bottom rather than dimming the whole print evenly.
                float alpha = Mathf.SmoothStep(1f, 0f, y / 63f);
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            fadeSprite = Sprite.Create(texture, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
            return fadeSprite;
        }

        void BuildOptions()
        {
            optionsSheet = NewRect("Options", transform);
            Stretch(optionsSheet);
            var dim = NewImage("Dim", optionsSheet, null, new Color(0.02f, 0.02f, 0.02f, 0.93f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var sheet = NewImage("Sheet", optionsSheet, null, new Color(0.09f, 0.1f, 0.08f, 0.98f));
            // Tall enough for eight rows (language, four sliders, three switches) at 120px plus spacing, which
            // is about 1070 reference pixels; the old 60% sheet held only about 830 and clipped the switches.
            sheet.rectTransform.anchorMin = new Vector2(0.06f, 0.06f);
            sheet.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
            sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;
            sheet.raycastTarget = true;

            var heading = NewLocalizedText("Heading", sheet.rectTransform, 56, Ember, "options.title");
            heading.AsHeading(8f);
            heading.rectTransform.anchorMin = new Vector2(0f, 0.86f);
            heading.rectTransform.anchorMax = new Vector2(1f, 0.98f);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;

            var rows = NewRect("Rows", sheet.rectTransform);
            rows.anchorMin = new Vector2(0.06f, 0.14f);
            rows.anchorMax = new Vector2(0.94f, 0.85f);
            rows.offsetMin = rows.offsetMax = Vector2.zero;
            // Rows ask for 120px but accept as little as 72. On a tall phone they get their full height; on
            // a tablet or a short window the layout group squeezes them evenly instead of letting the last
            // row slide underneath the close button.
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            AddLanguageRow(rows);
            AddSliderRow(rows, "options.music", Core.GameServices.Settings.MusicVolume, value =>
                Core.GameServices.Settings.SetVolumes(value, Core.GameServices.Settings.SfxVolume, Core.GameServices.Settings.VoiceVolume));
            AddSliderRow(rows, "options.sfx", Core.GameServices.Settings.SfxVolume, value =>
                Core.GameServices.Settings.SetVolumes(Core.GameServices.Settings.MusicVolume, value, Core.GameServices.Settings.VoiceVolume));
            AddSliderRow(rows, "options.voice", Core.GameServices.Settings.VoiceVolume, value =>
                Core.GameServices.Settings.SetVolumes(Core.GameServices.Settings.MusicVolume, Core.GameServices.Settings.SfxVolume, value));
            AddSliderRow(rows, "options.textSpeed", Mathf.InverseLerp(10f, 90f, Core.GameServices.Settings.TextSpeed), value =>
                Core.GameServices.Settings.SetTextSpeed(Mathf.Lerp(10f, 90f, value)));
            AddToggleRow(rows, "options.shake", Core.GameServices.Settings.ScreenShake,
                Core.GameServices.Settings.SetScreenShake);
            AddToggleRow(rows, "options.leftHanded", Core.GameServices.Settings.LeftHanded,
                Core.GameServices.Settings.SetLeftHanded);
            AddToggleRow(rows, "options.questArrow", Core.GameServices.Settings.QuestArrow,
                Core.GameServices.Settings.SetQuestArrow);
            AddToggleRow(rows, "options.subtitles", Core.GameServices.Settings.Subtitles,
                Core.GameServices.Settings.SetSubtitles);

            var close = NewImage("Close", sheet.rectTransform, null, new Color(0.14f, 0.13f, 0.1f, 1f));
            close.raycastTarget = true;
            close.rectTransform.anchorMin = new Vector2(0.25f, 0.02f);
            close.rectTransform.anchorMax = new Vector2(0.75f, 0.1f);
            close.rectTransform.offsetMin = close.rectTransform.offsetMax = Vector2.zero;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleOptions(false));
            NewLocalizedText("Label", close.rectTransform, 40, Bone, "options.close");

            optionsSheet.gameObject.SetActive(false);
        }

        static void AllowToShrink(GameObject row)
        {
            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 120f;
            element.minHeight = 72f;
        }

        void AddLanguageRow(RectTransform parent)
        {
            var row = NewImage("Language", parent, null, new Color(0.12f, 0.13f, 0.1f, 0.9f));
            row.rectTransform.sizeDelta = new Vector2(0f, 120f);
            AllowToShrink(row.gameObject);

            var label = NewLocalizedText("Label", row.rectTransform, 36, Bone, "options.language");
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.offsetMin = new Vector2(24f, 0f);

            string[] languages = Localization.LocalizationService.SupportedLanguages;
            for (int i = 0; i < languages.Length; i++)
            {
                string language = languages[i];
                var option = NewImage(language, row.rectTransform, null, new Color(0.18f, 0.17f, 0.13f, 1f));
                option.raycastTarget = true;
                option.rectTransform.anchorMin = new Vector2(0.55f + i * 0.22f, 0.15f);
                option.rectTransform.anchorMax = new Vector2(0.75f + i * 0.22f, 0.85f);
                option.rectTransform.offsetMin = option.rectTransform.offsetMax = Vector2.zero;
                option.gameObject.AddComponent<Button>().onClick.AddListener(() => Core.GameServices.Settings.SetLanguage(language));
                var text = NewText("Label", option.rectTransform, 34, language == Core.GameServices.Settings.Language ? Ember : Bone);
                text.text = language.ToUpperInvariant();
            }
        }

        /// <summary>An on/off row: the label, and a plate that reads ON or OFF and flips when tapped.</summary>
        void AddToggleRow(RectTransform parent, string key, bool value, Action<bool> onChanged)
        {
            var row = NewImage(key, parent, null, new Color(0.12f, 0.13f, 0.1f, 0.9f));
            row.rectTransform.sizeDelta = new Vector2(0f, 120f);
            AllowToShrink(row.gameObject);

            var label = NewLocalizedText("Label", row.rectTransform, 36, Bone, key);
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.offsetMin = new Vector2(24f, 0f);

            var plate = NewImage("Switch", row.rectTransform, null, new Color(0.2f, 0.19f, 0.15f, 1f));
            plate.raycastTarget = true;
            plate.rectTransform.anchorMin = new Vector2(0.72f, 0.22f);
            plate.rectTransform.anchorMax = new Vector2(0.95f, 0.78f);
            plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;

            var state = NewText("State", plate.rectTransform, 34, Bone);
            bool current = value;
            void Paint()
            {
                state.text = Core.GameServices.Localization.Get(current ? "options.on" : "options.off");
                state.color = current ? Ember : Bone;
                plate.color = current ? new Color(0.3f, 0.22f, 0.1f, 1f) : new Color(0.2f, 0.19f, 0.15f, 1f);
            }
            Paint();

            plate.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                current = !current;
                onChanged(current);
                Paint();
            });
        }

        void AddSliderRow(RectTransform parent, string key, float value, Action<float> onChanged)
        {
            var row = NewImage(key, parent, null, new Color(0.12f, 0.13f, 0.1f, 0.9f));
            row.rectTransform.sizeDelta = new Vector2(0f, 120f);
            AllowToShrink(row.gameObject);

            var label = NewLocalizedText("Label", row.rectTransform, 36, Bone, key);
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.offsetMin = new Vector2(24f, 0f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.rectTransform, false);
            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin = new Vector2(0.45f, 0.3f);
            sliderRect.anchorMax = new Vector2(0.95f, 0.7f);
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

            var background = NewImage("Background", sliderRect, null, new Color(0.2f, 0.19f, 0.15f, 1f));
            Stretch(background.rectTransform);
            var fillArea = NewRect("FillArea", sliderRect);
            Stretch(fillArea);
            var fill = NewImage("Fill", fillArea, null, Ember);
            Stretch(fill.rectTransform);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChanged(v));
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return text;
        }

        static TMP_Text NewLocalizedText(string name, Transform parent, float size, Color color, string key)
        {
            TMP_Text text = NewText(name, parent, size, color);
            text.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
