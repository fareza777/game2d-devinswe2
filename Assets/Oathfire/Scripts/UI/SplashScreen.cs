using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The opening seconds: embers rise out of the dark, the oathfire sigil burns itself into being around its
    /// flame, the name of the game settles beneath it, and everything breathes once before the title. Short
    /// enough to never be skipped in anger, strong enough to say what kind of game this is.
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.02f, 0.02f, 0.018f);
        static readonly Color Ember = new Color(1f, 0.6f, 0.2f);
        const int EmberCount = 34;

        [SerializeField] Sprite glowSprite;
        [SerializeField] Sprite sigilSprite;
        [SerializeField] Sprite logoSprite;
        [SerializeField] AudioClip emberSound;

        class Spark
        {
            public RectTransform rect;
            public Image image;
            public Vector2 start;
            public float speed, sway, phase, size, delay;
        }

        readonly List<Spark> sparks = new List<Spark>();
        CanvasGroup group;
        Image halo;
        Image sigil;
        Image sigilShade;
        Image logo;
        RectTransform canvasRect;

        void Awake() => Build();

        public IEnumerator PlayRoutine(float seconds)
        {
            Core.GameServices.EnsureCreated();
            if (emberSound)
                Core.GameServices.Audio.PlaySfx(emberSound, 0f);
            else
                Core.GameServices.Audio.PlaySfx("ember", 1f, 0f);

            float total = Mathf.Max(3.6f, seconds);
            float kindle = total * 0.18f;   // sparks alone in the dark
            float burn = total * 0.3f;      // the sigil burns in
            float name = total * 0.22f;     // the name settles
            float hold = total * 0.16f;
            float fade = total * 0.14f;

            float clock = 0f;
            for (float t = 0f; t < kindle; t += Time.unscaledDeltaTime)
            {
                clock += Time.unscaledDeltaTime;
                AnimateSparks(clock, Mathf.Clamp01(t / kindle));
                halo.color = new Color(Ember.r, Ember.g, Ember.b, 0.35f * Eased(t / kindle));
                yield return null;
            }

            for (float t = 0f; t < burn; t += Time.unscaledDeltaTime)
            {
                clock += Time.unscaledDeltaTime;
                float k = Eased(t / burn);
                AnimateSparks(clock, 1f);
                // The ring is drawn round in fire: a radial wipe, a warm tint cooling to its true colours.
                sigil.fillAmount = k;
                sigil.color = Color.Lerp(new Color(1f, 0.72f, 0.35f, 1f), Color.white, k);
                sigil.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
                sigilShade.color = new Color(0f, 0f, 0f, 0.55f * (1f - k));
                halo.color = new Color(Ember.r, Ember.g, Ember.b, Mathf.Lerp(0.35f, 0.75f, k) * Flicker(clock));
                halo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, k);
                yield return null;
            }
            sigil.fillAmount = 1f;

            for (float t = 0f; t < name + hold; t += Time.unscaledDeltaTime)
            {
                clock += Time.unscaledDeltaTime;
                float k = Eased(Mathf.Clamp01(t / name));
                AnimateSparks(clock, 1f);
                logo.color = new Color(1f, 1f, 1f, k);
                logo.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, 0f, k));
                halo.color = new Color(Ember.r, Ember.g, Ember.b, 0.75f * Flicker(clock));
                yield return null;
            }

            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                clock += Time.unscaledDeltaTime;
                AnimateSparks(clock, 1f);
                group.alpha = 1f - Eased(t / fade);
                yield return null;
            }

            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        static float Eased(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        static float Flicker(float time) => 0.88f + 0.12f * Mathf.PerlinNoise(time * 6f, 0.3f);

        /// <summary>Embers drift up from below the sigil, swaying, brightening and burning out.</summary>
        void AnimateSparks(float time, float strength)
        {
            float height = canvasRect.rect.height;
            foreach (Spark spark in sparks)
            {
                float life = Mathf.Repeat((time - spark.delay) * spark.speed, 1f);
                if (time < spark.delay)
                {
                    spark.image.color = Color.clear;
                    continue;
                }
                float y = spark.start.y + life * height * 0.75f;
                float x = spark.start.x + Mathf.Sin(time * spark.sway + spark.phase) * 40f * life;
                spark.rect.anchoredPosition = new Vector2(x, y);
                float alpha = Mathf.Sin(life * Mathf.PI) * strength;
                spark.image.color = new Color(1f, Mathf.Lerp(0.8f, 0.45f, life), 0.25f, alpha);
                spark.rect.sizeDelta = Vector2.one * spark.size * (1f - life * 0.6f);
            }
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            group = gameObject.AddComponent<CanvasGroup>();
            canvasRect = (RectTransform)transform;

            Stretch(NewImage("Ground", transform, null, Ink).rectTransform);

            halo = NewImage("Halo", transform, glowSprite, new Color(Ember.r, Ember.g, Ember.b, 0f));
            Place(halo.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(1100f, 1100f));

            var random = new System.Random(7);
            for (int i = 0; i < EmberCount; i++)
            {
                Image image = NewImage($"Spark {i}", transform, glowSprite, Color.clear);
                var spark = new Spark
                {
                    rect = image.rectTransform,
                    image = image,
                    start = new Vector2((float)(random.NextDouble() - 0.5) * 520f, -520f + (float)random.NextDouble() * 200f),
                    speed = 0.22f + (float)random.NextDouble() * 0.25f,
                    sway = 1.2f + (float)random.NextDouble() * 2f,
                    phase = (float)random.NextDouble() * 6.28f,
                    size = 14f + (float)random.NextDouble() * 26f,
                    delay = (float)random.NextDouble() * 1.4f,
                };
                spark.rect.anchorMin = spark.rect.anchorMax = new Vector2(0.5f, 0.5f);
                sparks.Add(spark);
            }

            sigil = NewImage("Sigil", transform, sigilSprite, Color.white);
            sigil.preserveAspect = true;
            sigil.type = Image.Type.Filled;
            sigil.fillMethod = Image.FillMethod.Radial360;
            sigil.fillOrigin = (int)Image.Origin360.Bottom;
            sigil.fillAmount = 0f;
            Place(sigil.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(620f, 620f));
            sigilShade = NewImage("SigilShade", sigil.rectTransform, glowSprite, new Color(0f, 0f, 0f, 0.55f));
            Stretch(sigilShade.rectTransform);

            logo = NewImage("Name", transform, logoSprite, new Color(1f, 1f, 1f, 0f));
            logo.preserveAspect = true;
            RectTransform logoRect = logo.rectTransform;
            logoRect.anchorMin = new Vector2(0.06f, 0.2f);
            logoRect.anchorMax = new Vector2(0.94f, 0.36f);
            logoRect.offsetMin = logoRect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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
    }
}
