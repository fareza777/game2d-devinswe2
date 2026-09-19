using System.Collections;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A named enemy who is standing there when you arrive, says what they have to say, and then fights. The
    /// stand-in is a harmless body built into the scene; the fighter is spawned in its place when the talking
    /// stops, with its name and health across the top so the fight reads as the one the story promised.
    /// </summary>
    public class BossEncounter : MonoBehaviour
    {
        [SerializeField] GameObject prefab;
        [SerializeField] ActorLook look;
        [SerializeField] GameObject standIn;
        [SerializeField] string nameKey = "boss.rook";
        [SerializeField] string introScriptId;
        [SerializeField] string defeatedFlag;
        [SerializeField] string defeatedToastKey = "toast.bossDown";
        [SerializeField] float triggerRadius = 3.2f;
        [SerializeField] float healthMultiplier = 0.9f;
        [SerializeField] int hitDamage = 11;
        [SerializeField] float sizeBoost = 1.18f;

        EnemyHealth2D fighter;

        public enum Phase { Waiting, Talking, Fighting, Defeated }

        public Phase Current { get; private set; } = Phase.Waiting;
        public EnemyHealth2D Fighter => fighter;
        public string DefeatedFlag => defeatedFlag;

        void Start()
        {
            if (IsDefeated)
                Resolve(silently: true);
        }

        bool IsDefeated
        {
            get
            {
                Save.SaveData save = Core.GameServices.Save.Current;
                return save != null && !string.IsNullOrEmpty(defeatedFlag) && save.HasFlag(defeatedFlag);
            }
        }

        void Update()
        {
            switch (Current)
            {
                case Phase.Waiting:
                    if (!PlayerHealth.Instance || PlayerHealth.IsPlayerDead || Dialogue.DialogueRunner.IsConversationActive)
                        return;
                    if (Vector2.Distance(PlayerHealth.Instance.transform.position, transform.position) <= triggerRadius)
                        StartCoroutine(Confront());
                    break;
                case Phase.Fighting:
                    if (!fighter || fighter.IsDead)
                    {
                        Resolve(silently: false);
                        break;
                    }
                    WorldEventDirector.SetBanner(Core.GameServices.Localization.Format("boss.banner",
                        Core.GameServices.Localization.Get(nameKey), Mathf.Max(0, fighter.CurrentHealth), fighter.maxHealth));
                    break;
            }
        }

        IEnumerator Confront()
        {
            Current = Phase.Talking;
            if (!string.IsNullOrEmpty(introScriptId) && Dialogue.DialogueRunner.Instance)
            {
                bool finished = false;
                Controls.MobileControls.GameplayActive = false;
                Dialogue.DialogueRunner.Instance.Play(introScriptId, () => finished = true);
                while (!finished && Dialogue.DialogueRunner.IsConversationActive)
                    yield return null;
                Controls.MobileControls.GameplayActive = true;
            }
            BeginFight();
        }

        /// <summary>Starts the fight at once, with no words first. Used after the intro and by the smoke test.</summary>
        public void BeginFight()
        {
            if (Current == Phase.Fighting || Current == Phase.Defeated)
                return;
            StopAllCoroutines();
            Current = Phase.Fighting;
            if (!prefab)
            {
                Debug.LogError($"[Oathfire] {name} has no fighter prefab");
                Resolve(silently: true);
                return;
            }

            Vector3 at = standIn ? standIn.transform.position : transform.position;
            if (standIn)
                standIn.SetActive(false);

            GameObject instance = Instantiate(prefab, at, prefab.transform.rotation);
            instance.name = Core.GameServices.Localization.Get(nameKey);
            ActorSorting.Raise(instance);
            look?.ApplyTo(instance);
            instance.transform.localScale *= sizeBoost;
            EnemyScaling.Apply(instance, healthMultiplier, hitDamage);
            instance.AddComponent<EnemyReachGuard>();
            instance.AddComponent<BossMarker>().nameKey = nameKey;

            fighter = instance.GetComponentInChildren<EnemyHealth2D>();
            EnemyAI ai = instance.GetComponentInChildren<EnemyAI>();
            if (ai)
            {
                ai.returnToPost = false;
                if (PlayerHealth.Instance)
                    ai.OnAllyAlerted(PlayerHealth.Instance.transform.position, false);
            }
            Core.GameServices.Audio.PlaySfx("enemy_alert", 1f, 0f);
        }

        void Resolve(bool silently)
        {
            Current = Phase.Defeated;
            if (standIn)
                standIn.SetActive(false);
            WorldEventDirector.SetBanner(string.Empty);
            if (silently)
                return;
            Quests.QuestRuntime.SetFlag(defeatedFlag);
            UI.Toast.Show(defeatedToastKey);
            Core.GameServices.Audio.PlaySfx("quest_complete", 0.9f, 0f);
            Core.GameServices.Save.Autosave(defeatedFlag, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void OnDestroy()
        {
            if (Current == Phase.Fighting)
                WorldEventDirector.SetBanner(string.Empty);
        }
    }
}
