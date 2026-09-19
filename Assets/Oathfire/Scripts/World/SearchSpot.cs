using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Something to find in the world: a journal under a ruin's floor, a lamp dropped in the hay. It is only
    /// there while the quest that asks for it is open (the builder wraps it in a FlagGatedPresence), glows so it
    /// can be found, and gives its item when searched. It can wake something that was guarding it.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class SearchSpot : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] string foundFlag;
        [SerializeField] string promptKey = "prompt.search";
        [SerializeField] string foundToastKey = "toast.found";
        [Tooltip("Hostiles that rise when the spot is searched (empty for none).")]
        [SerializeField] GameObject[] guardians;
        [SerializeField] int guardianCount;
        [SerializeField] string guardianBannerKey;

        public string FoundFlag => foundFlag;

        void Awake()
        {
            WorldInteractable interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt(promptKey);
            interactable.Used += Search;
        }

        void Search()
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state != null && !string.IsNullOrEmpty(itemId))
                state.Inventory.Add(itemId);
            Core.GameServices.Audio.PlaySfx("item_pickup", 1f, 0f);
            Items.ItemDefinition item = Items.ItemDatabase.Get(itemId);
            UI.Toast.ShowText(Core.GameServices.Localization.Format(foundToastKey,
                item != null ? Core.GameServices.Localization.Get(item.NameKey) : itemId));
            WakeGuardians();
            // The flag hides the spot (its gate is blocked by it) and moves the quest on.
            Quests.QuestRuntime.SetFlag(foundFlag);
            Core.GameServices.Save.Autosave(foundFlag, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        void WakeGuardians()
        {
            if (guardians == null || guardians.Length == 0 || guardianCount <= 0)
                return;
            if (!string.IsNullOrEmpty(guardianBannerKey))
                UI.Toast.Show(guardianBannerKey);
            Core.GameServices.Audio.PlaySfx("enemy_alert", 1f, 0f);
            Vector2 here = transform.position;
            for (int i = 0; i < guardianCount; i++)
            {
                GameObject prefab = guardians[i % guardians.Length];
                if (!prefab)
                    continue;
                Vector3 at = SpawnPlanner.FindOpenPoint(here, here, 1.6f, 3.2f, i * 2.1f);
                GameObject guardian = Instantiate(prefab, at, prefab.transform.rotation);
                ActorSorting.Raise(guardian);
                EnemyScaling.Apply(guardian, 0.7f, 7);
                guardian.AddComponent<EnemyReachGuard>();
                EnemyHealth2D health = guardian.GetComponentInChildren<EnemyHealth2D>();
                if (health)
                    health.OnDied += () => Quests.QuestRuntime.CountKill("kills.skeleton");
                EnemyAI ai = guardian.GetComponentInChildren<EnemyAI>();
                if (ai && PlayerHealth.Instance)
                {
                    ai.returnToPost = false;
                    ai.OnAllyAlerted(PlayerHealth.Instance.transform.position, false);
                }
            }
        }
    }
}
