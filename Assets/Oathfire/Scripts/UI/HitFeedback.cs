using System.Collections;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// What a hit feels like: a soft red bleed at the screen edges, a short camera kick, and a low thud.
    /// Replaces the asset's flat red rectangle, which read as a UI bug rather than damage.
    /// The bleed also sits and breathes while health is critical, so low HP is felt without watching the bar.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        const float FlashSeconds = 0.42f;
        const float ShakeSeconds = 0.18f;
        const float ShakeStrength = 0.13f;
        const float CriticalHealth = 0.28f;

        [SerializeField] Sprite vignetteSprite;
        [SerializeField] AudioClip hitSound;

        Image vignette;
        Transform cameraTransform;
        Coroutine flashRoutine;
        PlayerHealth trackedPlayer;
        float pulse;

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        void Update()
        {
            // PlayerHealth is created with the scene, so attach as soon as it appears.
            if (trackedPlayer != PlayerHealth.Instance)
            {
                if (trackedPlayer)
                    trackedPlayer.OnDamageTaken -= OnDamage;
                trackedPlayer = PlayerHealth.Instance;
                if (trackedPlayer)
                    trackedPlayer.OnDamageTaken += OnDamage;
            }

            if (!cameraTransform && Camera.main)
                cameraTransform = Camera.main.transform;

            UpdateCriticalPulse();
        }

        void OnDestroy()
        {
            if (trackedPlayer)
                trackedPlayer.OnDamageTaken -= OnDamage;
        }

        void OnDamage(int amount)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            float weight = Mathf.Clamp01(amount / 25f);
            flashRoutine = StartCoroutine(FlashRoutine(0.35f + weight * 0.45f));
            if (Core.GameServices.Settings.ScreenShake)
                StartCoroutine(ShakeRoutine(ShakeStrength * (0.6f + weight)));
            if (hitSound)
                Core.GameServices.Audio.PlaySfx(hitSound, 0.1f);
            else
                Core.GameServices.Audio.PlaySfx("hit", 0.8f, 0.1f);
        }

        IEnumerator FlashRoutine(float peak)
        {
            for (float t = 0f; t < FlashSeconds; t += Time.deltaTime)
            {
                float k = 1f - t / FlashSeconds;
                SetVignette(peak * k * k);
                yield return null;
            }
            SetVignette(0f);
            flashRoutine = null;
        }

        IEnumerator ShakeRoutine(float strength)
        {
            if (!cameraTransform)
                yield break;
            Vector3 origin = cameraTransform.localPosition;
            for (float t = 0f; t < ShakeSeconds; t += Time.deltaTime)
            {
                float decay = 1f - t / ShakeSeconds;
                Vector2 offset = Random.insideUnitCircle * strength * decay;
                cameraTransform.localPosition = origin + new Vector3(offset.x, offset.y * 0.6f, 0f);
                yield return null;
            }
            cameraTransform.localPosition = origin;
        }

        void UpdateCriticalPulse()
        {
            if (flashRoutine != null || !trackedPlayer)
                return;
            float ratio = trackedPlayer.currentHealth / (float)Mathf.Max(1, trackedPlayer.maxHealth);
            if (ratio > CriticalHealth || PlayerHealth.IsPlayerDead)
            {
                if (pulse > 0f)
                    SetVignette(pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime));
                return;
            }
            // Heartbeat: stronger the closer to death.
            float urgency = Mathf.InverseLerp(CriticalHealth, 0f, ratio);
            pulse = (0.18f + 0.22f * urgency) * (0.55f + 0.45f * Mathf.Sin(Time.time * (3f + urgency * 3f)));
            SetVignette(Mathf.Max(0f, pulse));
        }

        void SetVignette(float alpha) => vignette.color = new Color(0.62f, 0.09f, 0.07f, Mathf.Clamp01(alpha));

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480; // above the world, below menus
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            vignette = go.GetComponent<Image>();
            vignette.sprite = vignetteSprite;
            vignette.raycastTarget = false;
            vignette.type = Image.Type.Simple;
            RectTransform rect = vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Overscan so the soft edge never shows a seam on any aspect ratio.
            rect.offsetMin = new Vector2(-40f, -40f);
            rect.offsetMax = new Vector2(40f, 40f);
            SetVignette(0f);
        }
    }
}
