using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps a hostile within the player's reach. The pack's AI steers straight at its target with only a short
    /// probe for obstacles, so an enemy on the far side of a house presses into the wall indefinitely. If it has
    /// made no headway toward the hero for a few seconds, or ends up inside a wall, it steps out to open ground a
    /// short walk from the hero, on a clear line.
    /// </summary>
    public class EnemyReachGuard : MonoBehaviour
    {
        const float CheckSeconds = 1f;
        const float GiveUpSeconds = 4f;
        const float EngagedDistance = 2.2f;
        const float MinimumProgress = 0.35f;

        EnemyHealth2D health;
        Rigidbody2D body;
        float stalledFor;
        float lastDistance = float.MaxValue;
        float nextCheck;

        /// <summary>How many times any guard has moved an enemy; read by the smoke test.</summary>
        public static int Rescues { get; private set; }

        void Awake()
        {
            health = GetComponentInChildren<EnemyHealth2D>();
            body = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            if (Time.time < nextCheck || (health && health.IsDead) || !PlayerHealth.Instance)
                return;
            nextCheck = Time.time + CheckSeconds;

            Vector2 hero = PlayerHealth.Instance.transform.position;
            float distance = Vector2.Distance(transform.position, hero);
            bool embedded = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("World"));

            if (distance <= EngagedDistance && !embedded)
            {
                stalledFor = 0f;
                lastDistance = distance;
                return;
            }

            bool progressing = lastDistance - distance >= MinimumProgress;
            lastDistance = distance;
            stalledFor = progressing ? 0f : stalledFor + CheckSeconds;

            bool blocked = !SpawnPlanner.HasClearApproach(transform.position, hero);
            if (embedded || (stalledFor >= GiveUpSeconds && blocked))
                StepOut(hero);
        }

        void StepOut(Vector2 hero)
        {
            float angle = Mathf.Atan2(transform.position.y - hero.y, transform.position.x - hero.x);
            Vector3 point = SpawnPlanner.FindOpenPoint(hero, hero, 3.5f, 5.5f, angle);
            if (Vector2.Distance(point, hero) < 0.5f)
                return;
            if (body)
            {
                body.position = point;
                body.linearVelocity = Vector2.zero;
            }
            transform.position = point;
            stalledFor = 0f;
            lastDistance = float.MaxValue;
            Rescues++;
        }
    }
}
