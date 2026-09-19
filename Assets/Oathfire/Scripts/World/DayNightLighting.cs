using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Oathfire.World
{
    /// <summary>
    /// Day and night should feel like different places. By day the scene takes its location's own light. When the
    /// night assault begins the world sinks over a few seconds into a cold, dim blue, the village torches kindle and
    /// flicker, and the edges of the screen close in; at dawn it all eases back to warm daylight.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class DayNightLighting : MonoBehaviour
    {
        const float TransitionSeconds = 4f;

        [SerializeField] Color dayColour = new Color(1f, 0.96f, 0.88f);
        [SerializeField] float dayIntensity = 0.95f;
        [SerializeField] Color nightColour = new Color(0.36f, 0.45f, 0.78f);
        [SerializeField] float nightIntensity = 0.3f;
        [SerializeField] Sprite vignetteSprite;
        [SerializeField] List<Light2D> torches = new List<Light2D>();

        Light2D global;
        Image vignette;
        float night;

        /// <summary>0 at full day, 1 at full night; read by the smoke test.</summary>
        public float Night => night;

        public static DayNightLighting Instance { get; private set; }

        public void Configure(Color colour, float intensity, Sprite vignetteArt)
        {
            dayColour = colour;
            dayIntensity = intensity;
            vignetteSprite = vignetteArt;
        }

        public void AddTorch(Light2D torch) => torches.Add(torch);

        void Awake()
        {
            Instance = this;
            global = GetComponent<Light2D>();
            BuildVignette();
            Apply();
        }

        void Update()
        {
            bool nightRunning = NightDirector.Instance && NightDirector.Instance.IsNightRunning;
            float target = nightRunning ? 1f : 0f;
            if (!Mathf.Approximately(night, target))
                night = Mathf.MoveTowards(night, target, Time.deltaTime / TransitionSeconds);
            Apply();
        }

        void Apply()
        {
            float eased = night * night * (3f - 2f * night);
            global.color = Color.Lerp(dayColour, nightColour, eased);
            global.intensity = Mathf.Lerp(dayIntensity, nightIntensity, eased);

            for (int i = 0; i < torches.Count; i++)
            {
                if (!torches[i])
                    continue;
                // Each torch flickers on its own rhythm; by day they are barely a glow.
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3.1f + i * 1.7f, i * 0.37f);
                torches[i].intensity = Mathf.Lerp(0.12f, 1.1f, eased) * flicker;
            }

            if (vignette)
                vignette.color = new Color(0.02f, 0.03f, 0.08f, 0.55f * eased);
        }

        void BuildVignette()
        {
            if (!vignetteSprite)
                return;
            var canvasGo = new GameObject("NightVignette", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // over the world, under the HUD and controls
            var imageGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            vignette = imageGo.GetComponent<Image>();
            vignette.sprite = vignetteSprite;
            vignette.raycastTarget = false;
            vignette.color = Color.clear;
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
