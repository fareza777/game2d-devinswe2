using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Quests
{
    /// <summary>
    /// Tracks quest progress against the save file. Quests are evaluated from flags, counters and inventory,
    /// so a loaded save always lands on the right stage without storing quest state separately.
    /// </summary>
    public class QuestService
    {
        readonly List<QuestDefinition> quests = new List<QuestDefinition>();

        public event Action<QuestDefinition, QuestStage> StageCompleted;
        public event Action<QuestDefinition> QuestCompleted;
        /// <summary>Raised when side work is finished and waiting to be handed back.</summary>
        public event Action<QuestDefinition> QuestReady;

        public IReadOnlyList<QuestDefinition> Quests => quests;

        public void Load(string chapterId)
        {
            quests.Clear();
            var asset = Resources.Load<TextAsset>($"Quests/{chapterId}");
            if (!asset)
            {
                Debug.LogError($"[Quests] Missing Resources/Quests/{chapterId}.json");
                return;
            }
            quests.AddRange(JsonUtility.FromJson<QuestTable>(asset.text).quests);
        }

        public bool IsAvailable(QuestDefinition quest)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || (!string.IsNullOrEmpty(quest.unlockFlag) && !save.HasFlag(quest.unlockFlag)))
                return false;
            // Work someone has to offer is not work the Warden has: it counts from the moment it is accepted.
            return string.IsNullOrEmpty(quest.giver) || save.HasFlag(quest.TakenFlag);
        }

        /// <summary>The work this character has to hand out right now: unlocked by the story, not yet taken.</summary>
        public IEnumerable<QuestDefinition> OffersFrom(string speakerKey)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || string.IsNullOrEmpty(speakerKey))
                yield break;
            foreach (QuestDefinition quest in quests)
            {
                if (quest.giver != speakerKey || save.HasFlag(quest.TakenFlag))
                    continue;
                if (!string.IsNullOrEmpty(quest.unlockFlag) && !save.HasFlag(quest.unlockFlag))
                    continue;
                yield return quest;
            }
        }

        /// <summary>Takes the work on: it enters the journal, the arrow can point at it, and it can be finished.</summary>
        public void Accept(QuestDefinition quest)
        {
            Core.GameServices.Save.Current?.SetFlag(quest.TakenFlag);
            Evaluate();
        }

        public bool IsComplete(QuestDefinition quest)
        {
            QuestStage last = quest.stages.Length > 0 ? quest.stages[^1] : null;
            return last != null && !string.IsNullOrEmpty(last.setsFlag) && Core.GameServices.Save.Current.HasFlag(last.setsFlag);
        }

        /// <summary>The stage the player is currently working on, or null when the quest is done or locked.</summary>
        public QuestStage CurrentStage(QuestDefinition quest)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            // A quest whose final flag is set is finished even if the story skipped an earlier stage's flag
            // (the placeholder prologue sets prologue.done directly); otherwise it would haunt the HUD forever.
            if (save == null || !IsAvailable(quest) || IsComplete(quest))
                return null;
            foreach (QuestStage stage in quest.stages)
            {
                if (string.IsNullOrEmpty(stage.setsFlag) || !save.HasFlag(stage.setsFlag))
                    return stage;
            }
            return null;
        }

        /// <summary>True when the work is done and waits only on being handed back to the giver.</summary>
        public bool IsReadyToHandIn(QuestDefinition quest)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || !quest.HasGiver || !IsAvailable(quest) || IsComplete(quest))
                return false;
            if (!save.HasFlag(quest.ReadyFlag))
                return false;
            // Items can be sold or used after the work was marked done; it is only ready while they are still carried.
            QuestStage last = quest.stages[^1];
            foreach (QuestObjective objective in last.objectives)
                if (!objective.optional && !IsObjectiveMet(objective))
                    return false;
            return true;
        }

        /// <summary>Work finished for this character and waiting to be handed back.</summary>
        public IEnumerable<QuestDefinition> ReadyFor(string speakerKey)
        {
            foreach (QuestDefinition quest in quests)
                if (quest.giver == speakerKey && IsReadyToHandIn(quest))
                    yield return quest;
        }

        /// <summary>
        /// Hands the work back: the things that were asked for change hands, the quest closes and pays out.
        /// </summary>
        public bool HandIn(QuestDefinition quest)
        {
            if (!IsReadyToHandIn(quest))
                return false;
            Save.SaveData save = Core.GameServices.Save.Current;
            Progress.PlayerState state = Progress.PlayerState.Instance;
            foreach (QuestStage stage in quest.stages)
                foreach (QuestObjective objective in stage.objectives)
                    if (objective.kind == ObjectiveKind.Item && !objective.optional && state != null)
                        state.Inventory.Remove(objective.key, objective.amount);
            QuestStage last = quest.stages[^1];
            if (!string.IsNullOrEmpty(last.setsFlag))
                save.SetFlag(last.setsFlag);
            StageCompleted?.Invoke(quest, last);
            GrantReward(quest.reward);
            if (save.trackedQuest == quest.id)
                save.trackedQuest = string.Empty;
            QuestCompleted?.Invoke(quest);
            Evaluate();
            // A closed chapter earns a breath; side work never earns a billboard.
            if (quest.mainQuest)
                Core.GameServices.Ads?.ShowInterstitialIfReady();
            return true;
        }

        public bool IsObjectiveMet(QuestObjective objective)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return false;
            return objective.kind switch
            {
                ObjectiveKind.Flag => save.HasFlag(objective.key),
                ObjectiveKind.Counter => save.GetCounter(objective.key) >= objective.amount,
                ObjectiveKind.Item => Core.GameServices.Save.Current != null && InventoryCount(objective.key) >= objective.amount,
                _ => false,
            };
        }

        static int InventoryCount(string itemId) => Progress.PlayerState.Instance?.Inventory.Count(itemId) ?? 0;

        /// <summary>Call after anything that could advance a quest: a flag, a kill, a pickup.</summary>
        public void Evaluate()
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return;

            // Several stages can already be met at once (a conversation sets two flags, a kill finishes a count
            // that was nearly done): keep advancing until the current stage is not yet satisfied.
            foreach (QuestDefinition quest in quests)
                while (AdvanceOnce(quest))
                {
                }
        }

        bool AdvanceOnce(QuestDefinition quest)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            QuestStage stage = CurrentStage(quest);
            if (stage == null)
                return false;

            bool allMet = true;
            foreach (QuestObjective objective in stage.objectives)
            {
                if (objective.optional)
                    continue;
                if (!IsObjectiveMet(objective))
                {
                    allMet = false;
                    break;
                }
            }
            if (!allMet)
                return false;

            // Side work ends with the Warden going back to whoever asked. The last stage waits, marked ready,
            // until they are spoken to; only then is anything handed over and the reward paid.
            bool lastStage = quest.stages.Length > 0 && quest.stages[^1] == stage;
            if (lastStage && quest.HasGiver)
            {
                if (!save.HasFlag(quest.ReadyFlag))
                {
                    save.SetFlag(quest.ReadyFlag);
                    QuestReady?.Invoke(quest);
                }
                return false;
            }

            if (!string.IsNullOrEmpty(stage.setsFlag))
                save.SetFlag(stage.setsFlag);
            StageCompleted?.Invoke(quest, stage);

            if (CurrentStage(quest) == null)
            {
                GrantReward(quest.reward);
                QuestCompleted?.Invoke(quest);
                return false;
            }
            // A stage without a flag of its own cannot be told apart from the next pass; stop rather than loop.
            return !string.IsNullOrEmpty(stage.setsFlag);
        }

        static void GrantReward(QuestReward reward)
        {
            if (reward == null)
                return;
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state != null)
            {
                state.AddExperience(reward.experience);
                state.Inventory.coin += reward.coin;
                if (reward.items != null)
                    foreach (string itemId in reward.items)
                        state.Inventory.Add(itemId);
            }
            if (!string.IsNullOrEmpty(reward.faction) && reward.reputation != 0)
                Core.GameServices.Save.Current.AddReputation(reward.faction, reward.reputation);
        }
    }
}
