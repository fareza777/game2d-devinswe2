using SmallScale.FantasyKingdomTileset;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.UI
{
    /// <summary>
    /// The in-world HUD: health and mana bars, the active objective, night progress, and the Use button
    /// that acts on whatever the player is standing next to.
    /// </summary>
    public class GameplayHud : MonoBehaviour
    {
        static readonly Color Bone = new Color(0.87f, 0.84f, 0.76f);
        static readonly Color Ember = new Color(0.79f, 0.58f, 0.23f);

        [SerializeField] Sprite barFrame;
        [SerializeField] Sprite buttonRing;
        [SerializeField] Controls.MobileUiSkin skin = new Controls.MobileUiSkin();
        [SerializeField] Sprite packIcon;
        [SerializeField] Sprite bookIcon;

        // The bar frame's groove, measured in the sprite's own pixels (UISprites_17, 362x71): the fill must sit
        // exactly inside it. Left/right 19, bottom 23, top 16; the rest is bevel and drop shadow.
        static readonly Vector4 GrooveInset = new Vector4(19f, 23f, 19f, 16f);
        static readonly Vector4 FrameSlices = new Vector4(26f, 28f, 26f, 20f);
        const float DrainSpeed = 0.45f;

        RectTransform healthFill;
        RectTransform healthDrain;
        RectTransform manaFill;
        float shownHealth = 1f;
        TMP_Text healthLabel;
        TMP_Text objectiveLabel;
        TMP_Text nightLabel;
        TMP_Text eventLabel;
        TMP_Text promptLabel;
        GameObject useButton;
        RectTransform experienceFill;
        TMP_Text levelLabel;
        TMP_Text levelUpTitle;
        TMP_Text levelUpDetail;
        CanvasGroup levelUpGroup;
        TMP_Text placeLabel;
        CanvasGroup placeGroup;
        RectTransform floatLayer;
        float shownExperience;
        Coroutine levelUpRoutine;

        /// <summary>The HUD of the scene being played; null on menus and in cinematics.</summary>
        public static GameplayHud Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            Core.GameServices.EnsureCreated();
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start() => StartCoroutine(ShowPlaceName());

        void Update()
        {
            PlayerHealth health = PlayerHealth.Instance;
            if (health)
            {
                float ratio = Mathf.Clamp01(health.currentHealth / (float)Mathf.Max(1, health.maxHealth));
                healthFill.anchorMax = new Vector2(ratio, 1f);
                // A pale trail shows what the last hit took, then drains down to the new value.
                shownHealth = ratio >= shownHealth ? ratio : Mathf.MoveTowards(shownHealth, ratio, Time.deltaTime * DrainSpeed);
                healthDrain.anchorMax = new Vector2(shownHealth, 1f);
                healthLabel.text = $"{Mathf.Max(0, health.currentHealth)} / {health.maxHealth}";
            }

            PlayerMana mana = PlayerMana.Instance;
            if (mana)
                manaFill.anchorMax = new Vector2(Mathf.Clamp01(mana.CurrentMana / Mathf.Max(1f, mana.MaxMana)), 1f);

            UpdateExperience();
            UpdateObjective();
            UpdateNight();
            UpdateEvent();

            World.WorldInteractable focused = World.WorldInteractable.Focused;
            bool canUse = focused && !Dialogue.DialogueRunner.IsConversationActive;
            useButton.SetActive(canUse);
            promptLabel.gameObject.SetActive(canUse);
            if (canUse)
                promptLabel.text = Core.GameServices.Localization.Get(focused.PromptKey);
        }

        void UpdateExperience()
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            if (state == null)
                return;
            bool capped = state.Level >= Progress.PlayerState.MaxLevel;
            float ratio = capped ? 1f : Mathf.Clamp01(state.Experience / (float)Mathf.Max(1, state.ExperienceForNextLevel));
            // The bar fills smoothly, and wraps round to empty when a level is gained rather than draining back.
            shownExperience = ratio < shownExperience - 0.02f ? ratio : Mathf.MoveTowards(shownExperience, ratio, Time.deltaTime * 0.8f);
            experienceFill.anchorMax = new Vector2(shownExperience, 1f);
            levelLabel.text = Core.GameServices.Localization.Format("hud.level", state.Level);
        }

        /// <summary>A small number that rises off a fallen enemy and fades: what the fight just taught.</summary>
        public void FloatText(Vector3 worldPosition, string text, Color? colour = null)
        {
            Camera camera = Camera.main;
            if (!camera || !floatLayer)
                return;
            Vector3 screen = camera.WorldToScreenPoint(worldPosition + Vector3.up * 0.9f);
            if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(floatLayer, screen, null, out Vector2 local))
                return;
            TMP_Text label = NewText("Float", floatLayer, 40, colour ?? new Color(0.96f, 0.82f, 0.42f), heading: true, letterSpacing: 2f);
            label.text = text;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 70f);
            rect.anchoredPosition = local;
            StartCoroutine(RiseAndFade(label, local));
        }

        System.Collections.IEnumerator RiseAndFade(TMP_Text label, Vector2 from)
        {
            const float Seconds = 1.3f;
            for (float t = 0f; t < Seconds && label; t += Time.deltaTime)
            {
                float k = t / Seconds;
                label.rectTransform.anchoredPosition = from + Vector2.up * (140f * Mathf.Sqrt(k));
                label.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                yield return null;
            }
            if (label)
                Destroy(label.gameObject);
        }

        /// <summary>The moment a level is gained: the number, what it gave, and a skill if one just opened.</summary>
        public void ShowLevelUp(int level)
        {
            levelUpTitle.text = Core.GameServices.Localization.Format("hud.levelUp", level);
            string detail = Core.GameServices.Localization.Format("hud.levelUpGains",
                Progress.PlayerState.HealthPerLevel, Progress.PlayerState.DamagePerLevel);
            foreach (Progress.SkillBook.Entry skill in Progress.SkillBook.LearnedAt(level))
                detail += "\n<color=#e8b04a>" + Core.GameServices.Localization.Format("hud.skillUnlocked",
                    Core.GameServices.Localization.Get(skill.NameKey)) + "</color>";
            levelUpDetail.text = detail;
            if (levelUpRoutine != null)
                StopCoroutine(levelUpRoutine);
            levelUpRoutine = StartCoroutine(LevelUpRoutine());
        }

        /// <summary>True while the level banner is on screen; read by the smoke test.</summary>
        public bool ShowingLevelUp => levelUpGroup && levelUpGroup.alpha > 0.05f;

        System.Collections.IEnumerator LevelUpRoutine()
        {
            const float Seconds = 3.2f;
            RectTransform rect = (RectTransform)levelUpGroup.transform;
            for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / Seconds;
                levelUpGroup.alpha = k < 0.1f ? k / 0.1f : k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
                rect.localScale = Vector3.one * (k < 0.1f ? Mathf.Lerp(1.35f, 1f, k / 0.1f) : 1f);
                yield return null;
            }
            levelUpGroup.alpha = 0f;
            levelUpRoutine = null;
        }

        /// <summary>Arriving somewhere says where: the name of the place, large, then gone.</summary>
        System.Collections.IEnumerator ShowPlaceName()
        {
            string key = $"place.{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
            string name = Core.GameServices.Localization.Get(key);
            if (string.IsNullOrEmpty(name) || name == key)
                yield break;
            placeLabel.text = name;
            yield return new WaitForSeconds(0.8f);
            const float Seconds = 3.4f;
            for (float t = 0f; t < Seconds; t += Time.deltaTime)
            {
                float k = t / Seconds;
                placeGroup.alpha = k < 0.2f ? k / 0.2f : k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                yield return null;
            }
            placeGroup.alpha = 0f;
        }

        /// <summary>True while the name of the place is on screen; read by the smoke test.</summary>
        public bool ShowingPlaceName => placeGroup && placeGroup.alpha > 0.05f;

        void UpdateObjective()
        {
            Quests.QuestDefinition quest = Quests.QuestRuntime.FocusedQuest();
            if (quest == null)
            {
                objectiveLabel.text = string.Empty;
                return;
            }
            Quests.QuestStage stage = Quests.QuestRuntime.Service.CurrentStage(quest);
            if (stage == null)
            {
                objectiveLabel.text = string.Empty;
                return;
            }

            string title = Core.GameServices.Localization.Get(quest.TitleKey);
            bool handIn = Quests.QuestRuntime.Service.IsReadyToHandIn(quest);
            string step = Quests.QuestRuntime.StepText(quest);
            string colour = quest.mainQuest ? "#c9933b" : "#7fa89a";
            objectiveLabel.text = $"<color={colour}>{title}</color>\n{step}{(handIn ? string.Empty : ProgressSuffix(stage))}";
        }

        static string ProgressSuffix(Quests.QuestStage stage)
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
                return $"  <color=#5e8b83>{Mathf.Min(have, objective.amount)}/{objective.amount}</color>";
            }
            return string.Empty;
        }

        void UpdateNight()
        {
            World.NightDirector night = World.NightDirector.Instance;
            bool running = night && night.IsNightRunning;
            nightLabel.gameObject.SetActive(running);
            if (running)
                nightLabel.text = Core.GameServices.Localization.Format("hud.night", PushName(night), night.AliveCount);
        }

        /// <summary>
        /// What the settlement would call this part of the night. "Wave 2 of 3" is a spreadsheet; the people
        /// standing in the dark would say the second push, and the last one they would call the last one.
        /// </summary>
        static string PushName(World.NightDirector night)
        {
            if (night.WaveNumber >= night.TotalWaves)
                return Core.GameServices.Localization.Get("night.push.last");
            int index = Mathf.Clamp(night.WaveNumber, 1, 4);
            return Core.GameServices.Localization.Get($"night.push.{index}");
        }

        /// <summary>Whoever has just walked in out of the dark, and how long they will wait.</summary>
        void UpdateEvent()
        {
            string banner = World.WorldEventDirector.Banner;
            eventLabel.gameObject.SetActive(!string.IsNullOrEmpty(banner));
            eventLabel.text = banner;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            healthFill = BuildBar("Health", new Vector2(34f, -44f), new Vector2(600f, 88f), new Color(0.74f, 0.16f, 0.12f),
                out healthLabel, out healthDrain);
            manaFill = BuildBar("Mana", new Vector2(34f, -128f), new Vector2(520f, 70f), new Color(0.2f, 0.45f, 0.86f),
                out _, out _);
            BuildExperience();

            objectiveLabel = NewText("Objective", transform, 34, Bone, heading: true, letterSpacing: 2f);
            objectiveLabel.alignment = TextAlignmentOptions.TopLeft;
            RectTransform objectiveRect = objectiveLabel.rectTransform;
            objectiveRect.anchorMin = new Vector2(0f, 1f);
            objectiveRect.anchorMax = new Vector2(1f, 1f);
            objectiveRect.pivot = new Vector2(0f, 1f);
            objectiveRect.anchoredPosition = new Vector2(40f, -236f);
            objectiveRect.sizeDelta = new Vector2(-300f, 150f);

            nightLabel = NewText("Night", transform, 40, Ember, heading: true, letterSpacing: 4f);
            RectTransform nightRect = nightLabel.rectTransform;
            nightRect.anchorMin = new Vector2(0.5f, 1f);
            nightRect.anchorMax = new Vector2(0.5f, 1f);
            nightRect.pivot = new Vector2(0.5f, 1f);
            // Below the objective and clear of the PACK/BOOK column on the right.
            nightRect.anchoredPosition = new Vector2(-80f, -395f);
            nightRect.sizeDelta = new Vector2(760f, 90f);
            nightLabel.enableAutoSizing = true;
            nightLabel.fontSizeMin = 26f;
            nightLabel.fontSizeMax = 40f;
            nightLabel.gameObject.SetActive(false);

            eventLabel = NewText("Event", transform, 36, Bone, heading: true, letterSpacing: 3f);
            RectTransform eventRect = eventLabel.rectTransform;
            eventRect.anchorMin = new Vector2(0.5f, 1f);
            eventRect.anchorMax = new Vector2(0.5f, 1f);
            eventRect.pivot = new Vector2(0.5f, 1f);
            eventRect.anchoredPosition = new Vector2(-80f, -495f);
            eventRect.sizeDelta = new Vector2(760f, 120f);
            eventLabel.enableAutoSizing = true;
            eventLabel.fontSizeMin = 24f;
            eventLabel.fontSizeMax = 36f;
            eventLabel.gameObject.SetActive(false);

            BuildUseButton();
            BuildPackButton();
            BuildBookButton();
            BuildLevelUpBanner();
            BuildPlaceName();

            floatLayer = new GameObject("FloatingText", typeof(RectTransform)).GetComponent<RectTransform>();
            floatLayer.SetParent(transform, false);
            Stretch(floatLayer);
        }

        /// <summary>
        /// A thin gilt line under the mana bar that fills with experience, and the level engraved beside the
        /// mana bar, so progress is always on screen rather than two taps away in the book.
        /// </summary>
        void BuildExperience()
        {
            var bar = new GameObject("Experience", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(transform, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(52f, -202f);
            bar.sizeDelta = new Vector2(484f, 18f);

            Image back = NewImage("Back", bar, null, new Color(0.05f, 0.05f, 0.04f, 0.85f));
            Stretch(back.rectTransform);
            var groove = new GameObject("Groove", typeof(RectTransform)).GetComponent<RectTransform>();
            groove.SetParent(bar, false);
            Stretch(groove);
            groove.offsetMin = new Vector2(3f, 3f);
            groove.offsetMax = new Vector2(-3f, -3f);
            Image fill = NewImage("Fill", groove, FillShading(), new Color(0.86f, 0.66f, 0.26f));
            experienceFill = fill.rectTransform;
            FillFromLeft(experienceFill);
            experienceFill.anchorMax = new Vector2(0f, 1f);

            levelLabel = NewText("Level", transform, 38, new Color(0.96f, 0.84f, 0.5f), heading: true, letterSpacing: 2f);
            RectTransform levelRect = levelLabel.rectTransform;
            levelRect.anchorMin = levelRect.anchorMax = new Vector2(0f, 1f);
            levelRect.pivot = new Vector2(0f, 1f);
            levelRect.anchoredPosition = new Vector2(566f, -134f);
            levelRect.sizeDelta = new Vector2(170f, 60f);
            levelLabel.alignment = TextAlignmentOptions.Left;
        }

        void BuildLevelUpBanner()
        {
            var root = new GameObject("LevelUp", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(900f, 280f);
            levelUpGroup = root.GetComponent<CanvasGroup>();
            levelUpGroup.alpha = 0f;
            levelUpGroup.blocksRaycasts = false;

            Image shade = NewImage("Shade", root, null, new Color(0.03f, 0.03f, 0.02f, 0.62f));
            Stretch(shade.rectTransform);

            levelUpTitle = NewText("Title", root, 76, new Color(0.98f, 0.8f, 0.4f), heading: true, letterSpacing: 8f);
            levelUpTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            levelUpTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            levelUpDetail = NewText("Detail", root, 34, Bone);
            levelUpDetail.rectTransform.anchorMin = new Vector2(0.04f, 0.04f);
            levelUpDetail.rectTransform.anchorMax = new Vector2(0.96f, 0.52f);
            levelUpDetail.enableAutoSizing = true;
            levelUpDetail.fontSizeMin = 24f;
            levelUpDetail.fontSizeMax = 34f;
        }

        void BuildPlaceName()
        {
            var root = new GameObject("PlaceName", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            // Between the objective block and the thumbs: clear of the text, the menu medallions and the controls.
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.6f);
            root.sizeDelta = new Vector2(860f, 140f);
            placeGroup = root.GetComponent<CanvasGroup>();
            placeGroup.alpha = 0f;
            placeGroup.blocksRaycasts = false;
            Image band = NewImage("Band", root, null, new Color(0.03f, 0.03f, 0.02f, 0.5f));
            Stretch(band.rectTransform);
            placeLabel = NewText("Name", root, 64, Bone, heading: true, letterSpacing: 10f);
            placeLabel.enableAutoSizing = true;
            placeLabel.fontSizeMin = 40f;
            placeLabel.fontSizeMax = 64f;
            placeLabel.rectTransform.offsetMin = new Vector2(30f, 0f);
            placeLabel.rectTransform.offsetMax = new Vector2(-30f, 0f);
        }

        void BuildPackButton() =>
            BuildMenuMedallion("PackButton", packIcon, -40f, "hud.pack",
                () => FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include)?.Toggle());

        /// <summary>Opens the Warden's book: stats, gear, standing and deeds.</summary>
        void BuildBookButton() =>
            BuildMenuMedallion("BookButton", bookIcon, -250f, "hud.hero",
                () => FindAnyObjectByType<HeroPanel>(FindObjectsInactive.Include)?.Toggle());

        /// <summary>
        /// A round menu button in the same bronze-ring style as the combat controls, with its picture inside and
        /// its name engraved underneath, so nothing is printed over the ring.
        /// </summary>
        void BuildMenuMedallion(string name, Sprite icon, float top, string captionKey, System.Action open)
        {
            const float Size = 138f;
            // A warm leather plate: the pack's stone-grey icons disappear on the near-black one used for skills.
            Controls.MedallionButton button = Controls.MedallionButton.Create(name, transform, skin, icon, Size, iconScale: 0.74f,
                plateTint: new Color(0.47f, 0.36f, 0.24f, 1f));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36f, top);
            button.Released += () =>
            {
                Core.GameServices.Audio.PlaySfx("ui_open", 0.8f, 0f);
                open();
            };

            TMP_Text caption = NewText("Caption", transform, 24, Bone, heading: true, letterSpacing: 4f);
            caption.gameObject.AddComponent<Localization.LocalizedLabel>().Key = captionKey;
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.anchoredPosition = new Vector2(-36f - Size * 0.5f, top - Size - 2f);
            captionRect.sizeDelta = new Vector2(Size + 40f, 36f);
        }

        /// <summary>
        /// A vital bar: the pack's bevelled frame drawn nine-sliced, so its corners keep their shape at any
        /// length, and the fill placed exactly inside the frame's groove rather than over the bevel.
        /// </summary>
        RectTransform BuildBar(string name, Vector2 position, Vector2 size, Color fillColor, out TMP_Text valueLabel,
            out RectTransform drainFill)
        {
            var bar = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(transform, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = position;
            bar.sizeDelta = size;

            Image frame = NewImage("Frame", bar, SlicedFrame(), Color.white);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1f;
            Stretch(frame.rectTransform);

            var groove = new GameObject("Groove", typeof(RectTransform)).GetComponent<RectTransform>();
            groove.SetParent(bar, false);
            groove.anchorMin = Vector2.zero;
            groove.anchorMax = Vector2.one;
            groove.offsetMin = new Vector2(GrooveInset.x, GrooveInset.y);
            groove.offsetMax = new Vector2(-GrooveInset.z, -GrooveInset.w);

            Color pale = Color.Lerp(fillColor, Color.white, 0.55f);
            Image drain = NewImage("Drain", groove, null, new Color(pale.r, pale.g, pale.b, 0.75f));
            drainFill = drain.rectTransform;
            FillFromLeft(drainFill);

            Image fill = NewImage("Fill", groove, FillShading(), fillColor);
            RectTransform fillRect = fill.rectTransform;
            FillFromLeft(fillRect);

            valueLabel = NewText("Value", groove, size.y * 0.34f, Bone);
            valueLabel.fontStyle = FontStyles.Bold;
            return fillRect;
        }

        static void FillFromLeft(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
        }

        Sprite slicedFrame;

        /// <summary>The frame sprite again, with nine-slice borders; the pack's own import has none.</summary>
        Sprite SlicedFrame()
        {
            if (slicedFrame)
                return slicedFrame;
            if (!barFrame)
                return null;
            slicedFrame = Sprite.Create(barFrame.texture, barFrame.textureRect, new Vector2(0.5f, 0.5f),
                barFrame.pixelsPerUnit, 0, SpriteMeshType.FullRect, FrameSlices);
            return slicedFrame;
        }

        static Sprite fillShading;

        /// <summary>Soft top highlight and darker base, so a fill reads as liquid in a glass rather than flat paint.</summary>
        static Sprite FillShading()
        {
            if (fillShading)
                return fillShading;
            const int Height = 32;
            var texture = new Texture2D(1, Height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < Height; y++)
            {
                float k = y / (Height - 1f);
                float shade = Mathf.Min(1f, Mathf.Lerp(0.62f, 1f, k) + (k > 0.72f && k < 0.86f ? 0.22f : 0f));
                texture.SetPixel(0, y, new Color(shade, shade, shade, 1f));
            }
            texture.Apply();
            fillShading = Sprite.Create(texture, new Rect(0, 0, 1, Height), new Vector2(0.5f, 0.5f), 100f);
            return fillShading;
        }

        void BuildUseButton()
        {
            const float Size = 190f;
            Controls.MedallionButton button = Controls.MedallionButton.Create("UseButton", transform, skin, skin.glow, Size, iconScale: 0.95f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-70f, 620f);
            button.Released += () => World.WorldInteractable.Focused?.Use();
            Transform glowIcon = rect.Find("Visual/Plate/Icon");
            if (glowIcon)
                glowIcon.GetComponent<Image>().color = new Color(0.95f, 0.62f, 0.22f, 0.85f);

            TMP_Text label = NewText("Label", rect, 40, new Color(1f, 0.95f, 0.85f), heading: true, letterSpacing: 3f);
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = "hud.use";

            useButton = button.gameObject;
            useButton.SetActive(false);

            // What the Use button will do, written once, just left of the button.
            promptLabel = NewText("Prompt", transform, 34, Bone);
            promptLabel.alignment = TextAlignmentOptions.Right;
            RectTransform promptRect = promptLabel.rectTransform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(1f, 0f);
            promptRect.pivot = new Vector2(1f, 0f);
            promptRect.anchoredPosition = new Vector2(-280f, 650f);
            promptRect.sizeDelta = new Vector2(600f, 120f);
            promptLabel.enableAutoSizing = true;
            promptLabel.fontSizeMin = 24f;
            promptLabel.fontSizeMax = 34f;
            promptLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// A UI image. Round buttons keep their aspect; a bar frame must not, or it shrinks to its sprite's
        /// proportions while the fill behind it still uses the full width and spills out past the frame.
        /// </summary>
        static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool preserveAspect = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = sprite && preserveAspect;
            return image;
        }

        /// <summary>
        /// A HUD label. The typeface is chosen here, before the outline is set: an outline instances the
        /// label's material, and a material instanced under the old typeface keeps sampling the old atlas,
        /// which draws real letters that spell nothing.
        /// </summary>
        static TMP_Text NewText(string name, Transform parent, float size, Color color,
            bool heading = false, float letterSpacing = 6f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();

            if (heading)
                text.AsHeading(letterSpacing);
            else
                text.AsBody();

            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
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
