using System.Reflection;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Makes the sword hit as hard as the Warden's sheet says. The pack's melee hitbox captures its own base
    /// damage (12) once and never looks at Oathfire's character: level and weapon showed on the book page but did
    /// nothing in a fight, so a knight took seventeen swings. This keeps the hitbox's base equal to the hero's
    /// damage whenever gear or level changes.
    /// </summary>
    [RequireComponent(typeof(PlayerMeleeHitbox))]
    public class PlayerDamageSync : MonoBehaviour
    {
        static readonly FieldInfo BaseDamage = typeof(PlayerMeleeHitbox).GetField("_baseDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Captured = typeof(PlayerMeleeHitbox).GetField("_hasCapturedBaseDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo Recompute = typeof(PlayerMeleeHitbox).GetMethod("EnsureStatsSubscription", BindingFlags.Instance | BindingFlags.NonPublic);

        PlayerMeleeHitbox melee;
        Progress.PlayerState state;
        int applied = -1;

        /// <summary>The damage the hitbox is currently built on; read by the smoke test.</summary>
        public int Applied => applied;

        void Awake() => melee = GetComponent<PlayerMeleeHitbox>();

        void Update()
        {
            if (state != Progress.PlayerState.Instance)
                state = Progress.PlayerState.Instance;
            if (state == null || state.Damage == applied)
                return;

            applied = state.Damage;
            if (BaseDamage == null || Captured == null)
            {
                Debug.LogError("[Oathfire] PlayerMeleeHitbox changed: hero damage cannot be applied to the sword");
                melee.damage = applied;
                return;
            }
            BaseDamage.SetValue(melee, applied);
            Captured.SetValue(melee, true);
            melee.damage = applied;
            Recompute?.Invoke(melee, null);
        }
    }
}
