using System.Collections.Generic;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Oathfire.Audio
{
    /// <summary>
    /// Decides what the world sounds like from what is happening in it, so no gameplay system has to know
    /// about music: the place picks the theme and ambience, nightfall and nearby enemies take it over, and the
    /// common beats of play (a hit landing, an enemy falling, footsteps, a button, a finished quest) are heard.
    /// Lives on the persistent services object.
    /// </summary>
    public class SoundDirector : MonoBehaviour
    {
        const float CombatRange = 9f;
        const float CombatReleaseSeconds = 5f;
        const float StepInterval = 0.34f;
        const float StepSpeed = 0.6f;
        const float ButtonScanSeconds = 0.5f;

        /// <summary>Scene name to (theme, ambience). Scenes not listed (title, cinematic) choose their own music.</summary>
        static readonly Dictionary<string, (string music, string ambience)> Places = new Dictionary<string, (string, string)>
        {
            { "Rennfall", ("village_day", "village_day") },
            { "TradeRoad", ("road_travel", "road") },
            { "Greymarch", ("city_greymarch", "city") },
            { "Hollow", ("hollow_camp", "ravine") },
            { "Saltpans", ("saltpans_works", "salt_works") },
            { "Burnpits", ("burnpits_smoke", "char_camp") },
            { "Windrest", ("windrest_mill", "windmill_hill") },
            { "Chalkpit", ("chalkpit_cut", "chalk_pit") },
            { "Stampworks", ("stampworks_theme", "stamp_yard") },
            { "Tollbank", ("tollbank_theme", "toll_fort") },
            { "DroversRest", ("drovers_theme", "drover_camp") },
        };

        readonly HashSet<EnemyHealth2D> heardEnemies = new HashSet<EnemyHealth2D>();
        readonly HashSet<int> wiredButtons = new HashSet<int>();

        AudioService audioService;
        PlayerHealth trackedPlayer;
        Progress.PlayerState trackedState;
        Quests.QuestService trackedQuests;
        float lastCombatTime = -100f;
        float nextStepTime;
        float nextButtonScan;
        Vector3 lastPlayerPosition;

        public void Initialize(AudioService service)
        {
            audioService = service;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            heardEnemies.Clear();
            wiredButtons.Clear();
            nextButtonScan = 0f;
            lastCombatTime = -100f;
            if (!Places.ContainsKey(scene.name))
                audioService.PlayAmbience(null);
        }

        void Update()
        {
            TrackPlayer();
            TrackProgress();
            ListenToEnemies();
            ChooseMusic();
            Footsteps();
            WireButtons();
        }

        void TrackPlayer()
        {
            if (trackedPlayer == PlayerHealth.Instance)
                return;
            if (trackedPlayer)
                trackedPlayer.OnDamageTaken -= OnPlayerHurt;
            trackedPlayer = PlayerHealth.Instance;
            if (trackedPlayer)
            {
                trackedPlayer.OnDamageTaken += OnPlayerHurt;
                lastPlayerPosition = trackedPlayer.transform.position;
            }
        }

        void TrackProgress()
        {
            if (trackedState != Progress.PlayerState.Instance)
            {
                if (trackedState != null)
                    trackedState.LeveledUp -= OnLevelUp;
                trackedState = Progress.PlayerState.Instance;
                if (trackedState != null)
                    trackedState.LeveledUp += OnLevelUp;
            }
            if (trackedQuests != Quests.QuestRuntime.Service)
            {
                if (trackedQuests != null)
                    trackedQuests.QuestCompleted -= OnQuestCompleted;
                trackedQuests = Quests.QuestRuntime.Service;
                if (trackedQuests != null)
                    trackedQuests.QuestCompleted += OnQuestCompleted;
            }
        }

        void OnPlayerHurt(int amount) => audioService.PlaySfx("player_hurt", 0.8f, 0.08f);

        void OnLevelUp(int level) => audioService.PlaySfx("level_up");

        void OnQuestCompleted(Quests.QuestDefinition quest) => audioService.PlaySfx("quest_complete", 0.9f, 0f);

        /// <summary>Enemies register themselves; each new one is listened to for the hit and the fall.</summary>
        void ListenToEnemies()
        {
            Vector3 player = trackedPlayer ? trackedPlayer.transform.position : Vector3.zero;
            foreach (EnemyHealth2D enemy in EnemyHealth2D.All)
            {
                if (!enemy)
                    continue;
                if (heardEnemies.Add(enemy))
                {
                    enemy.OnDamageTaken += amount => audioService.PlaySfx("hit", 0.9f, 0.12f);
                    enemy.OnDied += () => audioService.PlaySfx("enemy_death", 0.9f, 0.1f);
                }
                if (trackedPlayer && !enemy.IsDead && Vector2.Distance(player, enemy.transform.position) < CombatRange)
                    lastCombatTime = Time.time;
            }
        }

        void ChooseMusic()
        {
            if (!Places.TryGetValue(SceneManager.GetActiveScene().name, out var place))
                return;

            bool night = World.NightDirector.Instance && World.NightDirector.Instance.IsNightRunning;
            bool fighting = Time.time - lastCombatTime < CombatReleaseSeconds;
            audioService.PlayMusic(night ? "night_siege" : fighting ? "battle" : place.music);
            audioService.PlayAmbience(night ? "night" : place.ambience);
        }

        void Footsteps()
        {
            if (!trackedPlayer || PlayerHealth.IsPlayerDead)
                return;
            Vector3 position = trackedPlayer.transform.position;
            float speed = Time.deltaTime > 0f ? (position - lastPlayerPosition).magnitude / Time.deltaTime : 0f;
            lastPlayerPosition = position;
            if (speed < StepSpeed || Time.time < nextStepTime)
                return;
            nextStepTime = Time.time + StepInterval;
            audioService.PlaySfx("footstep", 0.35f, 0.15f);
        }

        /// <summary>
        /// Every uGUI button gives a tap. Menus are built in code at different times, so new buttons are picked
        /// up by a light periodic scan instead of each screen remembering to wire its own.
        /// </summary>
        void WireButtons()
        {
            if (Time.unscaledTime < nextButtonScan)
                return;
            nextButtonScan = Time.unscaledTime + ButtonScanSeconds;
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (wiredButtons.Add(button.GetInstanceID()))
                    button.onClick.AddListener(() => audioService.PlaySfx("ui_tap", 0.7f, 0.05f));
            }
        }
    }
}
