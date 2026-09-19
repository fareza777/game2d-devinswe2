using System.Collections.Generic;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.Progress
{
    /// <summary>
    /// Pays experience for every hostile that falls, wherever it was spawned: a night wave, a road ambush, a
    /// camp, a captain. Before this only quests and contracts paid, so a hard fight taught nothing and a level
    /// felt like a reward for reading. Tougher things pay more; elites double, a boss triples.
    /// </summary>
    public class KillRewards : MonoBehaviour
    {
        const float ScanSeconds = 0.2f;

        readonly HashSet<EnemyHealth2D> watched = new HashSet<EnemyHealth2D>();
        float nextScan;

        /// <summary>Experience paid by the most recent kill; read by the smoke test.</summary>
        public static int LastPaid { get; private set; }

        void Update()
        {
            if (Time.time < nextScan)
                return;
            nextScan = Time.time + ScanSeconds;
            foreach (EnemyHealth2D enemy in EnemyHealth2D.All)
            {
                if (!enemy || enemy.IsDead || !watched.Add(enemy))
                    continue;
                EnemyHealth2D fallen = enemy;
                enemy.OnDied += () => Pay(fallen);
            }
            watched.RemoveWhere(enemy => !enemy);
        }

        static void Pay(EnemyHealth2D enemy)
        {
            if (!enemy || PlayerState.Instance == null)
                return;
            int amount = ExperienceFor(enemy);
            LastPaid = amount;
            PlayerState.Instance.AddExperience(amount);
            UI.GameplayHud.Instance?.FloatText(enemy.transform.position,
                Core.GameServices.Localization.Format("hud.xpGain", amount));
        }

        /// <summary>A plain skeleton on the first night is worth about a dozen; a captain is worth a fight's worth.</summary>
        public static int ExperienceFor(EnemyHealth2D enemy)
        {
            int amount = Mathf.RoundToInt(4f + enemy.maxHealth / 6f);
            if (enemy.GetComponentInParent<World.BossMarker>())
                return amount * 3;
            if (enemy.name.Contains("Elite"))
                return amount * 2;
            return amount;
        }
    }
}
