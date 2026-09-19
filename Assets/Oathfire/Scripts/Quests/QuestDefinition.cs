using System;

namespace Oathfire.Quests
{
    public enum ObjectiveKind
    {
        /// <summary>Completed when a story flag is set (talk, arrive, watch a scene).</summary>
        Flag,
        /// <summary>Completed when a counter reaches its target (kills, deliveries, buildings raised).</summary>
        Counter,
        /// <summary>Completed when the player holds enough of an item.</summary>
        Item,
    }

    /// <summary>
    /// A quest from Resources/Quests/&lt;chapter&gt;.json. Stages run in order; each stage has objectives that
    /// must all be met. Optional branches let one quest end in different states depending on player choices.
    /// </summary>
    [Serializable]
    public class QuestDefinition
    {
        public string id;
        public string act;
        public bool mainQuest = true;
        /// <summary>Quest becomes available when this flag is set (empty = from the start of its act).</summary>
        public string unlockFlag;
        /// <summary>
        /// Who hands this work out (a speaker key). Side work is offered in person and only enters the journal
        /// once taken; the story quests have no giver and follow the chapter on their own.
        /// </summary>
        public string giver;
        /// <summary>The map the giver stands in, so the arrow can lead the Warden back to them.</summary>
        public string giverScene;
        public QuestStage[] stages;
        public QuestReward reward;

        public string TitleKey => $"quest.{id}.title";
        /// <summary>Set once the Warden has agreed to the work.</summary>
        public string TakenFlag => $"quest.{id}.taken";
        /// <summary>Set when the work is done and only the handing back remains.</summary>
        public string ReadyFlag => $"quest.{id}.ready";
        public bool HasGiver => !string.IsNullOrEmpty(giver);
        public string SummaryKey => $"quest.{id}.summary";
    }

    [Serializable]
    public class QuestStage
    {
        public string id;
        public QuestObjective[] objectives;
        /// <summary>Flag set when this stage completes; drives the next stage, dialogue and events.</summary>
        public string setsFlag;

        public string DescriptionKeyFor(string questId) => $"quest.{questId}.{id}";
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveKind kind;
        /// <summary>Flag name, counter key or item id, depending on kind.</summary>
        public string key;
        public int amount = 1;
        public bool optional;
    }

    [Serializable]
    public class QuestReward
    {
        public int experience;
        public int coin;
        public string[] items;
        public string faction;
        public int reputation;
    }

    [Serializable]
    public class QuestTable
    {
        public QuestDefinition[] quests;
    }
}
