using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps the hero's feet out of walls. A body that spawns overlapping a collider, or is shoved into one by
    /// a crowd, is moved to the nearest open ground instead of being left frozen until a skill knocks it free.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class BodyUnstuck : MonoBehaviour
    {
        const float SearchStep = 0.2f;
        const float SearchRadius = 6f;
        const float EmbeddedGraceSeconds = 0.25f;

        Rigidbody2D body;
        CircleCollider2D feet;
        int worldMask;
        float embeddedSince = -1f;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            feet = GetComponent<CircleCollider2D>();
            worldMask = LayerMask.GetMask("World");
        }

        void Start()
        {
            Physics2D.SyncTransforms();
            if (Overlaps(FeetCentre()))
                MoveToOpenGround();
        }

        void FixedUpdate()
        {
            // The solver pushes a body out of shallow contact by itself. Only a body whose centre is inside a
            // wall is truly stuck, and only if it stays that way for a moment.
            if (!Physics2D.OverlapPoint(FeetCentre(), worldMask))
            {
                embeddedSince = -1f;
                return;
            }
            if (embeddedSince < 0f)
                embeddedSince = Time.time;
            else if (Time.time - embeddedSince > EmbeddedGraceSeconds)
                MoveToOpenGround();
        }

        Vector2 FeetCentre() => feet.bounds.center;

        float WorldRadius => feet.bounds.extents.x;

        bool Overlaps(Vector2 centre) => Physics2D.OverlapCircle(centre, WorldRadius * 1.1f, worldMask);

        void MoveToOpenGround()
        {
            Vector2 start = FeetCentre();
            Vector2 feetOffset = start - body.position;
            for (float ring = SearchStep; ring <= SearchRadius; ring += SearchStep)
            {
                int samples = Mathf.CeilToInt(2f * Mathf.PI * ring / SearchStep);
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector2 candidate = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f) * ring;
                    if (Overlaps(candidate))
                        continue;
                    body.position = candidate - feetOffset;
                    transform.position = body.position;
                    body.linearVelocity = Vector2.zero;
                    embeddedSince = -1f;
                    Debug.Log($"[Oathfire] freed {name} from a wall at {start}, moved {ring:0.0} units");
                    return;
                }
            }
            Debug.LogWarning($"[Oathfire] {name} is inside a wall at {start} and no open ground was found nearby");
        }
    }
}
