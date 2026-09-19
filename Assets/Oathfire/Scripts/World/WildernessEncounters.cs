using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// The dead do not wait for nightfall out on the road. While the player travels, small groups come out
    /// of the trees at unpredictable intervals — never inside a hearth's light, never during a conversation,
    /// and never straight after the last one. Tuning lives in Resources/Events/encounters.json per scene, so
    /// a new wilderness needs data, not code.
    /// </summary>
    public class WildernessEncounters : MonoBehaviour
    {
        [System.Serializable]
        public class Tuning
        {
            public string scene;
            public int minGroup = 2;
            public int maxGroup = 4;
            [Tooltip("Seconds of travel before the next roll.")]
            public float secondsBetween = 25f;
            [Tooltip("Chance of an encounter on each roll.")]
            public float chance = 0.45f;
            [Tooltip("Quiet time after a fight ends.")]
            public float respiteSeconds = 20f;
            public string counterKey = "kills.wild";
            public string bannerKey = "event.wild.banner";
        }

        [System.Serializable] class TuningTable { public Tuning[] encounters; }

        [SerializeField] GameObject[] enemies;
        [SerializeField] ActorLook[] looks;
        [SerializeField] float healthMultiplier = 0.7f;
        [SerializeField] int hitDamage = 6;

        public void SetLooks(ActorLook[] enemyLooks) => looks = enemyLooks;

        /// <summary>
        /// Plain cutthroats until the road ambush has been broken; then the rest of the bandit crews; and only once
        /// the valley has survived three nights do the dead start coming down from the hills as well.
        /// </summary>
        static int UnlockedLooks()
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || !save.HasFlag("act2.road_ambush_cleared"))
                return 1;
            return save.GetCounter("nights.survived") >= 3 ? int.MaxValue : 4;
        }
        [SerializeField] float spawnDistance = 9f;
        [SerializeField] float safeDistanceFromHearth = 12f;
        [SerializeField] int maxAlive = 6;

        readonly List<EnemyHealth2D> alive = new List<EnemyHealth2D>();
        Tuning tuning;

        public bool IsFighting { get; private set; }
        public int Fought { get; private set; }

        void Awake() => tuning = LoadTuning(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        static Tuning LoadTuning(string sceneName)
        {
            var asset = Resources.Load<TextAsset>("Events/encounters");
            if (!asset)
            {
                Debug.LogError("[Oathfire] Missing Resources/Events/encounters.json");
                return null;
            }
            Tuning[] table = JsonUtility.FromJson<TuningTable>(asset.text).encounters ?? System.Array.Empty<Tuning>();
            return table.FirstOrDefault(entry => entry.scene == sceneName);
        }

        IEnumerator Start()
        {
            if (tuning == null)
                yield break;

            while (true)
            {
                yield return new WaitForSeconds(tuning.secondsBetween);
                if (!CanAmbushNow())
                    continue;
                if (Random.value > tuning.chance)
                    continue;
                yield return StartCoroutine(EncounterRoutine());
                yield return new WaitForSeconds(tuning.respiteSeconds);
            }
        }

        /// <summary>The road is only dangerous where the player is alone, moving, and out of the light.</summary>
        bool CanAmbushNow()
        {
            if (IsFighting || !PlayerHealth.Instance)
                return false;
            if (Dialogue.DialogueRunner.IsConversationActive)
                return false;
            if (WorldEventDirector.Instance && WorldEventDirector.Instance.Running != null)
                return false;

            OathfireHearth hearth = OathfireHearth.Instance;
            if (hearth && hearth.IsLit &&
                Vector2.Distance(PlayerHealth.Instance.transform.position, hearth.transform.position) < safeDistanceFromHearth)
                return false;

            return PlayerHealth.Instance.currentHealth > PlayerHealth.Instance.maxHealth * 0.25f;
        }

        IEnumerator EncounterRoutine()
        {
            IsFighting = true;
            Fought++;
            WorldEventDirector.SetBanner(Core.GameServices.Localization.Get(tuning.bannerKey));

            Spawn(Random.Range(tuning.minGroup, tuning.maxGroup + 1));
            yield return new WaitForSeconds(1f);
            while (alive.Count > 0)
            {
                alive.RemoveAll(enemy => !enemy || enemy.IsDead);
                yield return null;
            }

            IsFighting = false;
            WorldEventDirector.SetBanner(string.Empty);
            UI.Toast.Show("toast.roadClear");
        }

        void Spawn(int count)
        {
            Core.GameServices.Audio.PlaySfx("enemy_alert", 0.9f, 0.08f);
            GameObject[] pool = enemies?.Where(prefab => prefab).ToArray() ?? System.Array.Empty<GameObject>();
            if (pool.Length == 0 || !PlayerHealth.Instance)
            {
                Debug.LogError("[Oathfire] Wilderness encounters have no enemy prefabs");
                return;
            }

            Vector3 player = PlayerHealth.Instance.transform.position;
            float facing = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count && alive.Count < maxAlive; i++)
            {
                // They come from one direction, spread across it, so the player can still run the other way.
                float angle = facing + Random.Range(-0.6f, 0.6f);
                Vector3 position = SpawnPlanner.FindOpenPoint(player, player, spawnDistance * 0.6f, spawnDistance, angle);
                GameObject prefab = pool[Random.Range(0, pool.Length)];
                GameObject instance = Instantiate(prefab, position, prefab.transform.rotation);
                ActorSorting.Raise(instance);
                ActorLook.PickUnlocked(looks, UnlockedLooks())?.ApplyTo(instance);
                EnemyScaling.Apply(instance, healthMultiplier, hitDamage);
                instance.AddComponent<EnemyReachGuard>();

                var health = instance.GetComponentInChildren<EnemyHealth2D>();
                if (health)
                {
                    alive.Add(health);
                    health.OnDied += () => Quests.QuestRuntime.CountKill(tuning.counterKey);
                }

                var ai = instance.GetComponentInChildren<EnemyAI>();
                if (ai)
                {
                    ai.returnToPost = false;
                    ai.OnAllyAlerted(player, false);
                }
            }
        }
    }
}
