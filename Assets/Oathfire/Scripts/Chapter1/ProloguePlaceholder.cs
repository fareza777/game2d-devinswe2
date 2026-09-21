using System.Collections;
using UnityEngine;

namespace Oathfire
{
    /// <summary>
    /// Bridges the opening cinematic into the settlement until the playable palace prologue is built:
    /// starts the chapter, grants Kael what the court let him keep, and sends him to Rennfall.
    /// </summary>
    public class ProloguePlaceholder : MonoBehaviour
    {
        const string RennfallScene = "Rennfall";

        public static readonly (string item, int count)[] StartingKit =
        {
            ("quest_testament", 1),
            ("sword_guard_issue", 1),
            ("armor_padded", 1),
            ("potion_small", 2),
            ("torch", 3),
            ("food_bread", 4),
        };

        IEnumerator Start()
        {
            Core.GameServices.EnsureCreated();
            Save.SaveData save = Core.GameServices.Save.Current ?? Core.GameServices.Save.NewGame();

            if (!save.HasFlag("prologue.kit_granted"))
            {
                Progress.PlayerState state = Progress.PlayerState.Instance;
                if (state == null)
                    state = new GameObject("PlayerState", typeof(Progress.PlayerState)).GetComponent<Progress.PlayerState>();

                foreach ((string item, int count) in StartingKit)
                    state.Inventory.Add(item, count);
                state.Inventory.Equip("sword_guard_issue");
                state.Inventory.Equip("armor_padded");
                state.Inventory.Add("boots_leather");
                state.Inventory.Equip("boots_leather");
                state.Inventory.Add("legs_leather");
                state.Inventory.Equip("legs_leather");
                state.Inventory.coin = 25;
                // The kit above stands in for the prologue's item reward; its experience is still owed, since the
                // quest is marked done directly and so never pays out through the quest service.
                Quests.QuestDefinition prologue = System.Linq.Enumerable.FirstOrDefault(Quests.QuestRuntime.Service.Quests, quest => quest.id == "q_prologue");
                if (prologue?.reward != null)
                    state.AddExperience(prologue.reward.experience);

                save.SetFlag("prologue.kit_granted");
                save.SetFlag("prologue.reached_queen");
                save.SetFlag("prologue.trial_answered");
                save.SetFlag("prologue.done");
                Quests.QuestRuntime.Evaluate();
            }

            Core.GameServices.Save.Autosave("prologue.done", RennfallScene);
            yield return Core.GameServices.Flow.LoadSceneRoutine(RennfallScene);
        }
    }
}
