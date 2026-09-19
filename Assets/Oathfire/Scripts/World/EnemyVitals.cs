using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// What a fight looks like from the outside: a readable strip of health above the enemy and the number of
    /// every hit rising off it. The pack's own bar is a quarter of a world unit wide, drains from both ends at
    /// once and hides itself a second and a half after each hit, so a long fight reads as a hero swinging at
    /// something that never changes.
    /// </summary>
    public class EnemyVitals : MonoBehaviour
    {
        const float Width = 1.05f;
        const float Height = 0.13f;
        const float Lift = 1.35f;
        const int SortingOrder = 9000;

        static Sprite pixel;

        EnemyHealth2D health;
        Transform bar;
        Transform fill;
        SpriteRenderer fillRenderer;

        public static void Attach(GameObject enemy)
        {
            EnemyHealth2D found = enemy.GetComponentInChildren<EnemyHealth2D>();
            if (!found || found.GetComponent<EnemyVitals>())
                return;
            // The pack's own bar would sit underneath ours, draining the other way.
            found.useHealthBar = false;
            found.gameObject.AddComponent<EnemyVitals>();
        }

        void Awake()
        {
            health = GetComponent<EnemyHealth2D>();
            Build();
            health.OnDamageTaken += ShowDamage;
        }

        void OnDestroy()
        {
            if (health)
                health.OnDamageTaken -= ShowDamage;
        }

        void Build()
        {
            if (!pixel)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            bar = new GameObject("Vitals").transform;
            bar.SetParent(transform, false);
            // The actor is drawn at 1.5x; the strip should be the same size above every enemy.
            float inverse = transform.lossyScale.x > 0.01f ? 1f / transform.lossyScale.x : 1f;
            bar.localPosition = new Vector3(0f, Lift, 0f);
            bar.localScale = Vector3.one * inverse;

            Piece("Frame", new Color(0.05f, 0.05f, 0.04f, 0.85f), Width + 0.06f, Height + 0.05f, SortingOrder, out _);
            Piece("Back", new Color(0.29f, 0.12f, 0.1f, 0.9f), Width, Height, SortingOrder + 1, out _);
            fill = Piece("Fill", new Color(0.78f, 0.19f, 0.15f, 1f), Width, Height, SortingOrder + 2, out fillRenderer);
            SetRatio(1f);
        }

        Transform Piece(string name, Color colour, float width, float height, int order, out SpriteRenderer renderer)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(bar, false);
            piece.transform.localScale = new Vector3(width, height, 1f);
            renderer = piece.GetComponent<SpriteRenderer>();
            renderer.sprite = pixel;
            renderer.color = colour;
            renderer.sortingOrder = order;
            return piece.transform;
        }

        void Update()
        {
            if (!health)
                return;
            if (health.IsDead)
            {
                bar.gameObject.SetActive(false);
                return;
            }
            float ratio = health.MaxHealth > 0 ? health.CurrentHealth / (float)health.MaxHealth : 0f;
            SetRatio(ratio);
            // Whole while it is hurt, so the player can see how much fight is left rather than a flash after a hit.
            bar.gameObject.SetActive(ratio < 0.999f);
        }

        void SetRatio(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            // Anchored on the left: health leaves from one end, the way every health bar in the game does.
            fill.localScale = new Vector3(Width * ratio, Height, 1f);
            fill.localPosition = new Vector3(-Width * (1f - ratio) * 0.5f, 0f, 0f);
            fillRenderer.color = ratio > 0.5f ? new Color(0.78f, 0.19f, 0.15f) :
                ratio > 0.25f ? new Color(0.85f, 0.45f, 0.12f) : new Color(0.95f, 0.72f, 0.2f);
        }

        void ShowDamage(int amount)
        {
            if (amount <= 0)
                return;
            UI.GameplayHud.Instance?.FloatText(transform.position + new Vector3(0f, 0.9f, 0f), $"-{amount}",
                new Color(1f, 0.86f, 0.72f));
        }
    }
}
