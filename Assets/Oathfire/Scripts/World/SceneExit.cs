using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// The edge of a location: a road out, a village gate. Using it saves the run and travels to the next
    /// scene, so leaving somewhere is a deliberate act rather than walking off the end of the tiles.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class SceneExit : MonoBehaviour
    {
        [SerializeField] string destinationScene = "Rennfall";
        [Tooltip("Where a hero arriving from this road stands. Unset: open ground is searched near the road.")]
        [SerializeField] Transform arrivalPoint;

        public string Destination => destinationScene;
        public Transform ArrivalPoint => arrivalPoint;
        [SerializeField] string promptKey = "prompt.travel";
        [Tooltip("Set once the player has taken this road, so quests can tell where they have been.")]
        [SerializeField] string setsFlag;

        void Awake()
        {
            WorldInteractable interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt(promptKey);
            interactable.Used += Travel;
        }

        void Travel()
        {
            if (Core.GameServices.Flow && Core.GameServices.Flow.IsBusy)
                return;
            if (!string.IsNullOrEmpty(setsFlag))
                Quests.QuestRuntime.SetFlag(setsFlag);
            Core.GameServices.Save.Autosave($"travel.{destinationScene}", destinationScene);
            Core.GameServices.Audio.PlaySfx("travel", 1f, 0f);
            Core.GameServices.Flow.LoadScene(destinationScene);
        }
    }
}
