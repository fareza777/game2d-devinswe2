using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Items
{
    /// <summary>Loads and serves the item table. One lookup for the whole game.</summary>
    public static class ItemDatabase
    {
        static readonly Dictionary<string, ItemDefinition> byId = new Dictionary<string, ItemDefinition>();
        static bool loaded;

        public static IReadOnlyDictionary<string, ItemDefinition> All
        {
            get
            {
                EnsureLoaded();
                return byId;
            }
        }

        public static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;

            var asset = Resources.Load<TextAsset>("Items/items");
            if (!asset)
            {
                Debug.LogError("[Items] Missing Resources/Items/items.json");
                return;
            }

            var table = JsonUtility.FromJson<ItemTable>(asset.text);
            foreach (ItemDefinition item in table.items)
                byId[item.id] = item;
        }

        public static ItemDefinition Get(string id)
        {
            EnsureLoaded();
            if (byId.TryGetValue(id, out ItemDefinition item))
                return item;
            Debug.LogError($"[Items] Unknown item id '{id}'");
            return null;
        }

        public static Sprite Icon(ItemDefinition item)
        {
            if (item == null || string.IsNullOrEmpty(item.icon))
                return null;
            return Resources.Load<Sprite>($"Icons/{item.icon}");
        }
    }
}
