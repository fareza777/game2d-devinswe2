using System.Reflection;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Moves the hero the way players expect: blocked by walls, trees and buildings, but sliding along them
    /// instead of stopping dead. The pack's controller still reads input and drives animation; this runs after
    /// it each physics step and replaces its move with a swept one.
    ///
    /// Why not leave it to the controller: its own sweep stops the body on any touch (walls felt sticky), and
    /// without that sweep MovePosition carries the body straight through static colliders.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(GenericTopDownController))]
    public class WallSlideMover : MonoBehaviour
    {
        const float Skin = 0.03f;
        const int SlidePasses = 3;

        static readonly FieldInfo DirectionField = typeof(GenericTopDownController)
            .GetField("_moveDirThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo SpeedField = typeof(GenericTopDownController)
            .GetField("_speedThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        GenericTopDownController controller;
        Rigidbody2D body;
        CircleCollider2D feet;
        ContactFilter2D walls;

        /// <summary>Where the last step wanted to go and where it was allowed to go; read by the smoke test.</summary>
        public Vector2 LastWanted { get; private set; }
        public Vector2 LastAllowed { get; private set; }

        void Awake()
        {
            controller = GetComponent<GenericTopDownController>();
            body = GetComponent<Rigidbody2D>();
            feet = GetComponent<CircleCollider2D>();
            walls = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("World"), useTriggers = false };
            if (DirectionField == null || SpeedField == null)
                Debug.LogError("[Oathfire] GenericTopDownController changed: movement fields not found, walls will not block");
        }

        void FixedUpdate()
        {
            if (PlayerHealth.IsPlayerDead || DirectionField == null || SpeedField == null)
                return;

            var direction = (Vector2)DirectionField.GetValue(controller);
            float speed = (float)SpeedField.GetValue(controller);
            Vector2 wanted = direction * speed * Time.fixedDeltaTime;
            LastWanted = wanted;
            if (wanted.sqrMagnitude < 1e-8f)
            {
                LastAllowed = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 allowed = Sweep(wanted);
            LastAllowed = allowed;
            // The last MovePosition in a step wins, so this replaces the controller's unswept move.
            body.MovePosition(body.position + allowed);
            body.linearVelocity = Vector2.zero;
        }

        /// <summary>Casts the feet along the move; on contact, keeps the part of the move that runs along the wall.</summary>
        Vector2 Sweep(Vector2 move)
        {
            Vector2 centre = feet.bounds.center;
            float radius = feet.bounds.extents.x * 0.95f;
            Vector2 travelled = Vector2.zero;

            for (int pass = 0; pass < SlidePasses && move.sqrMagnitude > 1e-8f; pass++)
            {
                float distance = move.magnitude;
                Vector2 heading = move / distance;
                if (!FirstBlockingHit(centre + travelled, radius, heading, distance + Skin, out RaycastHit2D hit))
                    return travelled + move;

                float free = Mathf.Max(0f, hit.distance - Skin);
                travelled += heading * free;
                Vector2 remaining = heading * (distance - free);
                // Drop the part of the move that pushes into the wall; what is left slides along it.
                move = remaining - hit.normal * Vector2.Dot(remaining, hit.normal);
            }
            return travelled;
        }

        bool FirstBlockingHit(Vector2 origin, float radius, Vector2 heading, float distance, out RaycastHit2D blocking)
        {
            blocking = default;
            int count = Physics2D.CircleCast(origin, radius, heading, walls, hits, distance);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.distance <= 0f)
                {
                    // Already touching this collider: only block moves that go further into it.
                    Vector2 away = origin - hit.collider.ClosestPoint(origin);
                    if (away.sqrMagnitude < 1e-8f || Vector2.Dot(away, heading) >= 0f)
                        continue;
                    hit.normal = away.normalized;
                }
                else if (Vector2.Dot(hit.normal, heading) >= 0f)
                {
                    continue;
                }
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    blocking = hit;
                }
            }
            return nearest < float.MaxValue;
        }
    }
}
