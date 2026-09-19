using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps the view inside the painted map. Without this the camera follows the player to the edge and
    /// shows the void past the last tile, which reads as a hole in the world rather than the end of it.
    /// Runs after the follow script so it has the last word on where the camera ends up.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(500)]
    public class CameraBounds : MonoBehaviour
    {
        [SerializeField] Vector2 min;
        [SerializeField] Vector2 max;

        Camera view;

        void Awake() => view = GetComponent<Camera>();

        void LateUpdate()
        {
            float halfHeight = view.orthographicSize;
            float halfWidth = halfHeight * view.aspect;

            // A map narrower than the screen centres instead of clamping, or the view would jitter.
            float x = min.x + halfWidth > max.x - halfWidth
                ? (min.x + max.x) * 0.5f
                : Mathf.Clamp(transform.position.x, min.x + halfWidth, max.x - halfWidth);
            float y = min.y + halfHeight > max.y - halfHeight
                ? (min.y + max.y) * 0.5f
                : Mathf.Clamp(transform.position.y, min.y + halfHeight, max.y - halfHeight);

            transform.position = new Vector3(x, y, transform.position.z);
        }

        /// <summary>Measures the ground layer's painted area and clamps to it, with a small inset.</summary>
        public void FitTo(Tilemap ground, float inset = 1.5f)
        {
            if (!ground)
                return;
            Bounds bounds = ground.localBounds;
            Vector3 centre = ground.transform.TransformPoint(bounds.center);
            Vector3 extents = bounds.extents;
            min = new Vector2(centre.x - extents.x + inset, centre.y - extents.y + inset);
            max = new Vector2(centre.x + extents.x - inset, centre.y + extents.y - inset);
        }
    }
}
