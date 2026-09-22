using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The road menu, reached from the HUD's gear medallion. Resume, write the save book into one of
    /// three slots, or ride back to the title screen — the game clock stops while it is open so a
    /// night wave cannot bite through the paper.
    /// </summary>
    public class PauseMenuPanel : MonoBehaviour
    {
        const int BenefactorCoins = 25;

        static readonly Color Ink = new Color(0.06f, 0.07f, 0.06f, 0.97f);
        static readonly Color Bone = new Color(0.87f, 0.84f, 0.76f);
        static readonly Color BoneDim = new Color(0.6f, 0.6f, 0.52f);
        static readonly Color Ember = new Color(0.79f, 0.58f, 0.23f);
        static readonly Color Plate = new Color(0.13f, 0.12f, 0.09f, 0.95f);

        [SerializeField] Sprite panelSprite;

        Canvas canvas;
        RectTransform root;
        RectTransform mainView;
        RectTransform slotView;
        RectTransform slotRows;

        void Awake()
        {
            Core.GameServices.EnsureCreated();
            Build();
        }

        public void Toggle() => SetOpen(!root.gameObject.activeSelf);

        void SetOpen(bool open)
        {
            if (open)
            {
                ShowMain();
                RefreshSlots();
            }
            root.gameObject.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            Controls.MobileControls.GameplayActive = !open;
            GameplayHud hud = FindAnyObjectByType<GameplayHud>();
            if (hud && hud.TryGetComponent(out Canvas hudCanvas))
                hudCanvas.enabled = !open;
            if (open)
                Core.GameServices.Audio.PlaySfx("ui_open", 0.8f, 0f);
        }

        void OnDestroy()
        {
            // If the panel dies while open (scene change), never leave the clock stopped.
            if (root && root.gameObject.activeSelf)
                Time.timeScale = 1f;
        }

        void ShowMain()
        {
            mainView.gameObject.SetActive(true);
            slotView.gameObject.SetActive(false);
        }

        void ShowSlots()
        {
            mainView.gameObject.SetActive(false);
            slotView.gameObject.SetActive(true);
            RefreshSlots();
        }

        void RefreshSlots()
        {
            foreach (Transform child in slotRows)
                Destroy(child.gameObject);
            var save = Core.GameServices.Save;
            for (int slot = 0; slot < Save.SaveService.SlotCount; slot++)
            {
                int captured = slot;
                Save.SaveData peek = save.Peek(slot);
                string title = Core.GameServices.Localization.Format("menu.slot", slot + 1);
                string meta = peek == null
                    ? Core.GameServices.Localization.Get("menu.slotEmpty")
                    : $"{peek.sceneName}  ·  {FormatWhen(peek.savedAtIso)}";
                AddSlotRow(title, meta, peek != null, () => SaveTo(captured));
            }
        }

        static string FormatWhen(string iso)
        {
            return DateTime.TryParse(iso, out DateTime when) ? when.ToLocalTime().ToString("d MMM · HH:mm") : "";
        }

        void SaveTo(int slot)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!Core.GameServices.Save.Save(slot, "manual", scene))
                return;
            Core.GameServices.Audio.PlaySfx("quest_new", 0.8f, 0f);
            Toast.ShowText(Core.GameServices.Localization.Format("menu.saved", slot + 1));
            RefreshSlots();
        }

        void ExitToTitle()
        {
            // Leaving by menu still leaves a trail: the run is written to autosave before the road fades.
            string scene = SceneManager.GetActiveScene().name;
            Core.GameServices.Save.Autosave("pause.title", scene);
            SetOpen(false);
            Core.GameServices.Ads?.ShowInterstitialIfReady();
            Core.GameServices.Flow.LoadScene(Core.GameBootstrap.TitleScene);
        }

        /// <summary>A benefactor's purse: the only coins the road gives for nothing but patience.</summary>
        void AskBenefactor()
        {
            Ads.AdsService ads = Core.GameServices.Ads;
            if (ads == null || !ads.GiftReady)
            {
                Toast.Show("toast.benefactorLater");
                return;
            }
            ads.ShowRewarded(ok =>
            {
                if (!ok)
                {
                    Toast.Show("toast.benefactorFailed");
                    return;
                }
                ads.NoteGiftTaken();
                Progress.PlayerState state = Progress.PlayerState.Instance;
                if (state != null)
                    state.Inventory.coin += BenefactorCoins;
                Core.GameServices.Audio.PlaySfx("coin", 0.8f, 0f);
                Toast.ShowText(Core.GameServices.Localization.Format("toast.benefactor", BenefactorCoins));
            });
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = NewRect("Root", transform);
            Stretch(root);

            var dim = NewImage("Dim", root, null, new Color(0.02f, 0.02f, 0.02f, 0.86f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => SetOpen(false));

            var sheet = NewImage("Sheet", root, panelSprite, new Color(0.12f, 0.12f, 0.09f, 0.98f));
            if (panelSprite)
                sheet.type = Image.Type.Sliced;
            sheet.raycastTarget = true;
            sheet.rectTransform.anchorMin = new Vector2(0.1f, 0.24f);
            sheet.rectTransform.anchorMax = new Vector2(0.9f, 0.76f);
            sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;

            var heading = NewLocalizedText("Heading", sheet.rectTransform, 54, Ember, "menu.title");
            heading.fontStyle = FontStyles.SmallCaps;
            heading.characterSpacing = 10f;
            heading.rectTransform.anchorMin = new Vector2(0f, 0.87f);
            heading.rectTransform.anchorMax = new Vector2(1f, 0.97f);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;

            mainView = NewRect("Main", sheet.rectTransform);
            mainView.anchorMin = new Vector2(0.1f, 0.08f);
            mainView.anchorMax = new Vector2(0.9f, 0.84f);
            mainView.offsetMin = mainView.offsetMax = Vector2.zero;
            var mainLayout = mainView.gameObject.AddComponent<VerticalLayoutGroup>();
            mainLayout.spacing = 20f;
            mainLayout.childControlHeight = true;
            mainLayout.childControlWidth = true;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childForceExpandWidth = true;

            AddButton(mainView, "menu.resume", () => SetOpen(false));
            AddButton(mainView, "menu.save", ShowSlots);
            AddButton(mainView, "menu.benefactor", AskBenefactor);
            AddButton(mainView, "menu.toTitle", ExitToTitle);
            AddButton(mainView, "menu.quit", QuitGame);

            slotView = NewRect("Slots", sheet.rectTransform);
            slotView.anchorMin = new Vector2(0.1f, 0.08f);
            slotView.anchorMax = new Vector2(0.9f, 0.84f);
            slotView.offsetMin = slotView.offsetMax = Vector2.zero;
            var slotLayout = slotView.gameObject.AddComponent<VerticalLayoutGroup>();
            slotLayout.spacing = 16f;
            slotLayout.childControlHeight = true;
            slotLayout.childControlWidth = true;
            slotLayout.childForceExpandHeight = false;
            slotLayout.childForceExpandWidth = true;

            var slotsHeading = NewLocalizedText("SlotsHeading", slotView, 40, Bone, "menu.saveTitle");
            slotsHeading.rectTransform.sizeDelta = new Vector2(0f, 56f);
            slotsHeading.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;

            slotRows = NewRect("Rows", slotView);
            var rowsLayout = slotRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 14f;
            rowsLayout.childControlHeight = true;
            rowsLayout.childControlWidth = true;
            rowsLayout.childForceExpandHeight = false;
            rowsLayout.childForceExpandWidth = true;
            slotRows.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            AddButton(slotView, "menu.back", ShowMain);
            slotView.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
        }

        void AddButton(RectTransform parent, string key, Action action)
        {
            var plate = NewImage(key, parent, null, Plate);
            plate.raycastTarget = true;
            plate.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            var button = plate.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.18f, 0.13f);
            colors.pressedColor = Ember;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Core.GameServices.Audio.PlaySfx("ui_tap", 0.7f, 0f);
                action();
            });
            var label = NewLocalizedText("Label", plate.rectTransform, 40, Bone, key);
            label.fontStyle = FontStyles.SmallCaps;
            label.characterSpacing = 6f;
        }

        void AddSlotRow(string title, string meta, bool filled, Action action)
        {
            var plate = NewImage("Slot", slotRows, null, Plate);
            plate.raycastTarget = true;
            plate.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
            var button = plate.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = Ember;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Core.GameServices.Audio.PlaySfx("ui_tap", 0.7f, 0f);
                action();
            });

            var name = NewText("Name", plate.rectTransform, 38, Bone);
            name.text = title;
            name.fontStyle = FontStyles.SmallCaps;
            name.characterSpacing = 4f;
            name.alignment = TextAlignmentOptions.Left;
            name.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
            name.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var info = NewText("Meta", plate.rectTransform, 28, filled ? BoneDim : new Color(0.4f, 0.4f, 0.35f));
            info.text = meta;
            info.alignment = TextAlignmentOptions.Left;
            info.rectTransform.anchorMin = new Vector2(0.05f, 0.05f);
            info.rectTransform.anchorMax = new Vector2(0.95f, 0.5f);
            info.rectTransform.offsetMin = info.rectTransform.offsetMax = Vector2.zero;
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
            Stretch(text.rectTransform);
            return text;
        }

        static TMP_Text NewLocalizedText(string name, Transform parent, float size, Color color, string key)
        {
            TMP_Text text = NewText(name, parent, size, color);
            text.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
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
