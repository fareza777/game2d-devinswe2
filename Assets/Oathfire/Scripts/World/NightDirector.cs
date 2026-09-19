using System.Collections;
using System.Collections.Generic;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Runs a night: the light drains, the dead walk in from outside the hearth's circle in waves, and
    /// dawn comes when the last of them falls. Kills are reported to the quest counter of the active night.
    /// </summary>
    public class NightDirector : MonoBehaviour
    {
        public static NightDirector Instance { get; private set; }

        [SerializeField] GameObject[] regularEnemies;
        [SerializeField] GameObject[] eliteEnemies;
        [SerializeField] ActorLook[] looks;
        [SerializeField] ActorLook[] eliteLooks;
        [SerializeField] int[] waveSizes = { 3, 4, 5 };
        [SerializeField] float waveDelaySeconds = 8f;
        [SerializeField] float spawnRingOffset = 4f;
        [SerializeField] string killCounterKey = "kills.night1";
        [SerializeField] string nightCompleteFlag = "act1.night1_cleared";
        [SerializeField] AudioClip nightMusic;
        [SerializeField] AudioClip dawnSound;

        readonly List<EnemyHealth2D> alive = new List<EnemyHealth2D>();
        int waveIndex = -1;

        bool tookDamageThisNight;

        public bool IsNightRunning { get; private set; }

        /// <summary>When the last night ended (unscaled seconds since start); nights cannot be called back to back.</summary>
        public float LastDawn { get; private set; } = -999f;

        /// <summary>How long the valley rests after a dawn before the fire can call another night.</summary>
        public const float RestSeconds = 30f;

        /// <summary>True when a new night may be called from the fire: after the first, whenever the valley has rested.</summary>
        public bool CanCallAnotherNight
        {
            get
            {
                Save.SaveData save = Core.GameServices.Save.Current;
                return !IsNightRunning && save != null && save.HasFlag("act1.night1_cleared")
                       && Time.time - LastDawn >= RestSeconds;
            }
        }
        public int AliveCount => alive.Count;
        public int WaveNumber => waveIndex + 1;
        public int TotalWaves => CurrentWaves.Length;

        /// <summary>
        /// The nights get harder, and stranger, as the settlement survives them. The first is a lesson: eight plain
        /// skeleton warriors at little more than half strength. Each night adds one new kind and one new look:
        /// chanting mages on the second, armoured knights on the third, smouldering champions from the fourth.
        /// </summary>
        int NightNumber => (Core.GameServices.Save.Current?.GetCounter("nights.survived") ?? 0) + 1;

        int[] CurrentWaves => NightNumber switch
        {
            1 => new[] { 2, 3, 3 },
            2 => new[] { 3, 4, 4 },
            _ => waveSizes,
        };

        float HealthMultiplier => NightNumber switch { 1 => 0.6f, 2 => 0.8f, 3 => 0.9f, _ => 1f };

        /// <summary>Which bodies may walk tonight, in the order they are introduced.</summary>
        static readonly string[] Introduced = { "Warrior", "Mage", "Knight" };

        bool Allowed(GameObject prefab)
        {
            if (!prefab)
                return false;
            for (int i = 0; i < Introduced.Length; i++)
                if (prefab.name.Contains(Introduced[i]))
                    return i < NightNumber;
            return NightNumber >= 4;
        }

        int HitDamage => NightNumber switch { 1 => 6, 2 => 8, _ => 10 };

        void Awake() => Instance = this;

        public void BeginNight()
        {
            if (IsNightRunning)
                return;
            StartCoroutine(NightRoutine());
        }

        IEnumerator NightRoutine()
        {
            IsNightRunning = true;
            tookDamageThisNight = false;
            if (PlayerHealth.Instance)
                PlayerHealth.Instance.OnDamageTaken += NoteDamage;
            // The sound director switches to the siege theme while a night runs; the horn announces it.
            if (nightMusic)
                Core.GameServices.Audio.PlayMusic(nightMusic);
            Core.GameServices.Audio.PlaySfx("night_horn", 1f, 0f);
            UI.Toast.Show("toast.nightFalls");

            int[] waves = CurrentWaves;
            for (waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                SpawnWave(waves[waveIndex], waveIndex == waves.Length - 1 && NightNumber >= 4);
                yield return new WaitForSeconds(waveDelaySeconds);
                while (alive.Count > 0)
                {
                    alive.RemoveAll(enemy => !enemy || enemy.IsDead);
                    yield return null;
                }
            }

            IsNightRunning = false;
            LastDawn = Time.time;
            if (PlayerHealth.Instance)
                PlayerHealth.Instance.OnDamageTaken -= NoteDamage;
            if (dawnSound)
                Core.GameServices.Audio.PlaySfx(dawnSound);
            else
                Core.GameServices.Audio.PlaySfx("dawn", 1f, 0f);
            UI.Toast.Show("toast.dawn");

            Save.SaveData save = Core.GameServices.Save.Current;
            save?.AddCounter("nights.survived", 1);
            if (!tookDamageThisNight)
                save?.AddCounter("night.flawless", 1);   // the board pays for an untouched vigil
            Quests.QuestRuntime.SetFlag(nightCompleteFlag);
            Core.GameServices.Save.Autosave(nightCompleteFlag, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void SpawnWave(int count, bool includeElite)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject[] pool = includeElite && i == count - 1 && eliteEnemies.Length > 0 ? eliteEnemies : regularEnemies;
                pool = System.Array.FindAll(pool, Allowed);
                if (pool.Length == 0)
                    pool = System.Array.FindAll(regularEnemies, prefab => prefab && prefab.name.Contains("Warrior"));
                if (pool.Length == 0)
                    continue;
                GameObject prefab = pool[Random.Range(0, pool.Length)];
                Vector3 position = SpawnPosition(i, count);
                SpawnPoints.Add(position);
                GameObject instance = Instantiate(prefab, position, prefab.transform.rotation);
                ActorSorting.Raise(instance);
                bool elite = includeElite && i == count - 1 && eliteEnemies.Length > 0;
                (elite ? ActorLook.Pick(eliteLooks) : ActorLook.PickUnlocked(looks, NightNumber))?.ApplyTo(instance);
                EnemyScaling.Apply(instance, HealthMultiplier, HitDamage);
                instance.AddComponent<EnemyReachGuard>();

                var health = instance.GetComponentInChildren<EnemyHealth2D>();
                if (health)
                {
                    alive.Add(health);
                    string kindCounter = KillCounterFor(prefab.name);
                    health.OnDied += () =>
                    {
                        Quests.QuestRuntime.CountKill(killCounterKey);
                        Quests.QuestRuntime.CountKill(kindCounter);
                    };
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

        void NoteDamage(int amount) => tookDamageThisNight = true;

        /// <summary>Contracts ask for specific quarry, so kills are tallied per enemy type as well.</summary>
        static string KillCounterFor(string prefabName)
        {
            string name = prefabName.ToLowerInvariant();
            if (name.Contains("elite"))
                return "kills.elite";
            if (name.Contains("knight"))
                return "kills.knight";
            if (name.Contains("mage"))
                return "kills.mage";
            return "kills.skeleton";
        }

        /// <summary>Spawns just beyond the hearth light, spread around the circle, so attacks come from the dark.</summary>
        /// <summary>
        /// Out at the edge of the firelight, spread around it, but only where there is open ground and a clear walk
        /// in to the hearth. A bare ring put the dead inside houses and behind the tree line, out of reach.
        /// </summary>
        Vector3 SpawnPosition(int index, int count)
        {
            OathfireHearth hearth = OathfireHearth.Instance;
            Vector3 center = hearth ? hearth.transform.position : transform.position;
            float radius = (hearth ? hearth.SafeRadius : 8f) + spawnRingOffset;
            float angle = (index / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
            return SpawnPlanner.FindOpenPoint(center, center, radius * 0.75f, radius, angle, SquareReach);
        }

        /// <summary>Every point an enemy has been placed at this session; the smoke test checks they are all reachable.</summary>
        public List<Vector3> SpawnPoints { get; } = new List<Vector3>();

        /// <summary>How far from the fire the open square begins; the dead need a clear walk to here.</summary>
        public const float SquareReach = 4.5f;

        public void SetLooks(ActorLook[] regular, ActorLook[] elite)
        {
            looks = regular;
            eliteLooks = elite;
        }
    }
}
