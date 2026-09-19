using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// One way a spawned hostile can look: which body it wears, the tint of that body, and how big it is. The pack
    /// ships three skeleton bodies, so without this every fight is the same three figures. Night undead come in
    /// grave, moss and ash; the trade road's attackers wear the human rigs as bandits.
    /// </summary>
    [System.Serializable]
    public class ActorLook
    {
        public string name;
        [Tooltip("Replacement body; empty keeps the prefab's own.")]
        public RuntimeAnimatorController rig;
        public Color tint = Color.white;
        public float minScale = 0.95f;
        public float maxScale = 1.08f;
        [Tooltip("Extra kill counter for this look (e.g. kills.bandit); empty for none.")]
        public string killCounter;

        static readonly FieldInfo OriginalColours = typeof(EnemyHealth2D).GetField("_orig", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo OriginalColourMap = typeof(EnemyHealth2D).GetField("_spriteOriginalColorMap", BindingFlags.Instance | BindingFlags.NonPublic);

        public static ActorLook Pick(ActorLook[] looks) =>
            looks == null || looks.Length == 0 ? null : looks[Random.Range(0, looks.Length)];

        /// <summary>
        /// Picks among only the first <paramref name="unlocked"/> looks. Lists are ordered plain to strange, so the
        /// world shows one ordinary kind of enemy first and introduces the odder ones as the story moves on.
        /// </summary>
        public static ActorLook PickUnlocked(ActorLook[] looks, int unlocked) =>
            looks == null || looks.Length == 0 ? null : looks[Random.Range(0, Mathf.Clamp(unlocked, 1, looks.Length))];

        /// <summary>Dresses a freshly spawned actor. Call after ActorSorting.Raise, which sets the base scale.</summary>
        public void ApplyTo(GameObject actor)
        {
            if (rig)
                foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true))
                    animator.runtimeAnimatorController = rig;

            foreach (SpriteRenderer renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.color = tint;

            // EnemyHealth2D remembers sprite colours in Awake and restores them after every hit flash; without
            // this, the first hit would bleach the tint back to white.
            EnemyHealth2D health = actor.GetComponentInChildren<EnemyHealth2D>();
            if (health && OriginalColours?.GetValue(health) is List<Color> colours)
                for (int i = 0; i < colours.Count; i++)
                    colours[i] = tint;
            if (health && OriginalColourMap?.GetValue(health) is Dictionary<SpriteRenderer, Color> map)
                foreach (SpriteRenderer key in new List<SpriteRenderer>(map.Keys))
                    map[key] = tint;

            actor.transform.localScale *= Random.Range(minScale, maxScale);
            if (!string.IsNullOrEmpty(killCounter) && health)
                health.OnDied += () => Quests.QuestRuntime.CountKill(killCounter);
        }
    }
}
