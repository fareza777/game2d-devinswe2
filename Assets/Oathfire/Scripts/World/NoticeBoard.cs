using UnityEngine;

namespace Oathfire.World
{
    /// <summary>The settlement's notice board: walk up, press Use, and the contract sheet opens.</summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class NoticeBoard : MonoBehaviour
    {
        void Awake()
        {
            var interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt("prompt.board");
            interactable.Used += Open;
        }

        static void Open()
        {
            UI.ContractBoardPanel panel = FindAnyObjectByType<UI.ContractBoardPanel>(FindObjectsInactive.Include);
            if (panel)
                panel.Toggle();
            else
                UI.Toast.Show("toast.nothingToSay");
        }
    }
}
