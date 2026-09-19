using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Anything the player can walk up to and use: the hearth, a villager, a timber stack, a build site, a door.
    /// The prompt itself is drawn once by the HUD next to the Use button, not as floating world text,
    /// which keeps the screen readable on a phone.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class WorldInteractable : MonoBehaviour
    {
        [SerializeField] string promptKey = "prompt.use";
        [SerializeField] float radius = 1.6f;
        [SerializeField] bool oneShot;

        static readonly HashSet<WorldInteractable> InRange = new HashSet<WorldInteractable>();

        bool used;

        public event Action Used;

        /// <summary>
        /// The interactable nearest the player among those in reach, or null. Nearest, not most recently
        /// entered: a villager on their doorstep and the door behind them overlap, and the one the player is
        /// actually standing at is the one they mean.
        /// </summary>
        public static WorldInteractable Focused
        {
            get
            {
                var hero = SmallScale.FantasyKingdomTileset.PlayerHealth.Instance;
                if (!hero)
                    return null;
                WorldInteractable best = null;
                float bestDistance = float.MaxValue;
                foreach (WorldInteractable candidate in InRange)
                {
                    if (!candidate || candidate.used || !candidate.isActiveAndEnabled)
                        continue;
                    float distance = ((Vector2)candidate.transform.position - (Vector2)hero.transform.position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
                return best;
            }
        }

        public string PromptKey => promptKey;

        void Awake()
        {
            var trigger = GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = radius;
        }

        void OnDisable() => InRange.Remove(this);

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!used && IsPlayer(other))
                InRange.Add(this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other))
                InRange.Remove(this);
        }

        static bool IsPlayer(Collider2D other) => other.GetComponentInParent<SmallScale.FantasyKingdomTileset.PlayerHealth>();

        /// <summary>Called by the HUD's Use button when this interactable is the focused one.</summary>
        public void Use()
        {
            if (used)
                return;
            if (oneShot)
            {
                used = true;
                InRange.Remove(this);
            }
            Used?.Invoke();
        }

        public void SetPrompt(string key) => promptKey = key;

        /// <summary>How close the player must be. Small things (a door) want a tighter reach than a person.</summary>
        public void SetRadius(float reach)
        {
            radius = reach;
            GetComponent<CircleCollider2D>().radius = reach;
        }
    }
}
