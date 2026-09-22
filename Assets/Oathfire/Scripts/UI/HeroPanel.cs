using System.Linq;
using SmallScale.FantasyKingdomTileset;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The Warden's book, in two pages. JOURNAL is the story so far: the main quest first, then side work, each
    /// with what it is about, the steps already done, and the one to do now with its count. WARDEN is who Kael
    /// is, what he carries, how the powers of the valley regard him, and what he has done. Everything is read
    /// from the same save the rest of the game writes, so the book cannot disagree with the world.
    /// </summary>
    public class HeroPanel : MonoBehaviour
    {
        static readonly (Items.EquipSlot slot, string labelKey)[] Slots =
        {
            (Items.EquipSlot.MainHand, "hero.slot.mainHand"),
            (Items.EquipSlot.OffHand, "hero.slot.offHand"),
            (Items.EquipSlot.Head, "hero.slot.head"),
            (Items.EquipSlot.Body, "hero.slot.body"),
            (Items.EquipSlot.Trinket, "hero.slot.trinket"),
            (Items.EquipSlot.Neck, "hero.slot.neck"),
            (Items.EquipSlot.Hands, "hero.slot.hands"),
            (Items.EquipSlot.Legs, "hero.slot.legs"),
            (Items.EquipSlot.Feet, "hero.slot.feet"),
            (Items.EquipSlot.Ring, "hero.slot.ring"),
        };

        static readonly (string faction, string labelKey)[] Factions =
        {
            ("hearth", "faction.hearth"),
            ("crown", "faction.crown"),
            ("ash", "faction.ash"),
            ("salt", "faction.salt"),
        };

        [SerializeField] Sprite panelSprite;
        [SerializeField] Sprite frameSprite;

        RectTransform root;
        RectTransform sheetRect;
        GameObject journalPage;
        GameObject wardenPage;
        GameObject skillsPage;
        SkillPage skills;
        RectTransform questList;
        (Image back, TMP_Text label)[] tabs;
        readonly System.Collections.Generic.List<GameObject> entries = new System.Collections.Generic.List<GameObject>();
        TMP_Text statsLabel;
        TMP_Text equipmentLabel;
        TMP_Text standingLabel;
        TMP_Text deedsLabel;

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
            root.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            bool showing = !root.gameObject.activeSelf;
            root.gameObject.SetActive(showing);
            Controls.MobileControls.GameplayActive = !showing;

            // The HUD's outlined text survives even a dark shade and ghosts through the page, so it steps
            // aside entirely while the book is open.
            GameplayHud hud = FindAnyObjectByType<GameplayHud>();
            if (hud && hud.TryGetComponent(out Canvas hudCanvas))
                hudCanvas.enabled = !showing;

            if (!showing)
                Core.GameServices.Ads?.NoteMenuClosed();
            if (showing)
                ShowPage(journal: true);
        }

        public bool ShowingJournal => journalPage && journalPage.activeSelf;

        public bool ShowingSkills => skillsPage && skillsPage.activeSelf;

        public int SkillRowCount => skills?.RowCount ?? 0;

        /// <summary>Opens the skills page.</summary>
        public void ShowSkills() => Show(1);

        public int JournalEntryCount => entries.Count;

        public void ShowPage(bool journal) => Show(journal ? 0 : 2);

        /// <summary>0 journal, 1 skills, 2 the Warden's sheet — the order the tabs run.</summary>
        void Show(int page)
        {
            journalPage.SetActive(page == 0);
            skillsPage.SetActive(page == 1);
            wardenPage.SetActive(page == 2);
            if (page == 1)
                skills.Refresh();
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = i == page;
                tabs[i].back.color = active ? new Color(0.3f, 0.22f, 0.1f, 1f) : new Color(0.12f, 0.12f, 0.1f, 1f);
                tabs[i].label.color = active ? UiTheme.Bone : UiTheme.BoneDim;
            }
            Refresh();
            RefreshJournal();
        }

        /// <summary>
        /// One entry per quest the player has begun: the story in progress first, then side work, then what is
        /// finished, which only keeps its title so the book does not grow into a wall of old errands.
        /// </summary>
        void RefreshJournal()
        {
            foreach (GameObject entry in entries)
                Destroy(entry);
            entries.Clear();

            Quests.QuestService service = Quests.QuestRuntime.Service;
            var active = service.Quests.Where(q => service.IsAvailable(q) && service.CurrentStage(q) != null).ToList();
            var done = service.Quests.Where(q => service.IsAvailable(q) && service.IsComplete(q)).ToList();

            AddHeading("journal.main");
            foreach (Quests.QuestDefinition quest in active.Where(q => q.mainQuest))
                AddQuest(quest, service);
            if (!active.Any(q => q.mainQuest))
                AddNote("journal.mainNone");

            AddHeading("journal.side");
            foreach (Quests.QuestDefinition quest in active.Where(q => !q.mainQuest))
                AddQuest(quest, service);
            if (!active.Any(q => !q.mainQuest))
                AddNote("journal.sideNone");

            if (done.Count > 0)
            {
                AddHeading("journal.done");
                foreach (Quests.QuestDefinition quest in done)
                    AddNote(null, $"<color=#6f6a5c><s>{Core.GameServices.Localization.Get(quest.TitleKey)}</s></color>");
            }
        }

        void AddHeading(string key)
        {
            TMP_Text heading = EntryText(34, UiTheme.Moss, 64f);
            heading.AsHeading(6f);
            heading.alignment = TextAlignmentOptions.BottomLeft;
            heading.text = Core.GameServices.Localization.Get(key);
        }

        void AddNote(string key, string text = null)
        {
            TMP_Text note = EntryText(28, UiTheme.BoneDim, 50f);
            note.alignment = TextAlignmentOptions.MidlineLeft;
            note.text = text ?? Core.GameServices.Localization.Get(key);
        }

        /// <summary>Title, what it is about, finished steps struck through, and the current step with its count.</summary>
        void AddQuest(Quests.QuestDefinition quest, Quests.QuestService service)
        {
            Quests.QuestStage current = service.CurrentStage(quest);
            var body = new System.Text.StringBuilder();
            string colour = quest.mainQuest ? "#c9933b" : "#7fa89a";
            bool followed = Quests.QuestRuntime.FocusedQuest() == quest;
            string mark = followed ? $"  <size=70%><color=#e8b04a>\u25c6 {Core.GameServices.Localization.Get("journal.following")}</color></size>" : string.Empty;
            body.Append($"<size=120%><color={colour}>{Core.GameServices.Localization.Get(quest.TitleKey)}</color></size>{mark}\n");
            body.Append($"<i><color=#a8a293>{Core.GameServices.Localization.Get(quest.SummaryKey)}</color></i>\n");
            foreach (Quests.QuestStage stage in quest.stages)
            {
                string step = Core.GameServices.Localization.Get(stage.DescriptionKeyFor(quest.id));
                if (stage == current)
                {
                    if (service.IsReadyToHandIn(quest))
                    {
                        body.Append($"<color=#6f6a5c>  <s>{step}</s></color>\n");
                        body.Append($"<color=#e8b04a>  \u2022 {Quests.QuestRuntime.StepText(quest)}</color>\n");
                    }
                    else
                        body.Append($"<color=#ded6c4>  \u2022 {step}{StepProgress(stage)}</color>\n");
                    break;
                }
                body.Append($"<color=#6f6a5c>  <s>{step}</s></color>\n");
            }

            if (!followed)
                body.Append($"<size=80%><color=#6f6a5c>{Core.GameServices.Localization.Get("journal.tapToFollow")}</color></size>\n");

            var box = new GameObject(quest.id, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Button));
            box.transform.SetParent(questList, false);
            var image = box.GetComponent<Image>();
            image.color = quest.mainQuest ? new Color(0.14f, 0.12f, 0.08f, 0.95f) : new Color(0.09f, 0.11f, 0.1f, 0.95f);
            image.raycastTarget = true;
            box.GetComponent<Button>().onClick.AddListener(() =>
            {
                Quests.QuestRuntime.Track(quest);
                Core.GameServices.Audio.PlaySfx("ui_tap", 0.8f, 0f);
                RefreshJournal();
            });
            var edge = box.AddComponent<Outline>();
            edge.effectColor = followed ? new Color(0.91f, 0.69f, 0.29f, 0.95f)
                : quest.mainQuest ? new Color(0.79f, 0.58f, 0.23f, 0.5f) : new Color(0.37f, 0.54f, 0.51f, 0.45f);
            edge.effectDistance = followed ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 20, 20);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            box.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text text = NewText("Text", box.transform, 30, UiTheme.Bone);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.lineSpacing = 6f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = body.ToString();
            entries.Add(box);
        }

        static string StepProgress(Quests.QuestStage stage)
        {
            foreach (Quests.QuestObjective objective in stage.objectives)
            {
                if (objective.amount <= 1)
                    continue;
                int have = objective.kind switch
                {
                    Quests.ObjectiveKind.Counter => Core.GameServices.Save.Current?.GetCounter(objective.key) ?? 0,
                    Quests.ObjectiveKind.Item => Progress.PlayerState.Instance?.Inventory.Count(objective.key) ?? 0,
                    _ => 0,
                };
                return $"  <color=#7fa89a>{Mathf.Min(have, objective.amount)}/{objective.amount}</color>";
            }
            return string.Empty;
        }

        TMP_Text EntryText(float size, Color colour, float height)
        {
            TMP_Text text = NewText("Line", questList, size, colour);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            entries.Add(text.gameObject);
            return text;
        }

        void Refresh()
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            Save.SaveData save = Core.GameServices.Save.Current;
            if (state == null)
                return;

            PlayerHealth health = PlayerHealth.Instance;
            var stats = new System.Text.StringBuilder();
            Line(stats, "hero.level", state.Level.ToString());
            Line(stats, "hero.experience", $"{state.Experience} / {state.ExperienceForNextLevel}");
            if (health)
                Line(stats, "hero.health", $"{Mathf.Max(0, health.currentHealth)} / {health.maxHealth}");
            Line(stats, "hero.damage", state.Damage.ToString());
            Line(stats, "hero.armour", state.Armor.ToString());
            Line(stats, "hero.coin", state.Inventory.coin.ToString());
            statsLabel.text = stats.ToString();

            // Only what is actually worn: ten slots listed one per line, mostly "nothing", would crowd the page.
            var worn = new System.Text.StringBuilder();
            foreach ((Items.EquipSlot slot, string labelKey) in Slots)
            {
                string itemId = state.Inventory.EquippedIn(slot);
                Items.ItemDefinition item = string.IsNullOrEmpty(itemId) ? null : Items.ItemDatabase.Get(itemId);
                if (item != null)
                    Line(worn, labelKey, Core.GameServices.Localization.Get(item.NameKey));
            }
            if (worn.Length == 0)
                Line(worn, "hero.slot.body", Core.GameServices.Localization.Get("hero.slot.empty"));
            equipmentLabel.text = worn.ToString();

            var standing = new System.Text.StringBuilder();
            foreach ((string faction, string labelKey) in Factions)
            {
                int value = save?.GetReputation(faction) ?? 0;
                Line(standing, labelKey, $"{value:+#;-#;0}  {Bar(value)}");
            }
            standingLabel.text = standing.ToString();

            var deeds = new System.Text.StringBuilder();
            Line(deeds, "hero.nights", (save?.GetCounter("nights.survived") ?? 0).ToString());
            Line(deeds, "hero.contracts", (save?.GetCounter("contracts.completed") ?? 0).ToString());
            Line(deeds, "hero.built", (save?.GetCounter("built.shelter") ?? 0).ToString());
            Line(deeds, "hero.felled", TotalKills(save).ToString());
            deedsLabel.text = deeds.ToString();
        }

        static int TotalKills(Save.SaveData save) =>
            save == null ? 0 : new[] { "kills.skeleton", "kills.knight", "kills.mage", "kills.elite", "kills.wild", "kills.bandit" }
                .Sum(save.GetCounter);

        /// <summary>Reputation as a row of marks, so the number is not the only thing to read.</summary>
        static string Bar(int value)
        {
            int marks = Mathf.Clamp(Mathf.Abs(value) / 5, 0, 6);
            string glyph = value < 0 ? "×" : "·";
            return new string(glyph[0], marks);
        }

        /// <summary>One ledger line: the label, then the value starting at a fixed column.</summary>
        static void Line(System.Text.StringBuilder into, string labelKey, string value) =>
            into.AppendLine($"<color=#a8a293>{Core.GameServices.Localization.Get(labelKey)}</color>" +
                            $"<pos=46%><color=#ded6c4>{value}</color>");

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 620;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = NewRect("Root", transform);
            Stretch(root);

            // Nearly opaque: the HUD's words showing through the book read as clutter, not as depth.
            Image shade = NewImage("Shade", root, null, new Color(0.02f, 0.03f, 0.02f, 0.94f));
            shade.raycastTarget = true;
            Stretch(shade.rectTransform);

            // The sheet is sized by margins, not by pixels, so it fits a tall phone and a short tablet alike.
            Image sheet = NewImage("Sheet", root, panelSprite, UiTheme.InkPanel);
            sheet.type = Image.Type.Sliced;
            sheetRect = sheet.rectTransform;
            sheetRect.anchorMin = new Vector2(0.04f, 0.03f);
            sheetRect.anchorMax = new Vector2(0.96f, 0.97f);
            sheetRect.offsetMin = sheetRect.offsetMax = Vector2.zero;

            TMP_Text title = NewText("Title", sheetRect, 56, UiTheme.Ember);
            title.AsHeading(8f);
            PinTop(title.rectTransform, 0.915f, 0.985f);
            title.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "hero.title";

            tabs = new[]
            {
                Tab("journal.tab", new Vector2(0.05f, 0.855f), new Vector2(0.345f, 0.91f), () => Show(0)),
                Tab("journal.tabSkills", new Vector2(0.355f, 0.855f), new Vector2(0.645f, 0.91f), () => Show(1)),
                Tab("journal.tabWarden", new Vector2(0.655f, 0.855f), new Vector2(0.95f, 0.91f), () => Show(2)),
            };

            journalPage = NewRect("Journal", sheetRect).gameObject;
            var journalRect = (RectTransform)journalPage.transform;
            journalRect.anchorMin = new Vector2(0.05f, 0.1f);
            journalRect.anchorMax = new Vector2(0.95f, 0.845f);
            journalRect.offsetMin = journalRect.offsetMax = Vector2.zero;
            questList = Scroll(journalRect);

            skillsPage = NewRect("Skills", sheetRect).gameObject;
            var skillsRect = (RectTransform)skillsPage.transform;
            skillsRect.anchorMin = new Vector2(0.05f, 0.1f);
            skillsRect.anchorMax = new Vector2(0.95f, 0.845f);
            skillsRect.offsetMin = skillsRect.offsetMax = Vector2.zero;
            skills = new SkillPage(Scroll(skillsRect));
            skillsPage.SetActive(false);

            wardenPage = NewRect("Warden", sheetRect).gameObject;
            var wardenRect = (RectTransform)wardenPage.transform;
            Stretch(wardenRect);

            TMP_Text name = NewText("Name", wardenRect, 34, UiTheme.Bone);
            name.AsHeading(3f);
            PinTop(name.rectTransform, 0.8f, 0.845f);
            name.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "hero.name";

            // The four sections share whatever height is left between the name and the close button.
            RectTransform content = NewRect("Content", wardenRect);
            content.anchorMin = new Vector2(0.07f, 0.1f);
            content.anchorMax = new Vector2(0.93f, 0.79f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            statsLabel = Section(content, "hero.section.self", 6);
            equipmentLabel = Section(content, "hero.section.worn", 5);
            standingLabel = Section(content, "hero.section.standing", 4);
            deedsLabel = Section(content, "hero.section.deeds", 4);

            BuildCloseButton(sheetRect);
        }

        (Image, TMP_Text) Tab(string key, Vector2 min, Vector2 max, System.Action open)
        {
            Image back = NewImage(key, sheetRect, null, new Color(0.12f, 0.12f, 0.1f, 1f));
            back.raycastTarget = true;
            back.rectTransform.anchorMin = min;
            back.rectTransform.anchorMax = max;
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => open());
            TMP_Text label = NewText("Label", back.rectTransform, 30, UiTheme.Bone);
            label.AsHeading(5f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
            return (back, label);
        }

        /// <summary>A clipped vertical list; RectMask2D, because a near-invisible stencil mask once hid every row.</summary>
        static RectTransform Scroll(RectTransform parent)
        {
            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scroll.transform.SetParent(parent, false);
            var scrollRect = (RectTransform)scroll.transform;
            Stretch(scrollRect);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            list.SetParent(scroll.transform, false);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = Vector2.zero;
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroller = scroll.GetComponent<ScrollRect>();
            scroller.content = list;
            scroller.horizontal = false;
            return list;
        }

        static void PinTop(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(0.05f, bottom);
            rect.anchorMax = new Vector2(0.95f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// One titled block: heading, rule, and its lines. Its share of the page follows how many lines it
        /// holds, and the text auto-sizes down a little if a short screen leaves it less room than it wants.
        /// </summary>
        TMP_Text Section(RectTransform parent, string headingKey, int lines)
        {
            RectTransform block = NewRect($"Section {headingKey}", parent);
            var element = block.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = lines + 1.5f;

            TMP_Text heading = NewText("Heading", block, 32, UiTheme.Moss);
            heading.AsHeading(6f);
            heading.alignment = TextAlignmentOptions.BottomLeft;
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 46f);
            heading.rectTransform.anchoredPosition = Vector2.zero;
            heading.gameObject.AddComponent<Localization.LocalizedLabel>().Key = headingKey;

            Image rule = NewImage("Rule", block, null, new Color(0.36f, 0.33f, 0.26f, 0.7f));
            rule.rectTransform.anchorMin = new Vector2(0f, 1f);
            rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.pivot = new Vector2(0.5f, 1f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 2f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -50f);

            TMP_Text body = NewText("Body", block, 30, UiTheme.Bone);
            body.AsBody();
            body.alignment = TextAlignmentOptions.TopLeft;
            body.lineSpacing = 8f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 30f;
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = new Vector2(0f, -60f);
            return body;
        }

        void BuildCloseButton(RectTransform parent)
        {
            Image image = NewImage("Close", parent, frameSprite, new Color(0.45f, 0.42f, 0.32f, 0.95f));
            image.raycastTarget = true;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(420f, 92f);
            rect.anchoredPosition = new Vector2(0f, 40f);

            image.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);
            TMP_Text label = NewText("Label", rect, 36, UiTheme.Bone);
            label.AsHeading(4f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "hero.close";
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.AsBody();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
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
