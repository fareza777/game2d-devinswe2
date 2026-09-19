using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The quest arrow. It circles the hero and points toward the next thing to do; once the goal is on screen
    /// it moves over the goal itself and bobs there. Amber for the main story, moss green for side work.
    /// Hidden while talking, in menus, near the goal, or when switched off in Options.
    /// </summary>
    public class QuestCompass : MonoBehaviour
    {
        const float OrbitRadius = 175f;
        const float ArriveDistance = 1.8f;
        const float RefreshSeconds = 0.4f;
        const float ScreenMargin = 90f;
        const float MarkerLift = 150f;
        const float Smoothing = 10f;
        const int SpriteSize = 96;

        static readonly Color SideQuest = new Color(0.52f, 0.74f, 0.62f);

        RectTransform canvasRect;
        RectTransform pivot;
        Image fill;
        Image glow;
        CanvasGroup group;
        Quests.QuestGuide.Waypoint waypoint;
        bool hasWaypoint;
        float nextRefresh;
        Vector2 shownPosition;
        float shownAngle;

        public bool IsShowing => group && group.alpha > 0.5f;
        public string GuidedQuestId => hasWaypoint ? waypoint.quest.id : string.Empty;

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        void Update()
        {
            PlayerHealth player = PlayerHealth.Instance;
            Camera view = Camera.main;
            bool allowed = Core.GameServices.Settings.QuestArrow && player && view && !PlayerHealth.IsPlayerDead
                && Controls.MobileControls.GameplayActive && !Dialogue.DialogueRunner.IsConversationActive;

            if (allowed && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                hasWaypoint = Quests.QuestGuide.TryFind(player.transform.position, out waypoint);
            }

            bool arrived = hasWaypoint && !waypoint.travel
                && Vector2.Distance(player ? player.transform.position : Vector3.zero, waypoint.position) < ArriveDistance;
            bool show = allowed && hasWaypoint && !arrived;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (!show)
                return;

            Place(player.transform.position, view);
            Color tint = waypoint.quest.mainQuest ? UiTheme.EmberBright : SideQuest;
            float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 4f) * 0.15f;
            fill.color = tint;
            glow.color = new Color(tint.r, tint.g, tint.b, 0.35f * pulse);
        }

        void Place(Vector3 heroWorld, Camera view)
        {
            Vector2 hero = ToCanvas(view, heroWorld);
            Vector2 goal = ToCanvas(view, waypoint.position);
            Rect bounds = canvasRect.rect;
            bool goalOnScreen = goal.x > bounds.xMin + ScreenMargin && goal.x < bounds.xMax - ScreenMargin
                && goal.y > bounds.yMin + ScreenMargin * 3f && goal.y < bounds.yMax - ScreenMargin * 3f;

            Vector2 target;
            float angle;
            if (goalOnScreen && !waypoint.travel)
            {
                // Over the goal, pointing down at it, with a gentle bob.
                target = goal + Vector2.up * (MarkerLift + Mathf.Sin(Time.unscaledTime * 3f) * 14f);
                angle = 180f;
            }
            else
            {
                Vector2 direction = (goal - hero).sqrMagnitude > 1f ? (goal - hero).normalized : Vector2.up;
                target = hero + Vector2.up * 40f + direction * OrbitRadius;
                angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            }

            float blend = 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            shownPosition = Vector2.Lerp(shownPosition, target, blend);
            shownAngle = Mathf.LerpAngle(shownAngle, angle, blend);
            pivot.anchoredPosition = shownPosition;
            pivot.localRotation = Quaternion.Euler(0f, 0f, shownAngle);
        }

        Vector2 ToCanvas(Camera view, Vector3 world)
        {
            Vector3 screen = view.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            return local;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 440; // under the HUD and controls, over the world
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            canvasRect = (RectTransform)transform;

            var pivotGo = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasGroup));
            pivotGo.transform.SetParent(transform, false);
            pivot = (RectTransform)pivotGo.transform;
            pivot.sizeDelta = new Vector2(84f, 84f);
            group = pivotGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            glow = Layer("Glow", Glow(), 2.3f);
            Layer("Outline", Arrowhead(1f), 1f).color = UiTheme.Ink;
            fill = Layer("Fill", Arrowhead(0.72f), 1f);
        }

        Image Layer(string name, Sprite sprite, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pivot, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = pivot.sizeDelta * scale;
            return image;
        }

        /// <summary>A notched arrowhead pointing up, drawn at a given inset so outline and fill share one shape.</summary>
        static Sprite Arrowhead(float scale)
        {
            Vector2[] shape = { new Vector2(0f, 0.95f), new Vector2(0.82f, -0.62f), new Vector2(0f, -0.2f), new Vector2(-0.82f, -0.62f) };
            for (int i = 0; i < shape.Length; i++)
                shape[i] = shape[i] * scale + new Vector2(0f, (1f - scale) * 0.08f);
            return Paint((x, y) => Coverage(shape, x, y));
        }

        static Sprite Glow() => Paint((x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - distance) * Mathf.Clamp01(1f - distance);
        });

        /// <summary>Four-by-four supersampled point-in-polygon, so the edges are smooth at any rotation.</summary>
        static float Coverage(Vector2[] polygon, float x, float y)
        {
            const int Samples = 4;
            float step = 2f / SpriteSize / Samples;
            int inside = 0;
            for (int sx = 0; sx < Samples; sx++)
                for (int sy = 0; sy < Samples; sy++)
                    if (Contains(polygon, new Vector2(x + (sx - 1.5f) * step, y + (sy - 1.5f) * step)))
                        inside++;
            return inside / (float)(Samples * Samples);
        }

        static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                    && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Sprite Paint(System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[SpriteSize * SpriteSize];
            for (int py = 0; py < SpriteSize; py++)
                for (int px = 0; px < SpriteSize; px++)
                {
                    float x = (px + 0.5f) / SpriteSize * 2f - 1f;
                    float y = (py + 0.5f) / SpriteSize * 2f - 1f;
                    pixels[py * SpriteSize + px] = new Color32(255, 255, 255, (byte)(alpha(x, y) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f));
        }
    }
}
