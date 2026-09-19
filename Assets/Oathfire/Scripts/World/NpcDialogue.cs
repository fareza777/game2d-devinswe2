using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A villager the player can talk to. Which conversation plays depends on story flags, so the same
    /// character says different things as the chapter moves on.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class NpcDialogue : MonoBehaviour
    {
        [System.Serializable]
        public class Conversation
        {
            public string scriptId;
            [Tooltip("Played only when this flag is set (empty = always available).")]
            public string requiresFlag;
            [Tooltip("Skipped once this flag is set.")]
            public string consumedFlag;
            [Tooltip("Played only while the Warden carries this item (empty = no item needed).")]
            public string requiresItem;
        }

        [SerializeField] string speakerKey = "speaker.brann";
        [SerializeField] Conversation[] conversations;

        WorldInteractable interactable;

        void Awake()
        {
            interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt("prompt.talk");
            interactable.Used += Talk;
        }

        /// <summary>
        /// Used for visitors the event director spawns at runtime: they exist for one scripted scene and
        /// have no authored conversation list to pick from.
        /// </summary>
        public void Configure(string speaker, string scriptId)
        {
            speakerKey = speaker;
            conversations = new[] { new Conversation { scriptId = scriptId } };
        }

        void Talk()
        {
            foreach (Quests.QuestDefinition ready in Quests.QuestRuntime.Service.ReadyFor(speakerKey))
            {
                if (UI.QuestOfferPanel.Instance)
                {
                    UI.QuestOfferPanel.Instance.ShowHandIn(ready, OfferWork);
                    return;
                }
            }
            Conversation conversation = PickConversation();
            if (conversation == null)
            {
                UI.Toast.Show("toast.nothingToSay");
                return;
            }

            Controls.MobileControls.GameplayActive = false;
            Dialogue.DialogueRunner.Instance.Play(conversation.scriptId, () =>
            {
                Controls.MobileControls.GameplayActive = true;
                Quests.QuestRuntime.Evaluate();
                Core.GameServices.Save.Autosave($"talk.{conversation.scriptId}", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                OfferWork();
            });
        }

        Conversation PickConversation()
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            Conversation fallback = null;
            foreach (Conversation conversation in conversations)
            {
                bool unlocked = (string.IsNullOrEmpty(conversation.requiresFlag) || (save != null && save.HasFlag(conversation.requiresFlag)))
                                && (string.IsNullOrEmpty(conversation.requiresItem)
                                    || (Progress.PlayerState.Instance?.Inventory.Count(conversation.requiresItem) ?? 0) > 0);
                bool spent = !string.IsNullOrEmpty(conversation.consumedFlag) && save != null && save.HasFlag(conversation.consumedFlag);
                if (!unlocked)
                    continue;
                if (!spent)
                    return conversation;
                fallback = conversation;
            }
            return fallback;
        }

        public string SpeakerKey => speakerKey;

        /// <summary>The work this character is holding out, if any.</summary>
        public Quests.QuestDefinition PendingOffer()
        {
            foreach (Quests.QuestDefinition quest in Quests.QuestRuntime.Service.OffersFrom(speakerKey))
                return quest;
            return null;
        }

        /// <summary>True when this character is waiting for finished work to be brought back.</summary>
        public bool WaitingForHandIn()
        {
            foreach (Quests.QuestDefinition quest in Quests.QuestRuntime.Service.ReadyFor(speakerKey))
                return true;
            return false;
        }

        /// <summary>After the conversation, whatever they are asking for is put to the Warden as a card.</summary>
        void OfferWork()
        {
            Quests.QuestDefinition offer = PendingOffer();
            if (offer != null && UI.QuestOfferPanel.Instance && !UI.ShopPanel.IsOpen)
                UI.QuestOfferPanel.Instance.Show(offer);
        }
    }
}
