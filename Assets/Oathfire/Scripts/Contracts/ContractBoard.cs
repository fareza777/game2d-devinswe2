using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oathfire.Contracts
{
    /// <summary>
    /// The settlement's notice board. Rolls a handful of jobs from the templates each night, tracks the ones
    /// the player accepted, and pays out when their target is met. Progress is read from the same save
    /// counters the rest of the game writes, so a contract can never disagree with what actually happened.
    /// </summary>
    public static class ContractBoard
    {
        public const int PostingsPerNight = 4;
        const string StateKey = "board.state";

        /// <summary>Kinds that must appear on every board, in the order they claim a slot.</summary>
        static readonly ContractKind[] GuaranteedKinds = { ContractKind.Cull, ContractKind.Supply };

        static ContractTemplate[] templates;
        static BoardState state;

        public static IReadOnlyList<PostedContract> Posted => State.posted;

        static BoardState State
        {
            get
            {
                if (state == null)
                    Load();
                return state;
            }
        }

        public static ContractTemplate Template(string id)
        {
            EnsureTemplates();
            return templates.FirstOrDefault(template => template.id == id);
        }

        static void EnsureTemplates()
        {
            if (templates != null)
                return;
            var asset = Resources.Load<TextAsset>("Contracts/board");
            if (!asset)
            {
                Debug.LogError("[Contracts] Missing Resources/Contracts/board.json");
                templates = System.Array.Empty<ContractTemplate>();
                return;
            }
            templates = JsonUtility.FromJson<ContractTable>(asset.text).contracts;
        }

        static void Load()
        {
            EnsureTemplates();
            string json = PlayerPrefs.GetString(SaveKey(), string.Empty);
            state = string.IsNullOrEmpty(json) ? new BoardState() : JsonUtility.FromJson<BoardState>(json);
        }

        static string SaveKey() => $"{StateKey}.{Core.GameServices.Save.CurrentSlot}";

        static void Save() => PlayerPrefs.SetString(SaveKey(), JsonUtility.ToJson(state));

        /// <summary>Puts a fresh set of jobs on the board, keeping anything the player already accepted.</summary>
        public static void RollPostings(int night)
        {
            EnsureTemplates();
            if (State.lastRollNight == night)
                return;

            Save.SaveData save = Core.GameServices.Save.Current;
            State.posted.RemoveAll(posted => !posted.accepted);
            State.posted.RemoveAll(posted =>
                posted.Template != null && posted.Template.expiresAfterNights > 0 &&
                night - posted.postedOnNight > posted.Template.expiresAfterNights);

            List<ContractTemplate> available = templates
                .Where(template => string.IsNullOrEmpty(template.requiresFlag) || (save != null && save.HasFlag(template.requiresFlag)))
                .Where(template => State.posted.All(posted => posted.templateId != template.id))
                .ToList();

            var random = new System.Random(night * 7919 + 13);

            // A board of nothing but hauling jobs is a dead board, so the fighting and the fetching each
            // get a guaranteed slot before the rest of the night's work is drawn at random.
            foreach (ContractKind kind in GuaranteedKinds)
            {
                if (State.posted.Count(posted => !posted.accepted) >= PostingsPerNight)
                    break;
                if (State.posted.Any(posted => posted.Template != null && posted.Template.kind == kind))
                    continue;
                List<int> candidates = Enumerable.Range(0, available.Count)
                    .Where(index => available[index].kind == kind)
                    .ToList();
                if (candidates.Count > 0)
                    Post(available, candidates[random.Next(candidates.Count)], night, random);
            }

            while (State.posted.Count(posted => !posted.accepted) < PostingsPerNight && available.Count > 0)
                Post(available, random.Next(available.Count), night, random);

            State.lastRollNight = night;
            Save();
        }

        /// <summary>Moves one template off the shortlist and onto the board with a rolled amount.</summary>
        static void Post(List<ContractTemplate> available, int index, int night, System.Random random)
        {
            ContractTemplate template = available[index];
            available.RemoveAt(index);
            State.posted.Add(new PostedContract
            {
                templateId = template.id,
                amount = random.Next(template.minAmount, template.maxAmount + 1),
                postedOnNight = night,
            });
        }

        public static void Accept(PostedContract contract)
        {
            contract.accepted = true;
            contract.baseline = ProgressOf(contract, ignoreBaseline: true);
            Save();
            UI.Toast.Show("toast.contractTaken");
        }

        /// <summary>How far along an accepted contract is, counted from when it was taken.</summary>
        public static int ProgressOf(PostedContract contract, bool ignoreBaseline = false)
        {
            ContractTemplate template = contract.Template;
            if (template == null)
                return 0;

            Save.SaveData save = Core.GameServices.Save.Current;
            int raw = template.kind switch
            {
                ContractKind.Supply => Oathfire.Progress.PlayerState.Instance?.Inventory.Count(template.target) ?? 0,
                _ => save?.GetCounter(template.target) ?? 0,
            };
            // Supply contracts look at what is in the bag right now; the rest count new progress only.
            if (template.kind == ContractKind.Supply || ignoreBaseline)
                return raw;
            return Mathf.Max(0, raw - contract.baseline);
        }

        public static bool IsComplete(PostedContract contract) =>
            contract.accepted && ProgressOf(contract) >= contract.amount;

        public static bool TryClaim(PostedContract contract)
        {
            if (!IsComplete(contract))
                return false;
            ContractTemplate template = contract.Template;
            Oathfire.Progress.PlayerState player = Oathfire.Progress.PlayerState.Instance;
            if (template == null || player == null)
                return false;

            // Supply jobs take the goods; the rest are paid on report.
            if (template.kind == ContractKind.Supply && !player.Inventory.Remove(template.target, contract.amount))
                return false;

            player.Inventory.coin += template.coinPerUnit * contract.amount;
            player.AddExperience(template.experiencePerUnit * contract.amount);
            if (!string.IsNullOrEmpty(template.rewardItem))
                player.Inventory.Add(template.rewardItem);
            if (!string.IsNullOrEmpty(template.faction) && template.reputation != 0)
                Core.GameServices.Save.Current?.AddReputation(template.faction, template.reputation);

            Core.GameServices.Save.Current?.AddCounter("contracts.completed", 1);
            State.posted.Remove(contract);
            Save();
            Quests.QuestRuntime.Evaluate();
            UI.Toast.Show("toast.contractPaid");
            return true;
        }

        /// <summary>Forgets the cached board so the next read loads the current slot's own postings.</summary>
        public static void Reset()
        {
            state = null;
            templates = null;
        }

        /// <summary>Throws away a slot's board entirely, for when its run is being started over.</summary>
        public static void ClearForSlot(int slot)
        {
            PlayerPrefs.DeleteKey($"{StateKey}.{slot}");
            state = null;
        }
    }
}
