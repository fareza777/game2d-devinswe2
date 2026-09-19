using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A fight that happens because of where the player walked, not because a night started. They are let
    /// past the treeline, then the road closes behind them. Clearing it sets a flag, so the trade road can
    /// be part of a quest chain instead of a detour that changes nothing.
    /// </summary>
    public class RoadAmbush : MonoBehaviour
    {
        [SerializeField] GameObject[] attackers;
        [SerializeField] ActorLook[] looks;

        public void SetLooks(ActorLook[] attackerLooks) => looks = attackerLooks;
        [SerializeField] int count = 4;
        [Tooltip("The road comes after the first nights: tougher than night one, gentler than the pack's sandbox.")]
        [SerializeField] float healthMultiplier = 0.75f;
        [SerializeField] int hitDamage = 7;
        [SerializeField] float triggerRadius = 4.5f;
        [SerializeField] float spread = 6f;
        [Tooltip("Shown across the top when the road closes.")]
        [SerializeField] string bannerKey = "event.ambush.banner";
        [SerializeField] string clearedFlag = "act2.road_ambush_cleared";
        [SerializeField] string counterKey = "kills.bandit";
        [Tooltip("How many of the looks, plain to strange, this fight may use.")]
        [SerializeField] int unlockedLooks = 2;
        [SerializeField] string clearedToastKey = "toast.roadClear";

        readonly List<EnemyHealth2D> alive = new List<EnemyHealth2D>();
        bool sprung;

        public bool IsFighting { get; private set; }

        /// <summary>Where each attacker appeared; read by the smoke test to prove none were put inside a wall.</summary>
        public List<Vector3> SpawnPoints { get; } = new List<Vector3>();

        public string ClearedFlag => clearedFlag;
        public int AliveCount => alive.Count(enemy => enemy && !enemy.IsDead);

        void Update()
        {
            if (sprung || !PlayerHealth.Instance)
                return;
            if (Vector2.Distance(PlayerHealth.Instance.transform.position, transform.position) > triggerRadius)
                return;
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save != null && save.HasFlag(clearedFlag))
            {
                enabled = false;
                return;
            }
            sprung = true;
            StartCoroutine(AmbushRoutine());
        }

        IEnumerator AmbushRoutine()
        {
            IsFighting = true;
            WorldEventDirector.SetBanner(Core.GameServices.Localization.Get(bannerKey));

            Spawn();
            yield return new WaitForSeconds(1f);
            while (alive.Count > 0)
            {
                alive.RemoveAll(enemy => !enemy || enemy.IsDead);
                yield return null;
            }

            IsFighting = false;
            WorldEventDirector.SetBanner(string.Empty);
            Quests.QuestRuntime.SetFlag(clearedFlag);
            UI.Toast.Show(clearedToastKey);
            Core.GameServices.Save.Autosave(clearedFlag, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void Spawn()
        {
            Core.GameServices.Audio.PlaySfx("enemy_alert", 1f, 0.05f);
            GameObject[] pool = attackers?.Where(prefab => prefab).ToArray() ?? System.Array.Empty<GameObject>();
            if (pool.Length == 0)
            {
                Debug.LogError("[Oathfire] Road ambush has no attacker prefabs");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = pool[Random.Range(0, pool.Length)];
                // They come out of both sides of the road, so the player is caught between the verges.
                float side = i % 2 == 0 ? 1f : -1f;
                float angle = side > 0f ? Random.Range(0.3f, 2.8f) : Random.Range(3.5f, 6f);
                Vector3 position = SpawnPlanner.FindOpenPoint(transform.position,
                    PlayerHealth.Instance ? PlayerHealth.Instance.transform.position : transform.position,
                    spread * 0.4f, spread, angle);
                bool stacked = SpawnPoints.Any(other => Vector2.Distance(other, position) < 0.6f);
                if (stacked || !SpawnPlanner.IsOpen(position))
                    position = SpawnPlanner.FindOpenPointApart(transform.position, spread, SpawnPoints);
                SpawnPoints.Add(position);
                GameObject instance = Instantiate(prefab, position, prefab.transform.rotation);
                ActorSorting.Raise(instance);
                // The first men on the road are plain cutthroats and one kind of hired boot.
                ActorLook.PickUnlocked(looks, unlockedLooks)?.ApplyTo(instance);
                EnemyScaling.Apply(instance, healthMultiplier, hitDamage);
                instance.AddComponent<EnemyReachGuard>();

                var health = instance.GetComponentInChildren<EnemyHealth2D>();
                if (health)
                {
                    alive.Add(health);
                    health.OnDied += () => Quests.QuestRuntime.CountKill(counterKey);
                }

                var ai = instance.GetComponentInChildren<EnemyAI>();
                if (ai)
                {
                    ai.returnToPost = false;
                    if (PlayerHealth.Instance)
                        ai.OnAllyAlerted(PlayerHealth.Instance.transform.position, false);
                }
            }
        }
    }
}
