using UnityEngine;

namespace Oathfire.Quests
{
    /// <summary>Scene-side access to the quest service, so world objects can report progress in one call.</summary>
    public static class QuestRuntime
    {
        static QuestService service;

        public static QuestService Service
        {
            get
            {
                if (service == null)
                {
                    service = new QuestService();
                    service.Load(Core.GameServices.Save.Current?.chapterId ?? "chapter1");
                }
                return service;
            }
        }

        public static void Evaluate() => Service.Evaluate();

        public static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return;
            Core.GameServices.Save.Current?.SetFlag(flag);
            Evaluate();
        }

        public static void CountKill(string counterKey)
        {
            Core.GameServices.Save.Current?.AddCounter(counterKey, 1);
            Evaluate();
        }

        /// <summary>
        /// The quest the arrow and the HUD follow: the one chosen in the journal while it is still open,
        /// otherwise the story. Before this the arrow only ever pointed along the main quest, so side work
        /// had to be found by memory.
        /// </summary>
        public static QuestDefinition FocusedQuest()
        {
            QuestDefinition tracked = TrackedQuest();
            return tracked ?? ActiveMainQuest();
        }

        public static QuestDefinition TrackedQuest()
        {
            string id = Core.GameServices.Save.Current?.trackedQuest;
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (QuestDefinition quest in Service.Quests)
                if (quest.id == id && Service.IsAvailable(quest) && Service.CurrentStage(quest) != null)
                    return quest;
            return null;
        }

        /// <summary>Follows this quest (or the story again when it is already being followed).</summary>
        public static void Track(QuestDefinition quest)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return;
            save.trackedQuest = quest == null || save.trackedQuest == quest.id ? string.Empty : quest.id;
        }

        /// <summary>The line to show for where a quest stands: its current step, or "return to" when it is done.</summary>
        public static string StepText(QuestDefinition quest)
        {
            if (Service.IsReadyToHandIn(quest))
                return Core.GameServices.Localization.Format("quest.returnTo", Core.GameServices.Localization.Get(quest.giver));
            QuestStage stage = Service.CurrentStage(quest);
            return stage == null ? string.Empty : Core.GameServices.Localization.Get(stage.DescriptionKeyFor(quest.id));
        }

        public static QuestDefinition ActiveMainQuest()
        {
            foreach (QuestDefinition quest in Service.Quests)
            {
                if (!quest.mainQuest || !Service.IsAvailable(quest))
                    continue;
                if (Service.CurrentStage(quest) != null)
                    return quest;
            }
            return null;
        }

        public static void Reset() => service = null;
    }
}
