using System.Collections.Generic;
using System.Linq;
using Oathfire.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The pack, in two pages.
    ///
    /// EQUIPMENT is a codex anatomy plate of the Warden with a slot for each place he can wear something, each
    /// tied by a thin line to that part of him. Tap a worn item to read it and take it off; tap an empty slot
    /// to choose from what in the bag fits there.
    ///
    /// BAG lists everything carried. Tapping a row opens a card with the full description and numbers, and only
    /// the card's button equips, drinks or unequips, so a stray thumb never drinks the last flask unread.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.06f, 0.07f, 0.06f, 0.97f);
        static readonly Color Bone = new Color(0.87f, 0.84f, 0.76f);
        static readonly Color Ember = new Color(0.79f, 0.58f, 0.23f);
        static readonly Color Faint = new Color(0.6f, 0.62f, 0.55f);
        static readonly Dictionary<ItemRarity, Color> RarityColors = new()
        {
            { ItemRarity.Common, new Color(0.74f, 0.72f, 0.66f) },
            { ItemRarity.Fine, new Color(0.55f, 0.78f, 0.62f) },
            { ItemRarity.Rare, new Color(0.45f, 0.66f, 0.92f) },
            { ItemRarity.Oathbound, new Color(0.92f, 0.62f, 0.25f) },
        };

        /// <summary>
        /// Where each slot sits on the page, and the point on the figure its line runs to. Page positions are
        /// fractions of the equipment page; figure points are fractions of the anatomy plate (x right, y up).
        /// </summary>
        static readonly (EquipSlot slot, string labelKey, Vector2 page, Vector2 onFigure)[] SlotLayout =
        {
            (EquipSlot.Head, "hero.slot.head", new Vector2(0.13f, 0.9f), new Vector2(0.5f, 0.89f)),
            (EquipSlot.Body, "hero.slot.body", new Vector2(0.13f, 0.695f), new Vector2(0.44f, 0.7f)),
            (EquipSlot.Hands, "hero.slot.hands", new Vector2(0.13f, 0.49f), new Vector2(0.17f, 0.58f)),
            (EquipSlot.MainHand, "hero.slot.mainHand", new Vector2(0.13f, 0.285f), new Vector2(0.2f, 0.49f)),
            (EquipSlot.Legs, "hero.slot.legs", new Vector2(0.13f, 0.08f), new Vector2(0.4f, 0.3f)),
            (EquipSlot.Neck, "hero.slot.neck", new Vector2(0.87f, 0.9f), new Vector2(0.49f, 0.8f)),
            (EquipSlot.Trinket, "hero.slot.trinket", new Vector2(0.87f, 0.695f), new Vector2(0.56f, 0.6f)),
            (EquipSlot.OffHand, "hero.slot.offHand", new Vector2(0.87f, 0.49f), new Vector2(0.85f, 0.57f)),
            (EquipSlot.Ring, "hero.slot.ring", new Vector2(0.87f, 0.285f), new Vector2(0.8f, 0.48f)),
            (EquipSlot.Feet, "hero.slot.feet", new Vector2(0.87f, 0.08f), new Vector2(0.66f, 0.06f)),
        };

        const float SlotSize = 118f;

        /// <summary>The bag's categories, in the order the chips run; each is the item kinds it shows.</summary>
        static readonly (string labelKey, ItemKind[] kinds)[] Categories =
        {
            ("inv.cat.all", null),
            ("inv.cat.weapons", new[] { ItemKind.Weapon }),
            ("inv.cat.armour", new[] { ItemKind.Armor }),
            ("inv.cat.trinkets", new[] { ItemKind.Trinket, ItemKind.Relic }),
            ("inv.cat.supplies", new[] { ItemKind.Consumable }),
            ("inv.cat.materials", new[] { ItemKind.Material }),
            ("inv.cat.quest", new[] { ItemKind.Quest }),
        };

        int category;
        (Image back, TMP_Text label)[] categoryChips;
        TMP_Text emptyNote;

        [SerializeField] Sprite panelSprite;
        [SerializeField] Sprite anatomySprite;
        [Tooltip("Every sprite from the pack's LootIcons sheet; items name theirs by sprite name.")]
        [SerializeField] Sprite[] icons;

        class SlotView
        {
            public EquipSlot slot;
            public RectTransform rect;
            public Image icon;
            public Outline edge;
            public TMP_Text itemName;
            public RectTransform line;
            public Vector2 onFigure;
        }

        readonly Dictionary<string, Sprite> iconsByName = new Dictionary<string, Sprite>();
        readonly List<SlotView> slotViews = new List<SlotView>();
        readonly List<GameObject> rows = new List<GameObject>();
        readonly List<GameObject> pickerRows = new List<GameObject>();

        GameObject root;
        RectTransform sheetRect;
        GameObject equipmentPage;
        GameObject bagPage;
        RectTransform figure;
        TMP_Text equipmentStats;
        TMP_Text statsLabel;
        RectTransform listHolder;
        (Image back, TMP_Text label)[] tabs;

        GameObject card;
        Image cardIcon;
        TMP_Text cardName;
        TMP_Text cardKind;
        TMP_Text cardBody;
        TMP_Text cardAction;
        Button cardActionButton;

        GameObject picker;
        TMP_Text pickerTitle;
        RectTransform pickerList;

        public int RowCount => rows.Count;

        public float FirstRowHeight => rows.Count > 0 && rows[0] ? ((RectTransform)rows[0].transform).rect.height : 0f;

        public bool ShowingEquipment => equipmentPage && equipmentPage.activeSelf;

        public int SlotCount => slotViews.Count;

        /// <summary>Shows one category of the bag (0 is everything).</summary>
        public void ShowCategory(int index)
        {
            category = Mathf.Clamp(index, 0, Categories.Length - 1);
            Refresh();
        }

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        public void Toggle()
        {
            bool show = !root.activeSelf;
            root.SetActive(show);
            Controls.MobileControls.GameplayActive = !show;
            if (!show)
            {
                card.SetActive(false);
                picker.SetActive(false);
                Core.GameServices.Ads?.NoteMenuClosed();
            }

            // Same as the Warden's book: the HUD's outlined text ghosts through the sheet, so it steps aside.
            GameplayHud hud = FindAnyObjectByType<GameplayHud>();
            if (hud && hud.TryGetComponent(out Canvas hudCanvas))
                hudCanvas.enabled = !show;
            if (show)
                ShowPage(equipment: true);
        }

        /// <summary>Switches between the anatomy plate and the bag list.</summary>
        public void ShowPage(bool equipment)
        {
            equipmentPage.SetActive(equipment);
            equipmentStats.gameObject.SetActive(equipment);
            bagPage.SetActive(!equipment);
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = (i == 0) == equipment;
                tabs[i].back.color = active ? new Color(0.3f, 0.22f, 0.1f, 1f) : new Color(0.12f, 0.12f, 0.1f, 1f);
                tabs[i].label.color = active ? Bone : Faint;
            }
            Refresh();
        }

        void Refresh()
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null)
                return;

            statsLabel.text = Core.GameServices.Localization.Format("inv.stats", state.Level, state.MaxHealth, state.Damage, state.Armor, state.Inventory.coin);
            equipmentStats.text = Core.GameServices.Localization.Format("inv.dollStats", state.Damage, state.Armor, state.MaxHealth);

            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();
            ItemKind[] kinds = Categories[category].kinds;
            bool Shown(ItemDefinition item) => item != null && (kinds == null || System.Array.IndexOf(kinds, item.kind) >= 0);
            foreach (Inventory.Equipped equipped in state.Inventory.equipment)
                if (Shown(ItemDatabase.Get(equipped.itemId)))
                    AddRow(ItemDatabase.Get(equipped.itemId), 1, equipped: true);
            var carried = state.Inventory.stacks
                .Select(stack => (item: ItemDatabase.Get(stack.itemId), stack.count))
                .Where(entry => Shown(entry.item))
                .OrderBy(entry => (int)entry.item.kind == (int)ItemKind.Quest ? 99 : (int)entry.item.kind)
                .ThenByDescending(entry => entry.item.rarity)
                .ThenBy(entry => entry.item.id);
            foreach (var (item, count) in carried)
                AddRow(item, count, equipped: false);
            if (emptyNote)
                emptyNote.gameObject.SetActive(rows.Count == 0);
            for (int i = 0; categoryChips != null && i < categoryChips.Length; i++)
            {
                bool active = i == category;
                categoryChips[i].back.color = active ? new Color(0.3f, 0.22f, 0.1f, 1f) : new Color(0.1f, 0.1f, 0.085f, 1f);
                categoryChips[i].label.color = active ? Bone : Faint;
            }

            foreach (SlotView view in slotViews)
            {
                string itemId = state.Inventory.EquippedIn(view.slot);
                ItemDefinition item = string.IsNullOrEmpty(itemId) ? null : ItemDatabase.Get(itemId);
                view.icon.sprite = item != null ? IconFor(item) : null;
                view.icon.enabled = view.icon.sprite;
                view.itemName.text = item != null
                    ? Core.GameServices.Localization.Get(item.NameKey)
                    : Core.GameServices.Localization.Get("hero.slot.empty");
                view.itemName.color = item != null ? RarityColors[item.rarity] : Faint;
                Color rarity = item != null ? RarityColors[item.rarity] : new Color(0.36f, 0.33f, 0.26f);
                view.edge.effectColor = new Color(rarity.r, rarity.g, rarity.b, 0.9f);
            }
            Canvas.ForceUpdateCanvases();
            DrawLines();
        }

        /// <summary>Runs a thin inked line from each slot to its place on the figure.</summary>
        void DrawLines()
        {
            Rect plate = figure.rect;
            foreach (SlotView view in slotViews)
            {
                Vector2 target = (Vector2)figure.localPosition
                                 + new Vector2(plate.xMin + plate.width * view.onFigure.x, plate.yMin + plate.height * view.onFigure.y);
                Vector2 from = view.rect.localPosition;
                Vector2 delta = target - from;
                float length = Mathf.Max(0f, delta.magnitude - SlotSize * 0.5f);
                view.line.localPosition = from + delta.normalized * (SlotSize * 0.5f);
                view.line.sizeDelta = new Vector2(length, 3f);
                view.line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        void TapSlot(SlotView view)
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null)
                return;
            string itemId = state.Inventory.EquippedIn(view.slot);
            if (!string.IsNullOrEmpty(itemId))
            {
                OpenCard(ItemDatabase.Get(itemId), 1, true);
                return;
            }
            OpenPicker(view);
        }

        /// <summary>Everything in the bag that can be worn in this slot, one tap to put it on.</summary>
        void OpenPicker(SlotView view)
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            var fits = state.Inventory.stacks
                .Select(stack => ItemDatabase.Get(stack.itemId))
                .Where(item => item != null && item.IsEquipment && item.slot == view.slot)
                .ToList();
            if (fits.Count == 0)
            {
                Toast.Show("inv.pick.none");
                return;
            }

            foreach (GameObject row in pickerRows)
                Destroy(row);
            pickerRows.Clear();
            string slotName = Core.GameServices.Localization.Get(SlotLayout.First(entry => entry.slot == view.slot).labelKey);
            pickerTitle.text = Core.GameServices.Localization.Format("inv.pick.title", slotName);
            foreach (ItemDefinition item in fits)
            {
                GameObject row = BuildItemRow(pickerList, item, 1, equipped: false);
                row.GetComponent<Button>().onClick.AddListener(() =>
                {
                    picker.SetActive(false);
                    Activate(item, equipped: false);
                });
                pickerRows.Add(row);
            }
            picker.SetActive(true);
        }

        void AddRow(ItemDefinition item, int count, bool equipped)
        {
            if (item == null)
                return;
            GameObject row = BuildItemRow(listHolder, item, count, equipped);
            row.GetComponent<Button>().onClick.AddListener(() => OpenCard(item, count, equipped));
            rows.Add(row);
        }

        GameObject BuildItemRow(RectTransform parent, ItemDefinition item, int count, bool equipped)
        {
            var row = new GameObject(item.id, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = equipped ? new Color(0.17f, 0.15f, 0.1f, 0.96f) : new Color(0.1f, 0.11f, 0.09f, 0.9f);
            row.AddComponent<LayoutElement>().preferredHeight = 124f;

            // The icon sits in a dark well on the left, edged in the item's rarity colour.
            var well = new GameObject("Well", typeof(RectTransform), typeof(Image));
            well.transform.SetParent(row.transform, false);
            var wellRect = (RectTransform)well.transform;
            wellRect.anchorMin = new Vector2(0f, 0.5f);
            wellRect.anchorMax = new Vector2(0f, 0.5f);
            wellRect.pivot = new Vector2(0f, 0.5f);
            wellRect.sizeDelta = new Vector2(100f, 100f);
            wellRect.anchoredPosition = new Vector2(14f, 0f);
            well.GetComponent<Image>().color = new Color(0.04f, 0.045f, 0.04f, 1f);
            well.GetComponent<Image>().raycastTarget = false;
            var edge = well.AddComponent<Outline>();
            Color rarity = RarityColors[item.rarity];
            edge.effectColor = new Color(rarity.r, rarity.g, rarity.b, 0.8f);
            edge.effectDistance = new Vector2(2f, -2f);

            Sprite sprite = IconFor(item);
            if (sprite)
            {
                Image iconImage = NewImage("Icon", well.transform, sprite, Color.white);
                iconImage.preserveAspect = true;
                iconImage.rectTransform.offsetMin = new Vector2(10f, 10f);
                iconImage.rectTransform.offsetMax = new Vector2(-10f, -10f);
            }

            TMP_Text title = NewText("Title", row.transform, 36, RarityColors[item.rarity]);
            title.AsBody();
            title.alignment = TextAlignmentOptions.BottomLeft;
            title.rectTransform.offsetMin = new Vector2(132f, 62f);
            title.rectTransform.offsetMax = new Vector2(-24f, -10f);
            string suffix = equipped ? Core.GameServices.Localization.Get("inv.equipped") : count > 1 ? $"  x{count}" : string.Empty;
            title.text = Core.GameServices.Localization.Get(item.NameKey) + suffix;

            TMP_Text kind = NewText("Kind", row.transform, 26, Faint);
            kind.AsBody();
            kind.alignment = TextAlignmentOptions.TopLeft;
            kind.rectTransform.offsetMin = new Vector2(132f, 12f);
            kind.rectTransform.offsetMax = new Vector2(-24f, -66f);
            kind.text = KindLine(item);
            return row;
        }

        Sprite IconFor(ItemDefinition item)
        {
            if (iconsByName.Count == 0 && icons != null)
                foreach (Sprite sprite in icons)
                    if (sprite)
                        iconsByName[sprite.name] = sprite;
            return !string.IsNullOrEmpty(item.icon) && iconsByName.TryGetValue(item.icon, out Sprite found) ? found : null;
        }

        static string KindLine(ItemDefinition item) =>
            Core.GameServices.Localization.Get($"rarity.{item.rarity}") + "  -  " +
            Core.GameServices.Localization.Get($"kind.{item.kind}");

        /// <summary>Everything worth knowing before committing to an item, and one button to act on it.</summary>
        void OpenCard(ItemDefinition item, int count, bool equipped)
        {
            if (item == null)
                return;
            card.SetActive(true);
            cardIcon.sprite = IconFor(item);
            cardIcon.enabled = cardIcon.sprite;
            cardName.text = Core.GameServices.Localization.Get(item.NameKey);
            cardName.color = RarityColors[item.rarity];
            cardKind.text = KindLine(item) + (count > 1 ? $"  -  x{count}" : string.Empty);

            var body = new System.Text.StringBuilder(Core.GameServices.Localization.Get(item.DescriptionKey));
            body.Append('\n');
            AppendStat(body, "stat.damage", item.damage);
            AppendStat(body, "stat.armour", item.armor);
            AppendStat(body, "stat.health", item.health);
            AppendStat(body, "stat.heals", item.healAmount);
            AppendStat(body, "stat.mana", item.manaAmount);
            AppendStat(body, "stat.value", item.value, signed: false);
            cardBody.text = body.ToString();

            string actionKey = equipped ? "inv.unequip"
                : item.IsEquipment ? "inv.equip"
                : item.kind == ItemKind.Consumable ? "inv.use"
                : string.Empty;
            cardActionButton.gameObject.SetActive(!string.IsNullOrEmpty(actionKey));
            cardActionButton.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(actionKey))
            {
                cardAction.text = Core.GameServices.Localization.Get(actionKey);
                cardActionButton.onClick.AddListener(() =>
                {
                    card.SetActive(false);
                    Activate(item, equipped);
                });
            }
        }

        static void AppendStat(System.Text.StringBuilder into, string labelKey, int amount, bool signed = true)
        {
            if (amount == 0)
                return;
            string label = Core.GameServices.Localization.Get(labelKey);
            into.Append('\n').Append("<color=#a8a293>").Append(label).Append("</color><pos=48%><color=#ded6c4>")
                .Append(signed && amount > 0 ? "+" : string.Empty).Append(amount).Append("</color>");
        }

        void Activate(ItemDefinition item, bool equipped)
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null)
                return;

            if (equipped)
            {
                state.Inventory.Unequip(item.slot);
                Core.GameServices.Audio.PlaySfx("equip", 0.8f);
            }
            else if (item.IsEquipment)
            {
                state.Inventory.Equip(item.id);
                Core.GameServices.Audio.PlaySfx("equip");
                Toast.Show("toast.equipped");
            }
            else if (item.kind == ItemKind.Consumable)
            {
                var health = SmallScale.FantasyKingdomTileset.PlayerHealth.Instance;
                if (item.healAmount > 0 && health)
                    health.currentHealth = Mathf.Min(health.maxHealth, health.currentHealth + item.healAmount);
                if (item.manaAmount > 0 && SmallScale.FantasyKingdomTileset.PlayerMana.Instance)
                    SmallScale.FantasyKingdomTileset.PlayerMana.Instance.Grant(item.manaAmount);
                state.Inventory.Remove(item.id);
                Core.GameServices.Audio.PlaySfx("potion");
                Toast.Show("toast.used");
            }
            Refresh();
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
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0.01f, 0.012f, 0.01f, 0.94f);

            Image sheet = NewImage("Sheet", root.transform, panelSprite, Ink);
            sheet.type = Image.Type.Sliced;
            sheet.raycastTarget = true;
            sheetRect = sheet.rectTransform;
            sheetRect.anchorMin = new Vector2(0.03f, 0.04f);
            sheetRect.anchorMax = new Vector2(0.97f, 0.96f);
            sheetRect.offsetMin = sheetRect.offsetMax = Vector2.zero;

            TMP_Text heading = NewText("Heading", sheetRect, 52, Ember);
            Band(heading.rectTransform, 0.93f, 0.99f);
            heading.AsHeading(10f);
            heading.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "inv.title";

            tabs = new[]
            {
                BuildTab("inv.tab.equipment", new Vector2(0.06f, 0.865f), new Vector2(0.495f, 0.925f), () => ShowPage(equipment: true)),
                BuildTab("inv.tab.bag", new Vector2(0.505f, 0.865f), new Vector2(0.94f, 0.925f), () => ShowPage(equipment: false)),
            };

            BuildEquipmentPage();
            BuildBagPage();

            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(sheetRect, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.3f, 0.015f);
            closeRect.anchorMax = new Vector2(0.7f, 0.07f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            close.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f);
            close.GetComponent<Button>().onClick.AddListener(Toggle);
            TMP_Text closeLabel = NewText("Label", close.transform, 34, Bone);
            closeLabel.AsHeading(3f);
            closeLabel.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "inv.close";

            BuildPicker(sheetRect);
            BuildCard(sheetRect);
            root.SetActive(false);
        }

        (Image, TMP_Text) BuildTab(string key, Vector2 min, Vector2 max, System.Action open)
        {
            Image back = NewImage(key, sheetRect, null, new Color(0.12f, 0.12f, 0.1f, 1f));
            back.raycastTarget = true;
            back.rectTransform.anchorMin = min;
            back.rectTransform.anchorMax = max;
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => open());
            TMP_Text label = NewText("Label", back.rectTransform, 32, Bone);
            label.AsHeading(5f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
            return (back, label);
        }

        void BuildEquipmentPage()
        {
            var page = new GameObject("Equipment", typeof(RectTransform));
            page.transform.SetParent(sheetRect, false);
            var pageRect = (RectTransform)page.transform;
            pageRect.anchorMin = new Vector2(0.03f, 0.14f);
            pageRect.anchorMax = new Vector2(0.97f, 0.85f);
            pageRect.offsetMin = pageRect.offsetMax = Vector2.zero;
            equipmentPage = page;

            // The plate keeps its own proportions inside the middle of the page; lines are measured against it.
            var holder = new GameObject("Figure", typeof(RectTransform), typeof(AspectRatioFitter));
            holder.transform.SetParent(pageRect, false);
            figure = (RectTransform)holder.transform;
            figure.anchorMin = new Vector2(0.27f, 0f);
            figure.anchorMax = new Vector2(0.73f, 1f);
            figure.offsetMin = figure.offsetMax = Vector2.zero;
            var fitter = holder.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = anatomySprite ? anatomySprite.rect.width / anatomySprite.rect.height : 0.5625f;
            Image plate = NewImage("Plate", figure, anatomySprite, Color.white);
            plate.preserveAspect = true;
            var plateEdge = plate.gameObject.AddComponent<Outline>();
            plateEdge.effectColor = new Color(0.36f, 0.33f, 0.26f, 0.8f);
            plateEdge.effectDistance = new Vector2(3f, -3f);

            foreach ((EquipSlot slot, string labelKey, Vector2 place, Vector2 onFigure) in SlotLayout)
                slotViews.Add(BuildSlot(pageRect, slot, labelKey, place, onFigure));

            equipmentStats = NewText("Totals", sheetRect, 32, Bone);
            equipmentStats.AsHeading(2f);
            Band(equipmentStats.rectTransform, 0.08f, 0.135f);
        }

        SlotView BuildSlot(RectTransform page, EquipSlot slot, string labelKey, Vector2 place, Vector2 onFigure)
        {
            Image line = NewImage($"Line {slot}", page, null, new Color(0.62f, 0.55f, 0.4f, 0.75f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.rectTransform.pivot = new Vector2(0f, 0.5f);

            Image well = NewImage($"Slot {slot}", page, null, new Color(0.035f, 0.04f, 0.035f, 1f));
            well.raycastTarget = true;
            RectTransform rect = well.rectTransform;
            rect.anchorMin = rect.anchorMax = place;
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);
            var edge = well.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(4f, -4f);

            Image icon = NewImage("Icon", rect, null, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.offsetMin = new Vector2(18f, 18f);
            icon.rectTransform.offsetMax = new Vector2(-18f, -18f);

            TMP_Text label = NewText("Label", rect, 22, Ember);
            label.AsHeading(3f);
            label.rectTransform.anchorMin = new Vector2(-0.3f, 1f);
            label.rectTransform.anchorMax = new Vector2(1.3f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = labelKey;

            // With ten slots there is no room for a name under each one without it running into the label of the
            // slot below; the icon and its rarity edge say what is worn, and a tap opens the full card.
            TMP_Text itemName = NewText("Item", rect, 24, Faint);
            itemName.gameObject.SetActive(false);

            var view = new SlotView { slot = slot, rect = rect, icon = icon, edge = edge, itemName = itemName, line = line.rectTransform, onFigure = onFigure };
            well.gameObject.AddComponent<Button>().onClick.AddListener(() => TapSlot(view));
            return view;
        }

        void BuildBagPage()
        {
            var page = new GameObject("Bag", typeof(RectTransform));
            page.transform.SetParent(sheetRect, false);
            var pageRect = (RectTransform)page.transform;
            pageRect.anchorMin = new Vector2(0f, 0.08f);
            pageRect.anchorMax = new Vector2(1f, 0.86f);
            pageRect.offsetMin = pageRect.offsetMax = Vector2.zero;
            bagPage = page;

            statsLabel = NewText("Stats", pageRect, 30, Bone);
            Band(statsLabel.rectTransform, 0.93f, 1f);

            // Two rows of category chips: seven in one row would be too small for a thumb.
            categoryChips = new (Image, TMP_Text)[Categories.Length];
            for (int i = 0; i < Categories.Length; i++)
            {
                int index = i;
                int rowIndex = i < 4 ? 0 : 1;
                int column = rowIndex == 0 ? i : i - 4;
                int perRow = rowIndex == 0 ? 4 : 3;
                float width = 0.94f / perRow;
                float left = 0.03f + column * width;
                float top = rowIndex == 0 ? 0.925f : 0.87f;
                Image chip = NewImage($"Chip {i}", pageRect, null, new Color(0.1f, 0.1f, 0.085f, 1f));
                chip.raycastTarget = true;
                chip.rectTransform.anchorMin = new Vector2(left + 0.005f, top - 0.05f);
                chip.rectTransform.anchorMax = new Vector2(left + width - 0.005f, top);
                chip.rectTransform.offsetMin = chip.rectTransform.offsetMax = Vector2.zero;
                chip.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    category = index;
                    Core.GameServices.Audio.PlaySfx("ui_tap", 0.7f, 0f);
                    Refresh();
                });
                TMP_Text label = NewText("Label", chip.rectTransform, 26, Faint);
                label.AsHeading(3f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 26f;
                label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = Categories[i].labelKey;
                categoryChips[i] = (chip, label);
            }
            statsLabel.rectTransform.anchorMin = new Vector2(0.04f, 0.935f);

            listHolder = BuildScroll(pageRect, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.81f));
            emptyNote = NewText("Empty", pageRect, 28, Faint);
            emptyNote.AsBody();
            Band(emptyNote.rectTransform, 0.6f, 0.7f);
            emptyNote.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "inv.cat.empty";
            emptyNote.gameObject.SetActive(false);
            page.SetActive(false);
        }

        /// <summary>
        /// A vertical scrolling list. RectMask2D clips by rectangle: the stencil Mask it replaces took its shape
        /// from an Image at alpha 0.001, and a graphic that faint can be culled, which silently hid every row.
        /// </summary>
        static RectTransform BuildScroll(RectTransform parent, Vector2 min, Vector2 max)
        {
            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scroll.transform.SetParent(parent, false);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = min;
            scrollRect.anchorMax = max;
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            list.SetParent(scroll.transform, false);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            // Exactly as wide as the viewport; a new RectTransform is 100px wider, and the clip shaved the rows.
            list.sizeDelta = Vector2.zero;
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroller = scroll.GetComponent<ScrollRect>();
            scroller.content = list;
            scroller.horizontal = false;
            scroller.movementType = ScrollRect.MovementType.Elastic;
            return list;
        }

        void BuildPicker(Transform parent)
        {
            Image back = NewImage("Picker", parent, panelSprite, new Color(0.07f, 0.075f, 0.06f, 0.99f));
            back.type = Image.Type.Sliced;
            back.raycastTarget = true;
            back.rectTransform.anchorMin = new Vector2(0.04f, 0.12f);
            back.rectTransform.anchorMax = new Vector2(0.96f, 0.84f);
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            picker = back.gameObject;

            pickerTitle = NewText("Title", back.rectTransform, 36, Ember);
            pickerTitle.AsHeading(3f);
            Band(pickerTitle.rectTransform, 0.88f, 0.98f);

            pickerList = BuildScroll(back.rectTransform, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.87f));

            var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
            cancel.transform.SetParent(back.transform, false);
            var cancelRect = (RectTransform)cancel.transform;
            cancelRect.anchorMin = new Vector2(0.3f, 0.03f);
            cancelRect.anchorMax = new Vector2(0.7f, 0.12f);
            cancelRect.offsetMin = cancelRect.offsetMax = Vector2.zero;
            cancel.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f);
            cancel.GetComponent<Button>().onClick.AddListener(() => picker.SetActive(false));
            TMP_Text label = NewText("Label", cancel.transform, 32, Bone);
            label.AsHeading(3f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "inv.back";
            picker.SetActive(false);
        }

        void BuildCard(Transform parent)
        {
            Image cardImage = NewImage("ItemCard", parent, panelSprite, new Color(0.08f, 0.085f, 0.07f, 0.99f));
            cardImage.type = Image.Type.Sliced;
            cardImage.raycastTarget = true;
            card = cardImage.gameObject;
            var cardRect = cardImage.rectTransform;
            cardRect.anchorMin = new Vector2(0.04f, 0.1f);
            cardRect.anchorMax = new Vector2(0.96f, 0.9f);
            cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;

            cardIcon = NewImage("Icon", card.transform, null, Color.white);
            cardIcon.preserveAspect = true;
            var iconRect = cardIcon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(200f, 200f);
            iconRect.anchoredPosition = new Vector2(0f, -40f);

            cardName = NewText("Name", card.transform, 44, Bone);
            cardName.AsHeading(4f);
            PinCardLine(cardName.rectTransform, -256f, 70f);

            cardKind = NewText("Kind", card.transform, 28, Faint);
            cardKind.AsBody();
            PinCardLine(cardKind.rectTransform, -326f, 44f);

            cardBody = NewText("Body", card.transform, 32, Bone);
            cardBody.AsBody();
            cardBody.alignment = TextAlignmentOptions.TopJustified;
            cardBody.lineSpacing = 8f;
            cardBody.enableAutoSizing = true;
            cardBody.fontSizeMin = 22f;
            cardBody.fontSizeMax = 32f;
            var bodyRect = cardBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0.08f, 0.18f);
            bodyRect.anchorMax = new Vector2(0.92f, 1f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = new Vector2(0f, -392f);

            cardActionButton = CardButton("Action", new Vector2(0.08f, 0.04f), new Vector2(0.48f, 0.13f),
                new Color(0.3f, 0.22f, 0.1f, 1f), out cardAction);
            Button back = CardButton("Back", new Vector2(0.52f, 0.04f), new Vector2(0.92f, 0.13f),
                new Color(0.18f, 0.16f, 0.12f, 1f), out TMP_Text backLabel);
            backLabel.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "inv.back";
            back.onClick.AddListener(() => card.SetActive(false));

            card.SetActive(false);
        }

        static void PinCardLine(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0.06f, 1f);
            rect.anchorMax = new Vector2(0.94f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, top);
        }

        static void Band(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(0.04f, bottom);
            rect.anchorMax = new Vector2(0.96f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        Button CardButton(string name, Vector2 min, Vector2 max, Color colour, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(card.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = colour;
            label = NewText("Label", go.transform, 36, Bone);
            label.AsHeading(3f);
            return go.GetComponent<Button>();
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        static TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform);
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
