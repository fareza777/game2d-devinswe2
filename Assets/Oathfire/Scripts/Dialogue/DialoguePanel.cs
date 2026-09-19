using System;
using System.Collections;
using System.Collections.Generic;
using Oathfire.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oathfire.Dialogue
{
    /// <summary>
    /// Classic RPG dialogue box, built in code: portrait, name plate, typewriter text, a tap-anywhere
    /// advance area, and choice buttons. First tap completes the line, second advances.
    /// </summary>
    public class DialoguePanel : MonoBehaviour, IPointerClickHandler
    {
        const float PortraitSize = 300f;
        const float BoxHeight = 460f;
        const float ChoiceHeight = 112f;
        const int MaxChoices = 6;
        static readonly Color Ink = new Color(0.055f, 0.063f, 0.051f, 0.94f);
        static readonly Color Bone = new Color(0.86f, 0.83f, 0.75f);
        static readonly Color Ember = new Color(0.79f, 0.58f, 0.23f);

        [SerializeField] Sprite boxSprite;
        [SerializeField] Sprite portraitFrame;
        [SerializeField] AudioClip typeBlip;

        RectTransform root;
        Image portrait;
        TMP_Text nameLabel;
        TMP_Text bodyLabel;
        RectTransform choiceHolder;
        Image tapCatcher;
        GameObject continueHint;
        readonly List<Button> choiceButtons = new List<Button>();
        bool tapped;
        bool lineComplete;

        public List<DialogueChoice> VisibleChoices { get; } = new List<DialogueChoice>();

        void Awake() => Build();

        public void Show(bool visible)
        {
            root.gameObject.SetActive(visible);
            if (!visible)
                Core.GameServices.Audio.StopVoice();
        }

        public IEnumerator ShowLineRoutine(DialogueNode node)
        {
            SetSpeaker(node.speaker, node.mood);
            string text = Core.GameServices.Localization.Get(node.textKey);
            PlayVoice(node.voiceClip);

            tapped = false;
            lineComplete = false;
            continueHint.SetActive(false);
            yield return TypeRoutine(text);
            lineComplete = true;
            continueHint.SetActive(true);

            // A tap always moves on, voice or no voice. Waiting out the recording made every conversation feel
            // stuck; a player who wants to hear the line simply does not tap. The short guard swallows the tail
            // of the tap that finished the typing, so one press never skips two lines.
            float earliest = Time.unscaledTime + 0.15f;
            while (!tapped || Time.unscaledTime < earliest)
                yield return null;
            tapped = false;
            Core.GameServices.Audio.StopVoice();
        }

        public IEnumerator ShowChoicesRoutine(DialogueChoice[] choices, Action<int> onPicked)
        {
            VisibleChoices.Clear();
            Save.SaveData save = Core.GameServices.Save.Current;
            foreach (DialogueChoice choice in choices)
            {
                bool required = string.IsNullOrEmpty(choice.requiresFlag) || (save != null && save.HasFlag(choice.requiresFlag));
                bool hidden = !string.IsNullOrEmpty(choice.hiddenIfFlag) && save != null && save.HasFlag(choice.hiddenIfFlag);
                // An offer the player cannot pay for is not shown at all; a greyed-out line just teases.
                bool affordable = choice.costCoin <= 0 ||
                                  (Progress.PlayerState.Instance?.Inventory.coin ?? 0) >= choice.costCoin;
                if (required && !hidden && affordable)
                    VisibleChoices.Add(choice);
            }

            continueHint.SetActive(false);
            // The world dims while a decision is on screen, so the choices are the thing being looked at.
            tapCatcher.color = new Color(0f, 0f, 0f, 0.45f);
            int picked = -1;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool used = i < VisibleChoices.Count;
                choiceButtons[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                int index = i;
                DialogueChoice choice = VisibleChoices[i];
                var label = choiceButtons[i].GetComponentInChildren<TMP_Text>();
                label.text = ToneTag(choice.tone) + Core.GameServices.Localization.Get(choice.textKey);
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => picked = index);
            }

            while (picked < 0)
                yield return null;

            foreach (Button button in choiceButtons)
                button.gameObject.SetActive(false);
            tapCatcher.color = new Color(0f, 0f, 0f, 0f);
            Core.GameServices.Audio.PlaySfx("ui_tap", 0.8f, 0f);
            onPicked(picked);
        }

        /// <summary>Colour-coded tone tag in front of a choice, in the player's language.</summary>
        static string ToneTag(string tone)
        {
            string colour = tone switch
            {
                "iron" => "#b0573f",
                "mercy" => "#5e8b83",
                "guile" => "#c9933b",
                _ => null,
            };
            if (colour == null)
                return string.Empty;
            return $"<color={colour}>[{Core.GameServices.Localization.Get($"tone.{tone}")}]</color> ";
        }

        void SetSpeaker(string speaker, string mood)
        {
            bool narration = string.IsNullOrEmpty(speaker);
            nameLabel.transform.parent.gameObject.SetActive(!narration);
            nameLabel.text = narration ? string.Empty : Core.GameServices.Localization.Get($"speaker.{speaker}");
            bodyLabel.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;

            Sprite art = null;
            if (!narration)
            {
                string moodSuffix = string.IsNullOrEmpty(mood) ? string.Empty : $"_{mood}";
                art = Resources.Load<Sprite>($"Portraits/{speaker}{moodSuffix}") ?? Resources.Load<Sprite>($"Portraits/{speaker}");
            }
            portrait.transform.parent.gameObject.SetActive(art);
            portrait.sprite = art;
        }

        static float PlayVoice(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return 0f;
            var clip = Resources.Load<AudioClip>($"Voice/{clipName}");
            if (!clip)
            {
                Debug.LogWarning($"[Dialogue] Missing voice clip Resources/Voice/{clipName}");
                return 0f;
            }
            return Core.GameServices.Audio.PlayVoice(clip);
        }

        IEnumerator TypeRoutine(string text)
        {
            bodyLabel.text = text;
            bodyLabel.maxVisibleCharacters = 0;
            float speed = Core.GameServices.Settings.TextSpeed;
            if (speed <= 0f)
            {
                bodyLabel.maxVisibleCharacters = int.MaxValue;
                yield break;
            }

            int total = text.Length;
            float shown = 0f;
            int lastBlip = 0;
            while (shown < total)
            {
                if (tapped)
                {
                    tapped = false;
                    break;
                }
                shown += Time.unscaledDeltaTime * speed;
                bodyLabel.maxVisibleCharacters = Mathf.FloorToInt(shown);
                if (typeBlip && bodyLabel.maxVisibleCharacters - lastBlip >= 3)
                {
                    lastBlip = bodyLabel.maxVisibleCharacters;
                    Core.GameServices.Audio.PlaySfx(typeBlip, 0.12f);
                }
                yield return null;
            }
            bodyLabel.maxVisibleCharacters = int.MaxValue;
        }

        public void OnPointerClick(PointerEventData eventData) => tapped = true;

        void Build()
        {
            var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            if (!gameObject.GetComponent<GraphicRaycaster>())
                gameObject.AddComponent<GraphicRaycaster>();

            root = NewRect("DialogueRoot", transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // Tap catcher covers the screen above the box so any tap advances the line.
            var catcher = NewImage("TapArea", root, null, new Color(0, 0, 0, 0));
            catcher.rectTransform.anchorMin = Vector2.zero;
            catcher.rectTransform.anchorMax = Vector2.one;
            catcher.rectTransform.offsetMin = catcher.rectTransform.offsetMax = Vector2.zero;
            catcher.raycastTarget = true;
            tapCatcher = catcher;

            var box = NewImage("Box", root, boxSprite, new Color(Ink.r, Ink.g, Ink.b, 0.97f));
            box.type = Image.Type.Sliced;
            RectTransform boxRect = box.rectTransform;
            boxRect.anchorMin = new Vector2(0f, 0f);
            boxRect.anchorMax = new Vector2(1f, 0f);
            boxRect.pivot = new Vector2(0.5f, 0f);
            boxRect.offsetMin = new Vector2(30f, 40f);
            boxRect.offsetMax = new Vector2(-30f, BoxHeight);

            var portraitHolder = NewImage("PortraitFrame", boxRect, portraitFrame, Color.white);
            portraitHolder.rectTransform.anchorMin = new Vector2(0f, 1f);
            portraitHolder.rectTransform.anchorMax = new Vector2(0f, 1f);
            portraitHolder.rectTransform.pivot = new Vector2(0f, 1f);
            portraitHolder.rectTransform.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            portraitHolder.rectTransform.anchoredPosition = new Vector2(24f, PortraitSize * 0.45f);
            portrait = NewImage("Portrait", portraitHolder.rectTransform, null, Color.white);
            portrait.rectTransform.anchorMin = new Vector2(0.06f, 0.06f);
            portrait.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
            portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
            portrait.preserveAspect = true;

            // Bone plate, ink lettering: the dark sliced box sprite tinted amber came out dark on dark and unreadable.
            var namePlate = NewImage("NamePlate", boxRect, null, Bone);
            namePlate.rectTransform.anchorMin = new Vector2(0f, 1f);
            namePlate.rectTransform.anchorMax = new Vector2(0f, 1f);
            namePlate.rectTransform.pivot = new Vector2(0f, 0f);
            namePlate.rectTransform.sizeDelta = new Vector2(560f, 64f);
            namePlate.rectTransform.anchoredPosition = new Vector2(PortraitSize + 48f, 10f);
            var plateEdge = namePlate.gameObject.AddComponent<Outline>();
            plateEdge.effectColor = Ember;
            plateEdge.effectDistance = new Vector2(3f, -3f);
            nameLabel = NewText("Name", namePlate.rectTransform, 34, new Color(Ink.r, Ink.g, Ink.b, 1f));
            nameLabel.AsHeading(4f);
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 22f;
            nameLabel.fontSizeMax = 34f;
            nameLabel.rectTransform.offsetMin = new Vector2(18f, 4f);
            nameLabel.rectTransform.offsetMax = new Vector2(-18f, -4f);

            bodyLabel = NewText("Body", boxRect, 40, Bone);
            // Justified, with the last line left aligned: a block of speech reads as a printed page rather
            // than a ragged column, which is what the rest of this game's typography is going for.
            bodyLabel.alignment = TextAlignmentOptions.TopJustified;
            bodyLabel.AsBody();
            bodyLabel.lineSpacing = 6f;
            bodyLabel.paragraphSpacing = 12f;
            bodyLabel.wordSpacing = 1f;
            // Justification stretches spaces; without a limit a short last word can tear a line apart.
            bodyLabel.enableWordWrapping = true;
            bodyLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            bodyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            bodyLabel.rectTransform.offsetMin = new Vector2(PortraitSize + 48f, 110f);
            bodyLabel.rectTransform.offsetMax = new Vector2(-36f, -36f);

            continueHint = NewText("ContinueHint", boxRect, 30, Ember).gameObject;
            var hintRect = (RectTransform)continueHint.transform;
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(240f, 50f);
            hintRect.anchoredPosition = new Vector2(-30f, 24f);
            continueHint.GetComponent<TMP_Text>().text = "▼";

            // Choices stand above the dialogue box, full width, so the question stays readable underneath and each
            // answer is a thumb-sized target instead of a line squeezed beside the portrait.
            choiceHolder = NewRect("Choices", root);
            choiceHolder.anchorMin = new Vector2(0f, 0f);
            choiceHolder.anchorMax = new Vector2(1f, 0f);
            choiceHolder.pivot = new Vector2(0.5f, 0f);
            choiceHolder.offsetMin = new Vector2(48f, BoxHeight + PortraitSize * 0.45f + 40f);
            choiceHolder.offsetMax = new Vector2(-48f, BoxHeight + PortraitSize * 0.45f + 40f + MaxChoices * ChoiceHeight + (MaxChoices - 1) * 18f);
            var layout = choiceHolder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.LowerCenter;

            // Six answers: a stall with five wares and a way to leave needs every one of them on screen. With four,
            // "Nothing today" was silently dropped and a shopper could not walk away.
            for (int i = 0; i < MaxChoices; i++)
                choiceButtons.Add(BuildChoiceButton(i));

            root.gameObject.SetActive(false);
        }

        Button BuildChoiceButton(int index)
        {
            var image = NewImage($"Choice{index}", choiceHolder, boxSprite, new Color(0.09f, 0.1f, 0.08f, 0.98f));
            image.type = Image.Type.Sliced;
            var edge = image.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(Ember.r, Ember.g, Ember.b, 0.85f);
            edge.effectDistance = new Vector2(3f, -3f);
            // A button only hears a tap through a raycast-target graphic. Without this every tap fell through to
            // the advance area behind it and the conversation could never move past a choice.
            image.raycastTarget = true;
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minHeight = ChoiceHeight;
            element.preferredHeight = ChoiceHeight;
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.25f, 0.22f, 0.16f);
            colors.pressedColor = Ember;
            button.colors = colors;

            var label = NewText("Label", image.rectTransform, 38, Bone);
            label.AsBody();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 26f;
            label.fontSizeMax = 38f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(34f, 8f);
            label.rectTransform.offsetMax = new Vector2(-34f, -8f);
            image.gameObject.SetActive(false);
            return button;
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
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
