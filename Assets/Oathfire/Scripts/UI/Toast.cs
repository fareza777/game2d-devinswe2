using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// Short status lines that rise and fade near the top of the screen: materials missing, a building
    /// raised, night falling. Queued so two events never overlap.
    /// </summary>
    public class Toast : MonoBehaviour
    {
        const float ShowSeconds = 2.2f;
        static Toast instance;

        readonly Queue<string> pending = new Queue<string>();
        TMP_Text label;
        CanvasGroup group;
        RectTransform holderRect;
        bool showing;

        public static void Show(string localizationKey) => ShowText(Core.GameServices.Localization.Get(localizationKey));

        /// <summary>A line that is already in the player's language, for messages with numbers in them.</summary>
        public static void ShowText(string text)
        {
            EnsureExists();
            instance.pending.Enqueue(text);
            if (!instance.showing)
                instance.StartCoroutine(instance.DrainRoutine());
        }

        static void EnsureExists()
        {
            if (instance)
                return;
            var go = new GameObject("Toast");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Toast>();
            instance.Build();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;

            var holder = new GameObject("Holder", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            holder.transform.SetParent(transform, false);
            var rect = (RectTransform)holder.transform;
            holderRect = rect;
            // Measured from the top like the HUD lines above it (objective, night, event), so the stack never
            // overlaps whatever the screen's proportions: a fraction of the height drifted into them on short screens.
            // Kept left of the right-hand column (PACK, BOOK, USE), which a full-width line ran into on short screens.
            rect.anchorMin = new Vector2(0.04f, 1f);
            rect.anchorMax = new Vector2(0.72f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 90f);
            rect.anchoredPosition = new Vector2(0f, -620f);
            holder.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.05f, 0.82f);
            group = holder.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(holder.transform, false);
            label = textGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 38;
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 38f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.87f, 0.84f, 0.76f);
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Under the HUD lines during play; across the very top while a card, shop or book is open, so the message
        /// never sits on the middle of the page being read.
        /// </summary>
        void Place()
        {
            bool overPage = !Controls.MobileControls.GameplayActive;
            holderRect.anchorMin = new Vector2(overPage ? 0.08f : 0.04f, 1f);
            holderRect.anchorMax = new Vector2(overPage ? 0.92f : 0.72f, 1f);
            holderRect.anchoredPosition = new Vector2(0f, overPage ? -8f : -620f);
            holderRect.sizeDelta = new Vector2(0f, overPage ? 70f : 90f);
        }

        IEnumerator DrainRoutine()
        {
            showing = true;
            while (pending.Count > 0)
            {
                label.text = pending.Dequeue();
                Place();
                yield return FadeRoutine(0f, 1f, 0.2f);
                yield return new WaitForSeconds(ShowSeconds);
                yield return FadeRoutine(1f, 0f, 0.35f);
            }
            showing = false;
        }

        IEnumerator FadeRoutine(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
