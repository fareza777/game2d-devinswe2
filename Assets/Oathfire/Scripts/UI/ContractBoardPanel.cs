using System.Collections.Generic;
using Oathfire.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The notice board sheet: posted jobs with client, rank, reward and progress. One tap takes a job,
    /// one tap claims a finished one. Built for a thumb, so rows are large and the action is a single button.
    /// </summary>
    public class ContractBoardPanel : MonoBehaviour
    {
        static readonly Dictionary<ContractRank, Color> RankColors = new()
        {
            { ContractRank.Copper, new Color(0.78f, 0.55f, 0.36f) },
            { ContractRank.Iron, new Color(0.72f, 0.74f, 0.76f) },
            { ContractRank.Silver, new Color(0.62f, 0.78f, 0.86f) },
            { ContractRank.Oathbound, UiTheme.EmberBright },
        };

        [SerializeField] Sprite panelSprite;

        GameObject root;
        RectTransform listHolder;
        TMP_Text summary;
        readonly List<GameObject> rows = new List<GameObject>();

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
            if (show)
            {
                ContractBoard.RollPostings(Core.GameServices.Save.Current?.GetCounter("nights.survived") ?? 0);
                Refresh();
            }
        }

        void Refresh()
        {
            foreach (GameObject row in rows)
                Destroy(row);
            rows.Clear();

            int taken = 0;
            foreach (PostedContract contract in ContractBoard.Posted)
            {
                AddRow(contract);
                if (contract.accepted)
                    taken++;
            }
            summary.text = Core.GameServices.Localization.Format("board.summary", ContractBoard.Posted.Count, taken,
                Core.GameServices.Save.Current?.GetCounter("contracts.completed") ?? 0);
        }

        void AddRow(PostedContract contract)
        {
            ContractTemplate template = contract.Template;
            if (template == null)
                return;

            var row = new GameObject(template.id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(listHolder, false);
            row.GetComponent<Image>().color = contract.accepted ? new Color(0.16f, 0.15f, 0.11f, 0.96f) : new Color(0.1f, 0.11f, 0.09f, 0.92f);
            row.GetComponent<LayoutElement>().preferredHeight = 250f;

            TMP_Text title = NewText("Title", row.transform, 36, RankColors[template.rank]);
            title.AsHeading(3f);
            title.alignment = TextAlignmentOptions.TopLeft;
            title.rectTransform.offsetMin = new Vector2(24f, 0f);
            title.rectTransform.offsetMax = new Vector2(-24f, -12f);
            title.text = $"{Core.GameServices.Localization.Get(template.TitleKey)}   <size=70%><color=#8d8a80>{template.rank}</color></size>";

            TMP_Text client = NewText("Client", row.transform, 26, UiTheme.Moss);
            client.alignment = TextAlignmentOptions.TopLeft;
            client.rectTransform.offsetMin = new Vector2(24f, 0f);
            client.rectTransform.offsetMax = new Vector2(-24f, -56f);
            client.text = Core.GameServices.Localization.Get(template.ClientKey);

            TMP_Text body = NewText("Body", row.transform, 27, UiTheme.BoneDim);
            body.fontStyle = FontStyles.Italic;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.rectTransform.offsetMin = new Vector2(24f, 86f);
            body.rectTransform.offsetMax = new Vector2(-260f, -92f);
            body.text = Core.GameServices.Localization.Get(template.BodyKey);

            TMP_Text reward = NewText("Reward", row.transform, 28, UiTheme.Ember);
            reward.alignment = TextAlignmentOptions.BottomLeft;
            reward.rectTransform.offsetMin = new Vector2(24f, 16f);
            reward.rectTransform.offsetMax = new Vector2(-260f, 0f);
            int coin = template.coinPerUnit * contract.amount;
            string rewardItem = string.IsNullOrEmpty(template.rewardItem)
                ? string.Empty
                : $" + {Core.GameServices.Localization.Get($"item.{template.rewardItem}.name")}";
            reward.text = Core.GameServices.Localization.Format("board.reward", contract.amount, coin) + rewardItem;

            BuildActionButton(row.transform, contract, template);
            rows.Add(row);
        }

        void BuildActionButton(Transform parent, PostedContract contract, ContractTemplate template)
        {
            bool complete = ContractBoard.IsComplete(contract);
            var button = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(210f, -40f);
            rect.anchoredPosition = new Vector2(-20f, 0f);
            button.GetComponent<Image>().color = complete ? new Color(0.35f, 0.42f, 0.28f) : contract.accepted ? new Color(0.2f, 0.19f, 0.15f) : new Color(0.3f, 0.24f, 0.14f);

            TMP_Text label = NewText("Label", button.transform, 30, UiTheme.Bone);
            label.AsHeading(4f);
            string key = complete ? "board.claim" : contract.accepted ? "board.inProgress" : "board.accept";
            label.text = Core.GameServices.Localization.Get(key);
            if (contract.accepted && !complete)
                label.text += $"\n<size=80%><color=#5e8b83>{ContractBoard.ProgressOf(contract)}/{contract.amount}</color></size>";

            button.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (complete)
                    ContractBoard.TryClaim(contract);
                else if (!contract.accepted)
                    ContractBoard.Accept(contract);
                Refresh();
            });
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 710;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            var sheet = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
            sheet.transform.SetParent(root.transform, false);
            var sheetRect = (RectTransform)sheet.transform;
            sheetRect.anchorMin = new Vector2(0.04f, 0.08f);
            sheetRect.anchorMax = new Vector2(0.96f, 0.94f);
            sheetRect.offsetMin = sheetRect.offsetMax = Vector2.zero;
            Image sheetImage = sheet.GetComponent<Image>();
            sheetImage.sprite = panelSprite;
            sheetImage.type = Image.Type.Sliced;
            sheetImage.color = UiTheme.InkPanel;

            TMP_Text heading = NewText("Heading", sheet.transform, 50, UiTheme.Ember);
            heading.AsHeading(10f);
            heading.rectTransform.anchorMin = new Vector2(0f, 0.93f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;
            heading.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "board.title";

            summary = NewText("Summary", sheet.transform, 28, UiTheme.BoneDim);
            summary.rectTransform.anchorMin = new Vector2(0f, 0.88f);
            summary.rectTransform.anchorMax = new Vector2(1f, 0.93f);
            summary.rectTransform.offsetMin = summary.rectTransform.offsetMax = Vector2.zero;

            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scroll.transform.SetParent(sheet.transform, false);
            var scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0.03f, 0.09f);
            scrollRect.anchorMax = new Vector2(0.97f, 0.87f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            listHolder = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            listHolder.SetParent(scroll.transform, false);
            listHolder.anchorMin = new Vector2(0f, 1f);
            listHolder.anchorMax = new Vector2(1f, 1f);
            listHolder.pivot = new Vector2(0.5f, 1f);
            var layout = listHolder.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            listHolder.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroller = scroll.GetComponent<ScrollRect>();
            scroller.content = listHolder;
            scroller.horizontal = false;

            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(sheet.transform, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.28f, 0.015f);
            closeRect.anchorMax = new Vector2(0.72f, 0.075f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            close.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f);
            close.GetComponent<Button>().onClick.AddListener(Toggle);
            TMP_Text closeLabel = NewText("Label", close.transform, 34, UiTheme.Bone);
            closeLabel.AsHeading(6f);
            closeLabel.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "board.close";

            root.SetActive(false);
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
