using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A marked plot inside the hearth light. Spend materials to raise a building; the finished building
    /// widens the oathfire's protected radius, which is the loop the whole chapter is built on.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class BuildSite : MonoBehaviour
    {
        [SerializeField] string buildingId = "shelter";
        [SerializeField] int timberCost = 6;
        [SerializeField] int stoneCost = 2;
        [SerializeField] float radiusBonus = 1.2f;
        [SerializeField] GameObject ghostVisual;
        [SerializeField] GameObject builtVisual;
        [SerializeField] AudioClip buildSound;

        WorldInteractable interactable;
        bool built;

        string BuiltFlag => $"built.{buildingId}.{name}";

        public bool IsBuilt => built;

        void Awake()
        {
            interactable = GetComponent<WorldInteractable>();
            interactable.Used += TryBuild;
            RefreshPrompt();
        }

        void Start()
        {
            if (Core.GameServices.Save.Current != null && Core.GameServices.Save.Current.HasFlag(BuiltFlag))
                Complete(silent: true);
            else
                ShowGhost();
        }

        void RefreshPrompt()
        {
            interactable.SetPrompt(built ? "prompt.built" : $"prompt.build.{buildingId}");
        }

        void TryBuild()
        {
            if (built)
                return;

            Items.Inventory inventory = Progress.PlayerState.Instance?.Inventory;
            if (inventory == null)
                return;

            if (inventory.Count("mat_timber") < timberCost || inventory.Count("mat_stone") < stoneCost)
            {
                UI.Toast.Show("toast.needMaterials");
                return;
            }

            inventory.Remove("mat_timber", timberCost);
            inventory.Remove("mat_stone", stoneCost);
            Complete(silent: false);
        }

        void Complete(bool silent)
        {
            built = true;
            if (ghostVisual)
                ghostVisual.SetActive(false);
            if (builtVisual)
                builtVisual.SetActive(true);
            RefreshPrompt();

            if (silent)
                return;

            Save.SaveData save = Core.GameServices.Save.Current;
            save?.SetFlag(BuiltFlag);
            save?.AddCounter($"built.{buildingId}", 1);
            OathfireHearth.Instance?.Expand(radiusBonus);
            if (buildSound)
                Core.GameServices.Audio.PlaySfx(buildSound);
            else
                Core.GameServices.Audio.PlaySfx("build");
            UI.Toast.Show("toast.built");
            Quests.QuestRuntime.Evaluate();
        }

        void ShowGhost()
        {
            if (ghostVisual)
                ghostVisual.SetActive(true);
            if (builtVisual)
                builtVisual.SetActive(false);
        }
    }
}
