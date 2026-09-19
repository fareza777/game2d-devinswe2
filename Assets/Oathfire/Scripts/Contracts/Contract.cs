using System;
using System.Collections.Generic;

namespace Oathfire.Contracts
{
    public enum ContractKind
    {
        /// <summary>Kill a number of a given enemy type.</summary>
        Cull,
        /// <summary>Deliver materials to the settlement stores.</summary>
        Supply,
        /// <summary>Raise buildings or clear a marked site.</summary>
        Labour,
        /// <summary>Survive a night with an extra condition.</summary>
        Vigil,
    }

    public enum ContractRank
    {
        Copper,
        Iron,
        Silver,
        Oathbound,
    }

    /// <summary>
    /// A posted job. Templates live in Resources/Contracts/board.json; the board rolls concrete contracts
    /// from them, so the notice board keeps offering fresh work without hand-writing every one.
    /// </summary>
    [Serializable]
    public class ContractTemplate
    {
        public string id;
        public ContractKind kind;
        public ContractRank rank;
        /// <summary>Counter or item key the contract tracks.</summary>
        public string target;
        public int minAmount = 3;
        public int maxAmount = 6;
        public int coinPerUnit = 6;
        public int experiencePerUnit = 12;
        public string faction;
        public int reputation;
        /// <summary>Item handed over on completion (optional).</summary>
        public string rewardItem;
        /// <summary>Contract only appears once this flag is set.</summary>
        public string requiresFlag;
        /// <summary>Nights before the posting expires; 0 means it stays until taken.</summary>
        public int expiresAfterNights;

        public string TitleKey => $"contract.{id}.title";
        public string BodyKey => $"contract.{id}.body";
        public string ClientKey => $"contract.{id}.client";
    }

    [Serializable]
    public class ContractTable
    {
        public ContractTemplate[] contracts;
    }

    /// <summary>A contract as posted: the template plus the rolled amount and its progress baseline.</summary>
    [Serializable]
    public class PostedContract
    {
        public string templateId;
        public int amount;
        /// <summary>Counter value when the contract was accepted, so only new progress counts.</summary>
        public int baseline;
        public bool accepted;
        public int postedOnNight;

        public ContractTemplate Template => ContractBoard.Template(templateId);
    }

    [Serializable]
    public class BoardState
    {
        public List<PostedContract> posted = new List<PostedContract>();
        public int lastRollNight = -1;
    }
}
