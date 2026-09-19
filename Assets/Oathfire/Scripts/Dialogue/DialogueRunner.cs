using System;
using System.Collections;
using UnityEngine;

namespace Oathfire.Dialogue
{
    /// <summary>
    /// Plays a DialogueScript through a DialoguePanel: shows a line, waits for tap or voice end,
    /// applies flags and reputation, and resolves branches.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        public static DialogueRunner Instance { get; private set; }

        [SerializeField] DialoguePanel panel;

        public bool IsRunning { get; private set; }

        /// <summary>True while any conversation is on screen, so gameplay input can stand down.</summary>
        public static bool IsConversationActive => Instance && Instance.IsRunning;
        public event Action<string> ConversationFinished;

        /// <summary>Raised when a conversation ends on a choice that opens something (a shop, for one).</summary>
        public static event Action<string> ActionRequested;

        string pendingAction;

        void Awake()
        {
            Instance = this;
            Core.GameServices.EnsureCreated();
            if (!panel)
                panel = GetComponentInChildren<DialoguePanel>(true);
        }

        public void Play(string scriptId, Action onFinished = null)
        {
            if (IsRunning)
                return;
            DialogueScript script = DialogueScript.Load(scriptId);
            if (script == null)
                return;
            StartCoroutine(PlayRoutine(script, onFinished));
        }

        public IEnumerator PlayRoutine(DialogueScript script, Action onFinished = null)
        {
            IsRunning = true;
            pendingAction = null;
            panel.Show(true);

            DialogueNode node = script.Node(script.startNode) ?? (script.nodes.Length > 0 ? script.nodes[0] : null);
            while (node != null)
            {
                ApplyFlags(node.setFlags);
                ApplyGifts(node);
                yield return panel.ShowLineRoutine(node);

                if (node.choices != null && node.choices.Length > 0)
                {
                    int picked = -1;
                    yield return panel.ShowChoicesRoutine(node.choices, index => picked = index);
                    if (picked < 0)
                        break;
                    DialogueChoice choice = panel.VisibleChoices[picked];
                    ApplyChoice(choice);
                    if (!string.IsNullOrEmpty(choice.action))
                        pendingAction = choice.action;
                    node = string.IsNullOrEmpty(choice.next) ? null : script.Node(choice.next);
                }
                else
                {
                    node = string.IsNullOrEmpty(node.next) ? null : script.Node(node.next);
                }
            }

            panel.Show(false);
            IsRunning = false;
            // What the conversation opens comes first, so the caller's own follow-up (a work offer) sees it open.
            if (!string.IsNullOrEmpty(pendingAction))
                ActionRequested?.Invoke(pendingAction);
            onFinished?.Invoke();
            ConversationFinished?.Invoke(script.id);
        }

        static void ApplyFlags(string[] flags)
        {
            if (flags == null || Core.GameServices.Save.Current == null)
                return;
            foreach (string flag in flags)
                Core.GameServices.Save.Current.SetFlag(flag);
        }

        /// <summary>Hands over whatever this line promises, so a reward lands in the scene that earned it.</summary>
        static void ApplyGifts(DialogueNode node)
        {
            Progress.PlayerState player = Progress.PlayerState.Instance;
            if (player == null)
                return;
            if (node.takeItems != null)
                foreach (string itemId in node.takeItems)
                    player.Inventory.Remove(itemId);
            if (node.giveItems != null && node.giveItems.Length > 0)
            {
                foreach (string itemId in node.giveItems)
                    player.Inventory.Add(itemId);
                Core.GameServices.Audio.PlaySfx("item_pickup");
            }
            if (node.giveCoin != 0)
            {
                player.Inventory.coin += node.giveCoin;
                Core.GameServices.Audio.PlaySfx("coin");
            }
        }

        static void ApplyChoice(DialogueChoice choice)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return;
            if (choice.costCoin > 0 && Progress.PlayerState.Instance != null)
                Progress.PlayerState.Instance.Inventory.coin -= choice.costCoin;
            if (!string.IsNullOrEmpty(choice.setFlag))
                save.SetFlag(choice.setFlag);
            if (!string.IsNullOrEmpty(choice.tone))
                save.AddCounter($"tone.{choice.tone}", 1);
            if (!string.IsNullOrEmpty(choice.faction) && choice.reputation != 0)
                save.AddReputation(choice.faction, choice.reputation);
        }
    }
}
