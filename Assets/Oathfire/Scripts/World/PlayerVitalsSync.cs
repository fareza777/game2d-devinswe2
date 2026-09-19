using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Makes the health bar belong to the Warden's sheet. The pack's PlayerHealth keeps its own maximum of 100
    /// forever, so a level or a new coat changed a number on the book page and nothing in a fight. This keeps
    /// the maximum equal to the sheet, and a level gained closes every wound and refills the mana.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerVitalsSync : MonoBehaviour
    {
        PlayerHealth health;
        Progress.PlayerState state;

        void Awake() => health = GetComponent<PlayerHealth>();

        void OnDestroy()
        {
            if (state != null)
                state.LeveledUp -= OnLevelUp;
        }

        void Update()
        {
            if (state != Progress.PlayerState.Instance)
            {
                if (state != null)
                    state.LeveledUp -= OnLevelUp;
                state = Progress.PlayerState.Instance;
                if (state != null)
                    state.LeveledUp += OnLevelUp;
            }
            if (state == null || health.maxHealth == state.MaxHealth)
                return;

            // Growing keeps the same share of health; shrinking (gear taken off) only trims the excess.
            int grown = state.MaxHealth - health.maxHealth;
            health.maxHealth = state.MaxHealth;
            if (grown > 0 && !PlayerHealth.IsPlayerDead)
                health.currentHealth += grown;
            health.currentHealth = Mathf.Clamp(health.currentHealth, 1, health.maxHealth);
        }

        void OnLevelUp(int level)
        {
            if (PlayerHealth.IsPlayerDead)
                return;
            health.maxHealth = state.MaxHealth;
            health.currentHealth = health.maxHealth;
            PlayerMana mana = PlayerMana.Instance;
            if (mana)
                mana.Refill();
            UI.GameplayHud.Instance?.ShowLevelUp(level);
        }
    }
}
