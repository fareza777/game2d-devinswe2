using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The skills page of the Warden's book: every skill, the level it opens at, what it costs, and two buttons
    /// to carry it in the first or second slot on the controls. Skills not yet learned are shown dimmed with the
    /// level they need, so the next few levels have something visible to reach for.
    /// </summary>
    public class SkillPage
    {
        const float RowHeight = 200f;

        readonly RectTransform list;
        readonly List<GameObject> rows = new List<GameObject>();

        public SkillPage(RectTransform list) => this.list = list;

        public int RowCount => rows.Count;

        public void Refresh()
        {
            foreach (GameObject row in rows)
                Object.Destroy(row);
            rows.Clear();

            AddNote(Core.GameServices.Localization.Get("skills.heading"), UiTheme.Moss);
            foreach (Progress.SkillBook.Entry entry in Progress.SkillBook.Entries)
                AddSkill(entry);
            AddNote(Core.GameServices.Localization.Get("skills.dodge"), UiTheme.BoneDim);
        }

        void AddNote(string text, Color colour)
        {
            var go = new GameObject("Note", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(list, false);
            go.GetComponent<LayoutElement>().preferredHeight = 70f;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.AsBody();
            label.fontSize = 28f;
            label.color = colour;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = text;
            rows.Add(go);
        }

        void AddSkill(Progress.SkillBook.Entry entry)
        {
            bool learned = Progress.SkillBook.IsLearned(entry);
            var row = new GameObject(entry.id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(list, false);
            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var back = row.GetComponent<Image>();
            back.color = learned ? new Color(0.12f, 0.11f, 0.08f, 0.96f) : new Color(0.07f, 0.075f, 0.065f, 0.9f);
            back.raycastTarget = false;
            var edge = row.AddComponent<Outline>();
            edge.effectColor = Progress.SkillBook.IsCarried(entry.id) ? new Color(0.91f, 0.69f, 0.29f, 0.9f) : new Color(0.36f, 0.33f, 0.26f, 0.5f);
            edge.effectDistance = new Vector2(2f, -2f);
            rows.Add(row);

            // The ability's own painted icon, in a dark well on the left.
            var well = Rect("Well", row.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            well.pivot = new Vector2(0f, 0.5f);
            well.sizeDelta = new Vector2(150f, 150f);
            well.anchoredPosition = new Vector2(20f, 0f);
            var wellImage = well.gameObject.AddComponent<Image>();
            wellImage.color = new Color(0.03f, 0.035f, 0.03f, 1f);
            wellImage.raycastTarget = false;
            if (entry.ability && entry.ability.Icon)
            {
                var icon = Rect("Icon", well, Vector2.zero, Vector2.one);
                icon.offsetMin = new Vector2(12f, 12f);
                icon.offsetMax = new Vector2(-12f, -12f);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = entry.ability.Icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconImage.color = learned ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            var text = Rect("Text", row.transform, Vector2.zero, Vector2.one);
            text.offsetMin = new Vector2(190f, 14f);
            text.offsetMax = new Vector2(learned ? -210f : -24f, -14f);
            var label = text.gameObject.AddComponent<TextMeshProUGUI>();
            label.AsBody();
            label.fontSize = 28f;
            label.color = learned ? UiTheme.Bone : UiTheme.BoneDim;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 28f;
            label.raycastTarget = false;
            string name = Core.GameServices.Localization.Get(entry.NameKey);
            string description = Core.GameServices.Localization.Get(entry.DescriptionKey);
            string status = learned && entry.ability
                ? Core.GameServices.Localization.Format("skills.cost", Mathf.RoundToInt(entry.ability.ManaCost), Mathf.RoundToInt(entry.ability.CooldownSeconds))
                : Core.GameServices.Localization.Format("skills.locked", entry.level);
            string nameColour = learned ? "#e8b04a" : "#8a8474";
            label.text = $"<size=120%><color={nameColour}>{name}</color></size>\n{description}\n<size=85%><color=#7fa89a>{status}</color></size>";

            if (!learned)
                return;
            for (int slot = 0; slot < Progress.SkillBook.Slots; slot++)
                SlotButton(row.transform, entry, slot);
        }

        void SlotButton(Transform row, Progress.SkillBook.Entry entry, int slot)
        {
            bool here = Progress.SkillBook.InSlot(slot) == entry;
            var rect = Rect($"Slot {slot + 1}", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(88f, 88f);
            rect.anchoredPosition = new Vector2(slot == 0 ? -114f : -18f, 0f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = here ? new Color(0.45f, 0.32f, 0.12f, 1f) : new Color(0.16f, 0.15f, 0.12f, 1f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                Progress.SkillBook.Assign(slot, entry.id);
                Core.GameServices.Audio.PlaySfx("equip", 0.8f, 0f);
                Refresh();
            });
            var label = Rect("Label", rect, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            label.AsHeading(2f);
            label.fontSize = 40f;
            label.color = here ? UiTheme.Bone : UiTheme.BoneDim;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = slot == 0 ? "I" : "II";
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
    }
}
