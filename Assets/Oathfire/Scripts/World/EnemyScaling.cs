using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// How tough a spawned hostile is. The pack's enemies are tuned for its own sandbox (a skeleton knight has 200
    /// health), far too much for the first nights of a story; spawners scale them to where the player is.
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>
        /// How much tougher hostiles are for a Warden who has grown. Without this, the levels that make a hero
        /// stronger also make every later fight emptier: by level six a night wave falls in one swing each.
        /// Kept gentle, and capped, so growing still feels like growing.
        /// </summary>
        public static float LevelScale
        {
            get
            {
                int level = Progress.PlayerState.Instance?.Level ?? 1;
                return Mathf.Min(1.8f, 1f + (level - 1) * 0.08f);
            }
        }

        public static void Apply(GameObject enemy, float healthMultiplier, int hitDamage)
        {
            float scale = LevelScale;
            healthMultiplier *= scale;
            hitDamage = Mathf.RoundToInt(hitDamage * Mathf.Min(1.45f, 1f + (scale - 1f) * 0.5f));
            EnemyHealth2D health = enemy.GetComponentInChildren<EnemyHealth2D>();
            if (health)
                health.ApplyExternalHealthMultiplier(healthMultiplier, fillToMax: true);
            // Every scaled enemy also gets a bar the player can read and a damage number on each hit.
            EnemyVitals.Attach(enemy);

            EnemyAI ai = enemy.GetComponentInChildren<EnemyAI>();
            if (ai)
            {
                ai.meleeDamage = hitDamage;
                ai.projectileDamage = hitDamage;
            }
        }
    }
}
