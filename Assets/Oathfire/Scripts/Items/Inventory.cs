using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Items
{
    /// <summary>
    /// Player inventory and equipment. Lives in the save file, so it survives scene changes and loads.
    /// Equipment bonuses are summed on demand rather than cached, which keeps loading a save trivial.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        public List<Stack> stacks = new List<Stack>();
        public List<Equipped> equipment = new List<Equipped>();
        public int coin;

        public event Action Changed;

        [Serializable]
        public class Stack
        {
            public string itemId;
            public int count;
        }

        [Serializable]
        public class Equipped
        {
            public EquipSlot slot;
            public string itemId;
        }

        public int Count(string itemId)
        {
            int total = 0;
            foreach (Stack stack in stacks)
                if (stack.itemId == itemId)
                    total += stack.count;
            return total;
        }

        public void Add(string itemId, int count = 1)
        {
            ItemDefinition definition = ItemDatabase.Get(itemId);
            if (definition == null || count <= 0)
                return;

            int remaining = count;
            foreach (Stack stack in stacks)
            {
                if (stack.itemId != itemId || stack.count >= definition.stackSize)
                    continue;
                int space = definition.stackSize - stack.count;
                int moved = Mathf.Min(space, remaining);
                stack.count += moved;
                remaining -= moved;
                if (remaining == 0)
                    break;
            }
            while (remaining > 0)
            {
                int moved = Mathf.Min(definition.stackSize, remaining);
                stacks.Add(new Stack { itemId = itemId, count = moved });
                remaining -= moved;
            }

            if (!string.IsNullOrEmpty(definition.setsFlag))
                Core.GameServices.Save.Current?.SetFlag(definition.setsFlag);
            Changed?.Invoke();
        }

        public bool Remove(string itemId, int count = 1)
        {
            if (Count(itemId) < count)
                return false;

            int remaining = count;
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (stacks[i].itemId != itemId)
                    continue;
                int taken = Mathf.Min(stacks[i].count, remaining);
                stacks[i].count -= taken;
                remaining -= taken;
                if (stacks[i].count == 0)
                    stacks.RemoveAt(i);
            }
            Changed?.Invoke();
            return true;
        }

        public string EquippedIn(EquipSlot slot)
        {
            foreach (Equipped entry in equipment)
                if (entry.slot == slot)
                    return entry.itemId;
            return null;
        }

        /// <summary>Equips an item from the bag; anything already in that slot goes back to the bag.</summary>
        public bool Equip(string itemId)
        {
            ItemDefinition definition = ItemDatabase.Get(itemId);
            if (definition == null || !definition.IsEquipment || Count(itemId) == 0)
                return false;

            string previous = EquippedIn(definition.slot);
            if (!string.IsNullOrEmpty(previous))
                Add(previous);

            equipment.RemoveAll(entry => entry.slot == definition.slot);
            equipment.Add(new Equipped { slot = definition.slot, itemId = itemId });
            Remove(itemId);
            Changed?.Invoke();
            return true;
        }

        public void Unequip(EquipSlot slot)
        {
            string itemId = EquippedIn(slot);
            if (string.IsNullOrEmpty(itemId))
                return;
            equipment.RemoveAll(entry => entry.slot == slot);
            Add(itemId);
        }

        public int TotalDamage() => Sum(item => item.damage);
        public int TotalArmor() => Sum(item => item.armor);
        public int TotalHealthBonus() => Sum(item => item.health);
        public float TotalMoveSpeedBonus() => Sum(item => item.moveSpeed);

        int Sum(Func<ItemDefinition, int> selector)
        {
            int total = 0;
            foreach (Equipped entry in equipment)
            {
                ItemDefinition definition = ItemDatabase.Get(entry.itemId);
                if (definition != null)
                    total += selector(definition);
            }
            return total;
        }

        float Sum(Func<ItemDefinition, float> selector)
        {
            float total = 0f;
            foreach (Equipped entry in equipment)
            {
                ItemDefinition definition = ItemDatabase.Get(entry.itemId);
                if (definition != null)
                    total += selector(definition);
            }
            return total;
        }
    }
}
