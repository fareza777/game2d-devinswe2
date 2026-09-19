using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A soft light over someone who has just arrived. A banner that announces a visitor the player cannot
    /// then find is worse than no banner at all — especially when a clock is running on it.
    /// </summary>
    public class EventMarker : MonoBehaviour
    {
        [SerializeField] float height = 1.5f;
        [SerializeField] float bob = 0.12f;

        SpriteRenderer glow;
        Vector3 origin;

        public static EventMarker Attach(GameObject actor, Sprite sprite, Color colour)
        {
            var go = new GameObject("Marker", typeof(SpriteRenderer), typeof(EventMarker));
            go.transform.SetParent(actor.transform, false);
            var marker = go.GetComponent<EventMarker>();
            marker.glow = go.GetComponent<SpriteRenderer>();
            marker.glow.sprite = sprite;
            marker.glow.color = colour;
            marker.glow.sortingOrder = ActorSorting.ActorOrder + 1;
            go.transform.localScale = Vector3.one * 0.85f;
            return marker;
        }

        void Start() => origin = transform.localPosition + new Vector3(0f, height, 0f);

        void Update()
        {
            float pulse = Mathf.Sin(Time.time * 3f);
            transform.localPosition = origin + new Vector3(0f, pulse * bob, 0f);
            if (glow)
                glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0.55f + pulse * 0.2f);
        }
    }
}
