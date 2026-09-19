using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Dialogue
{
    /// <summary>
    /// A conversation loaded from Resources/Dialogue/&lt;id&gt;.json. Nodes are addressed by id, so branches,
    /// jumps and re-entry all work without an editor graph. Text lives in the localization tables.
    /// </summary>
    [Serializable]
    public class DialogueScript
    {
        public string id;
        public string startNode;
        public DialogueNode[] nodes;

        readonly Dictionary<string, DialogueNode> lookup = new Dictionary<string, DialogueNode>();

        public void Index()
        {
            lookup.Clear();
            foreach (DialogueNode node in nodes)
                lookup[node.id] = node;
        }

        public DialogueNode Node(string nodeId)
        {
            if (lookup.Count == 0)
                Index();
            return lookup.TryGetValue(nodeId, out DialogueNode node) ? node : null;
        }

        public static DialogueScript Load(string scriptId)
        {
            var asset = Resources.Load<TextAsset>($"Dialogue/{scriptId}");
            if (!asset)
            {
                Debug.LogError($"[Dialogue] Missing Resources/Dialogue/{scriptId}.json");
                return null;
            }
            var script = JsonUtility.FromJson<DialogueScript>(asset.text);
            script.Index();
            return script;
        }
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        /// <summary>Speaker id; empty for narration. Portraits load from Art/Portraits/&lt;speaker&gt;.</summary>
        public string speaker;
        /// <summary>Localization key for the line.</summary>
        public string textKey;
        /// <summary>Optional voice clip in Resources/Voice/&lt;clip&gt;.</summary>
        public string voiceClip;
        /// <summary>Portrait mood suffix, e.g. "angry" → brann_angry.</summary>
        public string mood;
        /// <summary>Flags set when this node is shown.</summary>
        public string[] setFlags;
        /// <summary>Item ids handed over when this node is shown, so a scene can pay off where it lands.</summary>
        public string[] giveItems;
        /// <summary>Item ids taken from the Warden when this node is shown (a letter delivered, proof handed over).</summary>
        public string[] takeItems;
        public int giveCoin;
        /// <summary>Node shown next when there are no choices; empty ends the conversation.</summary>
        public string next;
        public DialogueChoice[] choices;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string textKey;
        public string next;
        /// <summary>Kael's tone: iron, mercy or guile. Shown as a tag and tracked for the epilogue.</summary>
        public string tone;
        /// <summary>Only offered when this flag is set (empty = always).</summary>
        public string requiresFlag;
        /// <summary>Hidden when this flag is set.</summary>
        public string hiddenIfFlag;
        public string setFlag;
        public string faction;
        public int reputation;
        /// <summary>Coin this choice costs. Offered only when the player can pay, and deducted when taken.</summary>
        public int costCoin;
        /// <summary>Something to open when the conversation ends, e.g. "shop:sabel".</summary>
        public string action;
    }
}
