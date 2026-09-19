using System;
using System.Collections.Generic;
using System.Linq;
using Oathfire.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// A merchant's stall: BUY from what they stock, SELL what the Warden carries. Stock comes from
    /// Resources/Shops/shops.json and is deliberately plain — common and a few fine pieces at a mark-up — so the
    /// good gear still has to be earned. Selling pays a fraction of an item's worth; quest things and what is
    /// being worn are never offered for sale.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        [Serializable] class StockEntry { public string item; public int price; }
        [Serializable] class Shop { public string id; public string nameKey; public float sellRate = 0.35f; public StockEntry[] stock; }
        [Serializable] class ShopTable { public Shop[] shops; }

        [SerializeField] Sprite panelSprite;
        [Tooltip("Item icons, the same set the pack uses.")]
        [SerializeField] Sprite[] icons;

        public static ShopPanel Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.root && Instance.root.activeSelf;

        readonly Dictionary<string, Sprite> iconsByName = new Dictionary<string, Sprite>();
        readonly List<GameObject> rows = new List<GameObject>();
        Shop[] shops = Array.Empty<Shop>();
        Shop current;
        bool selling;

        GameObject root;
        RectTransform list;
        TMP_Text title;
        TMP_Text coinLabel;
        (Image back, TMP_Text label)[] tabs;

        public int RowCount => rows.Count;
        public bool Selling => selling;

        void Awake()
        {
            Instance = this;
            Core.GameServices.EnsureCreated();
            var asset = Resources.Load<TextAsset>("Shops/shops");
            if (asset)
                shops = JsonUtility.FromJson<ShopTable>(asset.text).shops ?? shops;
            else
                Debug.LogError("[Oathfire] Missing Resources/Shops/shops.json; merchants have nothing to sell");
            Build();
            root.SetActive(false);
            Dialogue.DialogueRunner.ActionRequested += OnAction;
        }

        void OnDestroy()
        {
            Dialogue.DialogueRunner.ActionRequested -= OnAction;
            if (Instance == this)
                Instance = null;
        }

        void OnAction(string action)
        {
            if (action != null && action.StartsWith("shop:"))
                Open(action.Substring(5));
        }

        public void Open(string shopId)
        {
            current = shops.FirstOrDefault(shop => shop.id == shopId);
            if (current == null)
            {
                Debug.LogError($"[Oathfire] No shop called {shopId}");
                return;
            }
            title.text = Core.GameServices.Localization.Get(current.nameKey);
            root.SetActive(true);
            Controls.MobileControls.GameplayActive = false;
            Core.GameServices.Audio.PlaySfx("ui_open", 0.8f, 0f);
            ShowPage(sell: false);
        }

        public void Close()
        {
            root.SetActive(false);
            Controls.MobileControls.GameplayActive = true;
            Core.GameServices.Audio.PlaySfx("ui_close", 0.7f, 0f);
        }

        public void ShowPage(bool sell)
        {
            selling = sell;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = (i == 1) == sell;
                tabs[i].back.color = active ? new Color(0.3f, 0.22f, 0.1f, 1f) : new Color(0.12f, 0.12f, 0.1f, 1f);
                tabs[i].label.color = active ? UiTheme.Bone : UiTheme.BoneDim;
            }
            Refresh();
        }

        /// <summary>What a stack of this item fetches when sold here.</summary>
        public int SellPrice(ItemDefinition item) =>
            current == null || item == null ? 0 : Mathf.Max(1, Mathf.FloorToInt(item.value * current.sellRate));

        void Refresh()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();
            Progress.PlayerState state = Progress.PlayerState.Instance;
            coinLabel.text = Core.GameServices.Localization.Format("shop.coin", state?.Inventory.coin ?? 0);
            if (state == null || current == null)
                return;

            if (!selling)
            {
                foreach (StockEntry entry in current.stock)
                {
                    ItemDefinition item = ItemDatabase.Get(entry.item);
                    if (item == null)
                        continue;
                    int price = entry.price > 0 ? entry.price : Mathf.CeilToInt(item.value * 1.25f);
                    bool affordable = state.Inventory.coin >= price;
                    AddRow(item, Core.GameServices.Localization.Format("shop.price", price), affordable, () => Buy(item, price));
                }
                return;
            }

            foreach (Inventory.Stack stack in state.Inventory.stacks.ToList())
            {
                ItemDefinition item = ItemDatabase.Get(stack.itemId);
                if (item == null || item.kind == ItemKind.Quest)
                    continue;
                int price = SellPrice(item);
                string label = Core.GameServices.Localization.Format("shop.sellFor", price) + (stack.count > 1 ? $"   x{stack.count}" : string.Empty);
                AddRow(item, label, true, () => Sell(item, price));
            }
            if (rows.Count == 0)
                AddNote("shop.nothingToSell");
        }

        public bool Buy(ItemDefinition item, int price)
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null || state.Inventory.coin < price)
            {
                Core.GameServices.Audio.PlaySfx("denied", 0.7f, 0f);
                Toast.Show("shop.cannotAfford");
                return false;
            }
            state.Inventory.coin -= price;
            state.Inventory.Add(item.id);
            Core.GameServices.Audio.PlaySfx("coin", 0.9f, 0f);
            Toast.ShowText(Core.GameServices.Localization.Format("shop.bought", Core.GameServices.Localization.Get(item.NameKey)));
            Quests.QuestRuntime.Evaluate();
            Refresh();
            return true;
        }

        public bool Sell(ItemDefinition item, int price)
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null || !state.Inventory.Remove(item.id))
                return false;
            state.Inventory.coin += price;
            Core.GameServices.Audio.PlaySfx("coin", 0.9f, 0f);
            Refresh();
            return true;
        }

        /// <summary>Buys the first stocked item by id; used by the smoke test.</summary>
        public bool BuyById(string itemId)
        {
            StockEntry entry = current?.stock.FirstOrDefault(stock => stock.item == itemId);
            ItemDefinition item = ItemDatabase.Get(itemId);
            if (entry == null || item == null)
                return false;
            return Buy(item, entry.price > 0 ? entry.price : Mathf.CeilToInt(item.value * 1.25f));
        }

        public bool Stocks(Func<ItemDefinition, bool> test) =>
            current != null && current.stock.Any(entry => { ItemDefinition item = ItemDatabase.Get(entry.item); return item != null && test(item); });

        void AddRow(ItemDefinition item, string priceText, bool available, Action onTap)
        {
            var row = new GameObject(item.id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(list, false);
            row.GetComponent<LayoutElement>().preferredHeight = 124f;
            var back = row.GetComponent<Image>();
            back.color = available ? new Color(0.11f, 0.105f, 0.085f, 0.96f) : new Color(0.07f, 0.07f, 0.06f, 0.9f);
            row.GetComponent<Button>().onClick.AddListener(() => onTap());
            rows.Add(row);

            var well = Rect("Well", row.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            well.pivot = new Vector2(0f, 0.5f);
            well.sizeDelta = new Vector2(100f, 100f);
            well.anchoredPosition = new Vector2(14f, 0f);
            var wellImage = well.gameObject.AddComponent<Image>();
            wellImage.color = new Color(0.04f, 0.045f, 0.04f, 1f);
            wellImage.raycastTarget = false;
            Sprite sprite = IconFor(item);
            if (sprite)
            {
                var icon = Rect("Icon", well, Vector2.zero, Vector2.one);
                icon.offsetMin = new Vector2(10f, 10f);
                icon.offsetMax = new Vector2(-10f, -10f);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = sprite;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            var name = Label(row.transform, 34, available ? UiTheme.Bone : UiTheme.BoneDim, TextAlignmentOptions.BottomLeft);
            name.rectTransform.offsetMin = new Vector2(132f, 60f);
            name.rectTransform.offsetMax = new Vector2(-230f, -10f);
            name.text = Core.GameServices.Localization.Get(item.NameKey);

            var detail = Label(row.transform, 24, UiTheme.BoneDim, TextAlignmentOptions.TopLeft);
            detail.rectTransform.offsetMin = new Vector2(132f, 10f);
            detail.rectTransform.offsetMax = new Vector2(-230f, -66f);
            detail.text = Stats(item);

            var price = Label(row.transform, 30, available ? UiTheme.EmberBright : UiTheme.Rust, TextAlignmentOptions.MidlineRight);
            price.rectTransform.offsetMin = new Vector2(0f, 0f);
            price.rectTransform.offsetMax = new Vector2(-22f, 0f);
            price.rectTransform.anchorMin = new Vector2(0.62f, 0f);
            price.text = priceText;
        }

        static string Stats(ItemDefinition item)
        {
            var parts = new List<string> { Core.GameServices.Localization.Get($"rarity.{item.rarity}") };
            if (item.damage != 0) parts.Add($"{Core.GameServices.Localization.Get("stat.damage")} +{item.damage}");
            if (item.armor != 0) parts.Add($"{Core.GameServices.Localization.Get("stat.armour")} +{item.armor}");
            if (item.health != 0) parts.Add($"{Core.GameServices.Localization.Get("stat.health")} +{item.health}");
            if (item.healAmount != 0) parts.Add($"{Core.GameServices.Localization.Get("stat.heals")} {item.healAmount}");
            return string.Join("  ·  ", parts);
        }

        void AddNote(string key)
        {
            var note = Label(list, 28, UiTheme.BoneDim, TextAlignmentOptions.Center);
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            note.text = Core.GameServices.Localization.Get(key);
            rows.Add(note.gameObject);
        }

        Sprite IconFor(ItemDefinition item)
        {
            if (iconsByName.Count == 0 && icons != null)
                foreach (Sprite sprite in icons)
                    if (sprite)
                        iconsByName[sprite.name] = sprite;
            return !string.IsNullOrEmpty(item.icon) && iconsByName.TryGetValue(item.icon, out Sprite found) ? found : null;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            Stretch(rootRect);
            root.GetComponent<Image>().color = new Color(0.01f, 0.012f, 0.01f, 0.94f);

            var sheet = Rect("Sheet", root.transform, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
            var sheetImage = sheet.gameObject.AddComponent<Image>();
            sheetImage.sprite = panelSprite;
            sheetImage.type = Image.Type.Sliced;
            sheetImage.color = UiTheme.InkPanel;

            title = Label(sheet, 52, UiTheme.Ember, TextAlignmentOptions.Center);
            title.AsHeading(8f);
            Band(title.rectTransform, 0.93f, 0.99f);

            coinLabel = Label(sheet, 32, UiTheme.EmberBright, TextAlignmentOptions.Center);
            coinLabel.AsHeading(3f);
            Band(coinLabel.rectTransform, 0.885f, 0.93f);

            tabs = new[]
            {
                Tab(sheet, "shop.buy", new Vector2(0.06f, 0.82f), new Vector2(0.495f, 0.875f), () => ShowPage(sell: false)),
                Tab(sheet, "shop.sell", new Vector2(0.505f, 0.82f), new Vector2(0.94f, 0.875f), () => ShowPage(sell: true)),
            };

            list = Scroll(Rect("Items", sheet, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.81f)));

            var close = Rect("Close", sheet, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.08f));
            close.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f);
            close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            TMP_Text closeLabel = Label(close, 34, UiTheme.Bone, TextAlignmentOptions.Center);
            closeLabel.AsHeading(3f);
            closeLabel.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "shop.leave";
        }

        (Image, TMP_Text) Tab(RectTransform parent, string key, Vector2 min, Vector2 max, Action open)
        {
            var rect = Rect(key, parent, min, max);
            var back = rect.gameObject.AddComponent<Image>();
            back.color = new Color(0.12f, 0.12f, 0.1f, 1f);
            rect.gameObject.AddComponent<Button>().onClick.AddListener(() => open());
            TMP_Text label = Label(rect, 32, UiTheme.Bone, TextAlignmentOptions.Center);
            label.AsHeading(5f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
            return (back, label);
        }

        static RectTransform Scroll(RectTransform holder)
        {
            holder.gameObject.AddComponent<RectMask2D>();
            var scroll = holder.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(holder, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hit = holder.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            scroll.content = content;
            scroll.horizontal = false;
            return content;
        }

        static TMP_Text Label(Transform parent, float size, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.AsBody();
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform);
            return text;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Band(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(0.04f, bottom);
            rect.anchorMax = new Vector2(0.96f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
