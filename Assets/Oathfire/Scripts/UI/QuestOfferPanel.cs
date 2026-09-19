using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The card a villager holds out when they have work for the Warden: what they want, the first step, and
    /// what it pays. Nothing enters the journal until it has been taken here, so the book is what the Warden
    /// has agreed to do rather than a list of everything the valley could ever ask.
    /// </summary>
    public class QuestOfferPanel : MonoBehaviour
    {
        [SerializeField] Sprite panelSprite;

        public static QuestOfferPanel Instance { get; private set; }

        /// <summary>The offer on screen, or null when the card is closed; read by the smoke test.</summary>
        public Quests.QuestDefinition Showing { get; private set; }

        RectTransform card;
        TMP_Text eyebrowLabel;
        TMP_Text acceptLabel;
        GameObject laterButton;
        bool handingIn;
        RectTransform sheet;
        TMP_Text title;
        TMP_Text summary;
        TMP_Text step;
        TMP_Text reward;
        Action closed;

        void Awake()
        {
            Instance = this;
            Core.GameServices.EnsureCreated();
            Build();
            card.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>The card for work brought back: what was done, what it pays, and one button to hand it over.</summary>
        public void ShowHandIn(Quests.QuestDefinition quest, Action onClosed = null)
        {
            Show(quest, onClosed);
            handingIn = true;
            SetKey(eyebrowLabel, "offer.doneHeading");
            SetKey(acceptLabel, "offer.handIn");
            laterButton.SetActive(false);
            step.text = RewardItems(quest);
            Core.GameServices.Audio.PlaySfx("quest_complete", 0.9f, 0f);
        }

        static void SetKey(TMP_Text label, string key)
        {
            var localized = label.GetComponent<Localization.LocalizedLabel>();
            if (localized)
                localized.Key = key;
            label.text = Core.GameServices.Localization.Get(key);
        }

        static string RewardItems(Quests.QuestDefinition quest)
        {
            if (quest.reward?.items == null || quest.reward.items.Length == 0)
                return string.Empty;
            var names = new System.Collections.Generic.List<string>();
            foreach (string itemId in quest.reward.items)
            {
                Items.ItemDefinition item = Items.ItemDatabase.Get(itemId);
                if (item != null)
                    names.Add(Core.GameServices.Localization.Get(item.NameKey));
            }
            return names.Count == 0 ? string.Empty : "+ " + string.Join(", ", names);
        }

        public void Show(Quests.QuestDefinition quest, Action onClosed = null)
        {
            handingIn = false;
            SetKey(eyebrowLabel, "offer.heading");
            SetKey(acceptLabel, "offer.accept");
            laterButton.SetActive(true);
            Showing = quest;
            closed = onClosed;
            Localization.LocalizationService text = Core.GameServices.Localization;
            title.text = text.Get(quest.TitleKey);
            summary.text = text.Get(quest.SummaryKey);
            Quests.QuestStage first = quest.stages != null && quest.stages.Length > 0 ? quest.stages[0] : null;
            step.text = first != null ? "— " + text.Get(first.DescriptionKeyFor(quest.id)) : string.Empty;
            reward.text = text.Format("offer.reward", quest.reward?.experience ?? 0, quest.reward?.coin ?? 0);
            card.gameObject.SetActive(true);
            Controls.MobileControls.GameplayActive = false;
            Core.GameServices.Audio.PlaySfx("quest_new", 0.9f, 0f);
        }

        void Close(bool accepted)
        {
            if (Showing != null && handingIn)
            {
                string title = Core.GameServices.Localization.Get(Showing.TitleKey);
                if (Quests.QuestRuntime.Service.HandIn(Showing))
                    Toast.ShowText(Core.GameServices.Localization.Format("toast.questDone", title));
            }
            else if (Showing != null && accepted)
            {
                Quests.QuestRuntime.Service.Accept(Showing);
                if (Quests.QuestRuntime.TrackedQuest() == null)
                    Quests.QuestRuntime.Track(Showing);
                Toast.ShowText(Core.GameServices.Localization.Format("toast.questTaken",
                    Core.GameServices.Localization.Get(Showing.TitleKey)));
            }
            Showing = null;
            card.gameObject.SetActive(false);
            Controls.MobileControls.GameplayActive = true;
            Core.GameServices.Audio.PlaySfx("ui_close", 0.7f, 0f);
            Action after = closed;
            closed = null;
            after?.Invoke();
        }

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

            // The card and the dimmed world are one unit: both hide together, and the dimming is drawn first.
            card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(transform, false);
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = card.offsetMax = Vector2.zero;

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(card, false);
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            sheet = new GameObject("Sheet", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            sheet.SetParent(card, false);
            sheet.anchorMin = new Vector2(0.5f, 0.5f);
            sheet.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(940f, 760f);
            var background = sheet.GetComponent<Image>();
            background.sprite = panelSprite;
            background.type = Image.Type.Sliced;
            background.color = UiTheme.InkPanel;

            TMP_Text eyebrow = Label("Eyebrow", 30, UiTheme.Moss, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.95f));
            eyebrow.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "offer.heading";
            eyebrow.AsHeading(8f);
            eyebrowLabel = eyebrow;

            title = Label("Title", 52, UiTheme.EmberBright, new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.87f));
            title.AsHeading(3f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 32f;
            title.fontSizeMax = 52f;

            summary = Label("Summary", 32, UiTheme.Bone, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.71f));
            summary.alignment = TextAlignmentOptions.Top;
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 20f;
            summary.fontSizeMax = 32f;
            summary.overflowMode = TextOverflowModes.Ellipsis;

            step = Label("Step", 30, UiTheme.BoneDim, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.41f));
            step.alignment = TextAlignmentOptions.Top;

            reward = Label("Reward", 30, UiTheme.Ember, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.29f));
            reward.AsHeading(4f);

            acceptLabel = Button("Accept", "offer.accept", new Vector2(0.08f, 0.05f), new Vector2(0.48f, 0.17f), UiTheme.Ember, () => Close(true));
            laterButton = Button("Later", "offer.later", new Vector2(0.52f, 0.05f), new Vector2(0.92f, 0.17f), UiTheme.BoneDim, () => Close(false)).transform.parent.gameObject;
        }

        TMP_Text Label(string name, float size, Color colour, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(sheet, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.AsBody();
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }

        TMP_Text Button(string name, string labelKey, Vector2 min, Vector2 max, Color tint, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(sheet, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 0.95f);
            image.raycastTarget = true;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.AsHeading(6f);
            text.fontSize = 34f;
            text.color = tint;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Localization.LocalizedLabel>().Key = labelKey;
            var labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
