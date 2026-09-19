using System;
using UnityEngine;

namespace Oathfire.Items
{
    public enum ItemKind
    {
        Material,
        Consumable,
        Weapon,
        Armor,
        Trinket,
        Quest,
        Relic,
    }

    public enum EquipSlot
    {
        None,
        MainHand,
        OffHand,
        Body,
        Head,
        Trinket,
        // Appended, never inserted: saves store slots by number.
        Neck,
        Hands,
        Legs,
        Feet,
        Ring,
    }

    public enum ItemRarity
    {
        Common,
        Fine,
        Rare,
        Oathbound,
    }

    /// <summary>
    /// One entry of the item database (Resources/Items/items.json). Names and descriptions are localization
    /// keys so every item reads correctly in both languages.
    /// </summary>
    [Serializable]
    public class ItemDefinition
    {
        public string id;
        public ItemKind kind;
        public ItemRarity rarity;
        public EquipSlot slot = EquipSlot.None;
        public int stackSize = 1;
        public int value;
        public string icon;

        [Header("Combat")]
        public int damage;
        public int armor;
        public int health;
        public float attackSpeed;
        public float moveSpeed;

        [Header("Use")]
        public int healAmount;
        public int manaAmount;
        /// <summary>Flag set when the item is used or picked up; drives story gates.</summary>
        public string setsFlag;

        public string NameKey => $"item.{id}.name";
        public string DescriptionKey => $"item.{id}.desc";
        public bool IsEquipment => slot != EquipSlot.None;
    }

    [Serializable]
    public class ItemTable
    {
        public ItemDefinition[] items;
    }
}
