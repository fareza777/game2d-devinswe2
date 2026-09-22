using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oathfire.Cinematic
{
    /// <summary>
    /// Plays the woodcut panel cinematic: each panel pushes in slowly while its narration plays, with
    /// amber fire glows, drifting embers and skeletal watchers layered on top of the print in engine.
    /// Tap anywhere skips to the next panel; the Skip button ends the whole sequence.
    /// </summary>
    public class CinematicPlayer : MonoBehaviour, IPointerClickHandler
    {
        const float CrossFade = 0.9f;
        static readonly Color Ink = new Color(0.055f, 0.063f, 0.051f);
        static readonly Color Bone = new Color(0.87f, 0.84f, 0.76f);
        static readonly Color Ember = new Color(1f, 0.62f, 0.22f);

        [SerializeField] string cinematicId = "opening";
        [SerializeField] Sprite glowSprite;
        [SerializeField] Sprite watcherSprite;
        [SerializeField] Sprite emberSprite;

        CinematicData data;
        Image panelImage;
        Image previousImage;
        RectTransform panelHolder;
        RectTransform overlayHolder;
        TMP_Text subtitle;
        TMP_Text speakerLabel;
        CanvasGroup subtitleGroup;
        Button skipButton;
        bool skipRequested;
        bool panelSkipRequested;
        readonly List<Image> overlays = new List<Image>();

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        IEnumerator Start()
        {
            var asset = Resources.Load<TextAsset>($"Cinematic/{cinematicId}");
            if (!asset)
            {
                Debug.LogError($"[Cinematic] Missing Resources/Cinematic/{cinematicId}.json");
                yield break;
            }
            data = JsonUtility.FromJson<CinematicData>(asset.text);

            var music = Resources.Load<AudioClip>($"Music/{data.musicClip}");
            if (music)
                Core.GameServices.Audio.PlayMusic(music);

            foreach (CinematicPanel panel in data.panels)
            {
                if (skipRequested)
                    break;
                yield return PlayPanelRoutine(panel);
            }

            Core.GameServices.Audio.StopVoice();
            string next = string.IsNullOrEmpty(data.nextScene) ? Core.GameBootstrap.TitleScene : data.nextScene;
            // A story that ends hands back to the title: the one natural interstitial slot.
            if (next == Core.GameBootstrap.TitleScene)
                Core.GameServices.Ads?.ShowInterstitialIfReady();
            yield return Core.GameServices.Flow.LoadSceneRoutine(next);
        }

        IEnumerator PlayPanelRoutine(CinematicPanel panel)
        {
            panelSkipRequested = false;
            var sprite = Resources.Load<Sprite>($"Cinematic/{panel.image}");
            if (!sprite)
                Debug.LogWarning($"[Cinematic] Missing panel art Resources/Cinematic/{panel.image}");

            previousImage.sprite = panelImage.sprite;
            previousImage.color = Color.white;
            panelImage.sprite = sprite;
            panelImage.color = new Color(1f, 1f, 1f, 0f);

            ClearOverlays();
            BuildOverlays(panel);

            float elapsed = 0f;
            float duration = 0f;
            Coroutine narration = StartCoroutine(NarrateRoutine(panel, length => duration = length));

            while (narration != null && (duration <= 0f || elapsed < duration) && !panelSkipRequested && !skipRequested)
            {
                elapsed += Time.deltaTime;
                float k = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;

                panelHolder.localScale = Vector3.one * Mathf.Lerp(panel.zoomFrom, panel.zoomTo, k);
                panelHolder.anchorMin = new Vector2(0f, Mathf.Lerp(panel.panFromY, panel.panToY, k));
                panelHolder.anchorMax = new Vector2(1f, 1f + Mathf.Lerp(panel.panFromY, panel.panToY, k));

                float fade = Mathf.Clamp01(elapsed / CrossFade);
                panelImage.color = new Color(1f, 1f, 1f, fade);
                previousImage.color = new Color(1f, 1f, 1f, 1f - fade);

                UpdateOverlays(panel, elapsed);
                yield return null;
            }

            StopCoroutine(narration);
            panelImage.color = Color.white;
            previousImage.color = new Color(1f, 1f, 1f, 0f);
        }

        IEnumerator NarrateRoutine(CinematicPanel panel, System.Action<float> reportDuration)
        {
            float total = 0f;
            foreach (CinematicLine line in panel.lines)
            {
                var clip = Resources.Load<AudioClip>($"Voice/{line.voiceClip}");
                float length = clip ? Core.GameServices.Audio.PlayVoice(clip) : 2.6f;
                if (!clip && !string.IsNullOrEmpty(line.voiceClip))
                    Debug.LogWarning($"[Cinematic] Missing voice clip Resources/Voice/{line.voiceClip}");

                ShowSubtitle(line);
                total += length + line.hold;
                reportDuration(total + 0.6f);

                float wait = length + line.hold;
                for (float t = 0f; t < wait; t += Time.deltaTime)
                {
                    if (panelSkipRequested || skipRequested)
                        yield break;
                    yield return null;
                }
                yield return HideSubtitleRoutine();
            }
        }

        void ShowSubtitle(CinematicLine line)
        {
            if (!Core.GameServices.Settings.Subtitles)
            {
                subtitleGroup.alpha = 0f;
                return;
            }
            speakerLabel.text = string.IsNullOrEmpty(line.speaker) ? string.Empty : Core.GameServices.Localization.Get($"speaker.{line.speaker}");
            subtitle.text = Core.GameServices.Localization.Get(line.textKey);
            subtitleGroup.alpha = 1f;
        }

        IEnumerator HideSubtitleRoutine()
        {
            for (float t = 0.25f; t > 0f; t -= Time.deltaTime)
            {
                subtitleGroup.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            subtitleGroup.alpha = 0f;
        }

        void BuildOverlays(CinematicPanel panel)
        {
            if (panel.glows != null)
            {
                foreach (CinematicGlow glow in panel.glows)
                {
                    Image image = NewOverlay(glowSprite, new Color(Ember.r, Ember.g, Ember.b, 0.85f * glow.intensity));
                    Place(image.rectTransform, glow.x, glow.y, glow.radius);
                    image.name = glow.flicker ? "Glow(flicker)" : "Glow";
                }
            }

            if (panel.watchers != null)
            {
                foreach (CinematicWatcher watcher in panel.watchers)
                {
                    Image image = NewOverlay(watcherSprite, new Color(0.04f, 0.05f, 0.04f, 0f));
                    Place(image.rectTransform, watcher.x, watcher.y, 0.055f * watcher.scale);
                    image.name = $"Watcher@{watcher.appearAt:0.0}";
                    image.preserveAspect = true;
                }
            }

            if (panel.emberParticles)
            {
                for (int i = 0; i < 18; i++)
                {
                    Image image = NewOverlay(emberSprite, new Color(Ember.r, Ember.g, Ember.b, Random.Range(0.25f, 0.7f)));
                    Place(image.rectTransform, Random.value, Random.value * 0.8f, Random.Range(0.004f, 0.009f));
                    image.name = "Ember";
                }
            }
        }

        void UpdateOverlays(CinematicPanel panel, float elapsed)
        {
            int watcherIndex = 0;
            foreach (Image image in overlays)
            {
                if (image.name.StartsWith("Glow"))
                {
                    float flicker = image.name.Contains("flicker") ? 0.82f + 0.18f * Mathf.PerlinNoise(elapsed * 6f, image.GetInstanceID() % 100) : 1f;
                    Color color = image.color;
                    image.color = new Color(color.r, color.g, color.b, 0.85f * flicker);
                }
                else if (image.name.StartsWith("Watcher"))
                {
                    CinematicWatcher watcher = panel.watchers[watcherIndex++];
                    float alpha = Mathf.Clamp01((elapsed - watcher.appearAt) / 1.2f);
                    image.color = new Color(0.04f, 0.05f, 0.04f, alpha);
                }
                else if (image.name == "Ember")
                {
                    RectTransform rect = image.rectTransform;
                    Vector2 position = rect.anchorMin;
                    float drift = Mathf.Sin(elapsed * 1.5f + image.GetInstanceID() % 10) * 0.0004f;
                    position.y += Time.deltaTime * 0.02f;
                    position.x += drift;
                    if (position.y > 1f)
                        position.y = 0f;
                    float size = rect.anchorMax.x - rect.anchorMin.x;
                    rect.anchorMin = position;
                    rect.anchorMax = position + new Vector2(size, size * 1.7f);
                }
            }
        }

        Image NewOverlay(Sprite sprite, Color color)
        {
            var go = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(overlayHolder, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            overlays.Add(image);
            return image;
        }

        static void Place(RectTransform rect, float x, float y, float size)
        {
            rect.anchorMin = new Vector2(x - size * 0.5f, y - size * 0.5f);
            rect.anchorMax = new Vector2(x + size * 0.5f, y + size * 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void ClearOverlays()
        {
            foreach (Image image in overlays)
                if (image)
                    Destroy(image.gameObject);
            overlays.Clear();
        }

        public void OnPointerClick(PointerEventData eventData) => panelSkipRequested = true;

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            var background = NewImage("Background", transform, null, Ink);
            Stretch(background.rectTransform);
            background.raycastTarget = true;

            panelHolder = NewRect("PanelHolder", transform);
            Stretch(panelHolder);
            previousImage = NewImage("PanelPrevious", panelHolder, null, new Color(1f, 1f, 1f, 0f));
            Stretch(previousImage.rectTransform);
            previousImage.preserveAspect = false;
            panelImage = NewImage("Panel", panelHolder, null, Color.white);
            Stretch(panelImage.rectTransform);
            panelImage.preserveAspect = false;

            overlayHolder = NewRect("Overlays", panelHolder);
            Stretch(overlayHolder);

            var vignette = NewImage("Vignette", transform, null, new Color(0f, 0f, 0f, 0.25f));
            Stretch(vignette.rectTransform);

            var subtitleRoot = NewRect("Subtitle", transform);
            subtitleRoot.anchorMin = new Vector2(0.06f, 0.04f);
            subtitleRoot.anchorMax = new Vector2(0.94f, 0.26f);
            subtitleRoot.offsetMin = subtitleRoot.offsetMax = Vector2.zero;
            subtitleGroup = subtitleRoot.gameObject.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;

            var plate = NewImage("Plate", subtitleRoot, null, new Color(0.04f, 0.05f, 0.04f, 0.72f));
            Stretch(plate.rectTransform);

            speakerLabel = NewText("Speaker", subtitleRoot, 34, Ember);
            speakerLabel.alignment = TextAlignmentOptions.TopLeft;
            speakerLabel.rectTransform.offsetMin = new Vector2(28f, 0f);
            speakerLabel.rectTransform.offsetMax = new Vector2(-28f, -16f);

            subtitle = NewText("Line", subtitleRoot, 42, Bone);
            subtitle.alignment = TextAlignmentOptions.TopLeft;
            subtitle.rectTransform.offsetMin = new Vector2(28f, 20f);
            subtitle.rectTransform.offsetMax = new Vector2(-28f, -60f);

            var skipImage = NewImage("Skip", transform, null, new Color(0.08f, 0.09f, 0.07f, 0.8f));
            skipImage.raycastTarget = true;
            RectTransform skipRect = skipImage.rectTransform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.sizeDelta = new Vector2(220f, 84f);
            skipRect.anchoredPosition = new Vector2(-30f, -40f);
            skipButton = skipImage.gameObject.AddComponent<Button>();
            skipButton.onClick.AddListener(() => skipRequested = true);
            var skipLabel = NewText("Label", skipRect, 32, Bone);
            skipLabel.text = "SKIP";
            skipLabel.fontStyle = FontStyles.Bold;
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
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform);
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
