using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A villager who actually lives somewhere: stands a while, then walks a short way off and stands again.
    /// Destinations are picked from open ground (SpawnPlanner) so people wander the square and the lanes
    /// instead of pacing through walls. Holds still while a conversation is running, so nobody drifts off
    /// mid-sentence, and gives up on a destination that stops being reachable.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcWanderer : MonoBehaviour
    {
        [SerializeField] float radius = 2.4f;
        [SerializeField] float walkSpeed = 0.85f;
        [SerializeField] Vector2 idlePause = new Vector2(2.5f, 6.5f);

        const int PickTries = 12;
        const float ArriveDistance = 0.08f;
        const float StuckSeconds = 1.1f;

        readonly int hDirection = Animator.StringToHash("Direction");
        readonly int hDirIndex = Animator.StringToHash("DirIndex");
        readonly int hIsWalk = Animator.StringToHash("IsWalk");

        Animator anim;
        Vector3 home;
        Vector3 destination;
        float pauseUntil;
        float stuckFor;
        bool hasDestination;

        void Awake() => home = transform.position;

        void OnEnable()
        {
            anim = GetComponent<Animator>();
            home = transform.position;
            hasDestination = false;
            pauseUntil = Time.time + Random.Range(0.5f, idlePause.y);
        }

        void Update()
        {
            if (Dialogue.DialogueRunner.IsConversationActive)
            {
                Hold();
                return;
            }
            if (!hasDestination)
            {
                if (Time.time < pauseUntil || !PickDestination())
                {
                    Hold();
                    return;
                }
            }

            Vector3 delta = destination - transform.position;
            delta.z = 0f;
            float distance = delta.magnitude;
            if (distance < ArriveDistance || stuckFor >= StuckSeconds)
            {
                hasDestination = false;
                stuckFor = 0f;
                pauseUntil = Time.time + Random.Range(idlePause.x, idlePause.y);
                Hold();
                return;
            }

            float step = Mathf.Min(walkSpeed * Time.deltaTime, distance);
            transform.position += delta / distance * step;
            stuckFor += step < walkSpeed * Time.deltaTime * 0.35f ? Time.deltaTime : -stuckFor;

            if (anim)
            {
                int dirIndex = DirIndex(delta);
                anim.SetFloat(hDirection, dirIndex);
                anim.SetInteger(hDirIndex, dirIndex);
                anim.SetBool(hIsWalk, true);
            }
        }

        void Hold()
        {
            hasDestination = hasDestination && !Dialogue.DialogueRunner.IsConversationActive;
            if (anim)
                anim.SetBool(hIsWalk, false);
        }

        bool PickDestination()
        {
            for (int i = 0; i < PickTries; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector2 point = (Vector2)home + offset;
                if (SpawnPlanner.IsOpen(point, 0.3f) && SpawnPlanner.HasClearApproach(transform.position, point, 0.2f))
                {
                    destination = point;
                    hasDestination = true;
                    return true;
                }
            }
            pauseUntil = Time.time + Random.Range(idlePause.x, idlePause.y);
            return false;
        }

        // Same 8-way index the pack's rigs use: E=0 W=1 S=2 N=3 NE=4 NW=5 SE=6 SW=7.
        static int DirIndex(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0f)
                ang += 360f;
            if (ang >= 337.5f || ang < 22.5f) return 0;
            if (ang < 67.5f) return 4;
            if (ang < 112.5f) return 3;
            if (ang < 157.5f) return 5;
            if (ang < 202.5f) return 1;
            if (ang < 247.5f) return 7;
            if (ang < 292.5f) return 2;
            return 6;
        }
    }
}
