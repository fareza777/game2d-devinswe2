using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oathfire.Controls
{
    /// <summary>
    /// Medallion joystick that jumps to wherever the thumb lands inside its zone, dims at rest and
    /// glows while held. Value is analog, magnitude 0..1.
    /// </summary>
    public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float IdleAlpha = 0.55f;
        const float FadeSpeed = 12f;
        const float DeadZone = 0.12f;

        RectTransform zone;
        RectTransform baseRect;
        RectTransform knob;
        CanvasGroup baseGroup;
        Image glow;
        float radius;
        Vector2 restPosition;
        int activePointerId = int.MinValue;

        public Vector2 Value { get; private set; }
        bool IsHeld => activePointerId != int.MinValue;

        public static FloatingJoystick Create(RectTransform zoneRect, MobileUiSkin skin, float radiusPixels, Vector2 restAnchoredPosition)
        {
            var zoneImage = zoneRect.gameObject.AddComponent<Image>();
            zoneImage.color = Color.clear; // invisible, receives touches

            var baseGo = new GameObject("Base", typeof(RectTransform), typeof(CanvasGroup));
            baseGo.transform.SetParent(zoneRect, false);
            var baseRect = (RectTransform)baseGo.transform;
            baseRect.anchorMin = baseRect.anchorMax = Vector2.zero;
            baseRect.sizeDelta = Vector2.one * radiusPixels * 2.3f;

            var glow = AddImage("Glow", baseRect, skin.glow, new Color(1f, 0.72f, 0.25f, 0f), 1.35f);
            AddImage("Plate", baseRect, skin.disc, new Color(0.08f, 0.07f, 0.06f, 0.55f), 0.86f);
            AddImage("Ring", baseRect, skin.ring, new Color(1f, 0.88f, 0.62f, 1f), 1f);
            var knob = AddImage("Knob", baseRect, skin.medallion, Color.white, 0.44f).rectTransform;

            var stick = zoneRect.gameObject.AddComponent<FloatingJoystick>();
            stick.zone = zoneRect;
            stick.baseRect = baseRect;
            stick.baseGroup = baseGo.GetComponent<CanvasGroup>();
            stick.baseGroup.blocksRaycasts = false;
            stick.knob = knob;
            stick.glow = glow;
            stick.radius = radiusPixels;
            stick.restPosition = restAnchoredPosition;
            stick.ResetStick();
            return stick;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld)
                return;
            activePointerId = eventData.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 local))
                baseRect.anchoredPosition = local - zone.rect.min;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            knob.anchoredPosition = clamped;
            Vector2 normalized = clamped / radius;
            Value = normalized.magnitude < DeadZone ? Vector2.zero : normalized;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
                ResetStick();
        }

        void OnDisable() => ResetStick();

        void Update()
        {
            float step = Time.unscaledDeltaTime * FadeSpeed;
            baseGroup.alpha = Mathf.Lerp(baseGroup.alpha, IsHeld ? 1f : IdleAlpha, step);
            var c = glow.color;
            glow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(c.a, IsHeld ? 0.6f * Mathf.Max(0.4f, Value.magnitude) : 0f, step));
        }

        void ResetStick()
        {
            activePointerId = int.MinValue;
            Value = Vector2.zero;
            if (baseRect) baseRect.anchoredPosition = restPosition;
            if (knob) knob.anchoredPosition = Vector2.zero;
        }

        static Image AddImage(string name, RectTransform parent, Sprite sprite, Color color, float relativeSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            float inset = (1f - relativeSize) * 0.5f;
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }
    }
}
