using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Listens for the dialogue action "cinematic:ending" — the chapter's closing beat, spoken
    /// at the last hand-in — and carries the game into the Ending scene's panel cinematic.
    /// Lives in every map scene alongside the shared UI.
    /// </summary>
    public class EndingDirector : MonoBehaviour
    {
        public const string ActionKey = "cinematic:ending";
        public const string SceneName = "Ending";

        void OnEnable()
        {
            Dialogue.DialogueRunner.ActionRequested += OnAction;
        }

        void OnDisable()
        {
            Dialogue.DialogueRunner.ActionRequested -= OnAction;
        }

        static void OnAction(string action)
        {
            if (action != ActionKey)
                return;
            if (Core.GameServices.Save.Current != null)
                Core.GameServices.Save.Current.SetFlag("act1.chapter_done");
            Core.GameServices.Flow.LoadScene(SceneName);
        }
    }
}
