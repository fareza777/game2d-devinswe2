using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oathfire.Controls
{
    /// <summary>
    /// Round fantasy-style touch button: glow, dark plate, circular icon, radial cooldown and bronze ring.
    /// Raises Pressed/Released and animates a press punch and golden glow.
    /// </summary>
    public class MedallionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        static readonly Color RingTint = new Color(1f, 0.88f, 0.62f, 1f);
        static readonly Color GlowTint = new Color(1f, 0.72f, 0.25f, 1f);
        static readonly Color DeniedTint = new Color(1f, 0.25f, 0.2f, 1f);
        const float PressedScale = 0.9f;
        const float AnimationSpeed = 14f;
        const float DeniedFlashSeconds = 0.25f;

        // The bronze ring (UISprites_99, 139x134) has a hole 88px across: 63% of the button. The pack's dark disc is
        // an uneven blob about 58% across, which left a ring of the world showing between icon and bronze. The plate
        // is a true circle a little wider than the hole, so its edge always tucks under the ring.
        const float PlateSize = 0.68f;
        static Sprite circle;

        Image glow;
        Image icon;
        Image cooldownFill;
        TMP_Text cooldownText;
        RectTransform visualRoot;
        bool isPressed;
        float glowAlpha;
        float deniedUntil;

        public event Action Pressed;
        public event Action Released;

        public static MedallionButton Create(string name, Transform parent, MobileUiSkin skin, Sprite iconSprite, float size,
            float iconScale = 1.02f, Color? plateTint = null)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(size, size);
            var hitArea = root.GetComponent<Image>();
            hitArea.color = Color.clear; // generous invisible touch target
            hitArea.raycastTarget = true;

            var button = root.AddComponent<MedallionButton>();
            button.visualRoot = NewRect("Visual", rootRect, 1f);

            button.glow = NewImage("Glow", button.visualRoot, skin.glow, GlowTint, 1.45f);
            button.glow.color = new Color(GlowTint.r, GlowTint.g, GlowTint.b, 0f);

            Image plate = NewImage("Plate", button.visualRoot, Circle(), plateTint ?? new Color(0.12f, 0.1f, 0.08f, 0.96f), PlateSize);
            plate.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            button.icon = NewImage("Icon", plate.rectTransform, iconSprite, Color.white, iconScale);

            button.cooldownFill = NewImage("Cooldown", plate.rectTransform, Circle(), new Color(0f, 0f, 0f, 0.7f), 1f);
            button.cooldownFill.type = Image.Type.Filled;
            button.cooldownFill.fillMethod = Image.FillMethod.Radial360;
            button.cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            button.cooldownFill.fillClockwise = false;
            button.cooldownFill.fillAmount = 0f;

            NewImage("Ring", button.visualRoot, skin.ring, RingTint, 1f);

            var textGo = new GameObject("CooldownText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(button.visualRoot, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size * 0.28f;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            button.cooldownText = text;
            text.text = string.Empty;
            return button;
        }

        /// <summary>Shows remaining cooldown (0..1 fill) and seconds; pass 0 when ready.</summary>
        public void SetCooldown(float fill, float secondsRemaining)
        {
            cooldownFill.fillAmount = Mathf.Clamp01(fill);
            cooldownText.text = fill > 0f && secondsRemaining >= 0.95f ? Mathf.CeilToInt(secondsRemaining).ToString() : string.Empty;
        }

        /// <summary>Swaps the picture, for a skill slot whose skill has changed.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon.sprite != sprite)
                icon.sprite = sprite;
        }

        /// <summary>Dims the icon when the action cannot be used (e.g. not enough mana).</summary>
        public void SetUsable(bool usable)
        {
            icon.color = usable ? Color.white : new Color(0.45f, 0.45f, 0.55f, 1f);
        }

        /// <summary>Brief red flash to tell the player the press was rejected.</summary>
        public void FlashDenied() => deniedUntil = Time.unscaledTime + DeniedFlashSeconds;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        void OnDisable() => Release();

        void Release()
        {
            if (!isPressed)
                return;
            isPressed = false;
            Released?.Invoke();
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime * AnimationSpeed;
            float targetScale = isPressed ? PressedScale : 1f;
            visualRoot.localScale = Vector3.one * Mathf.Lerp(visualRoot.localScale.x, targetScale, step);

            bool denied = Time.unscaledTime < deniedUntil;
            glowAlpha = Mathf.Lerp(glowAlpha, isPressed || denied ? 0.95f : 0f, step);
            Color tint = denied ? DeniedTint : GlowTint;
            glow.color = new Color(tint.r, tint.g, tint.b, glowAlpha);
        }

        /// <summary>A smooth-edged white circle, drawn once, used as the plate, the icon mask and the cooldown sweep.</summary>
        static Sprite Circle()
        {
            if (circle)
                return circle;
            const int Size = 256;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float radius = Size * 0.5f - 1f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(Size * 0.5f, Size * 0.5f));
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            circle = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        static RectTransform NewRect(string name, RectTransform parent, float relativeSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            SizeRelative(rect, relativeSize);
            return rect;
        }

        static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color, float relativeSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SizeRelative(image.rectTransform, relativeSize);
            return image;
        }

        static void SizeRelative(RectTransform rect, float relativeSize)
        {
            float inset = (1f - relativeSize) * 0.5f;
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rect) => SizeRelative(rect, 1f);
    }
}
