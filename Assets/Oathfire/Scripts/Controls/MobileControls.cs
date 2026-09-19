using SmallScale.FantasyKingdomTileset;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.Controls
{
    /// <summary>
    /// Touch controls for phones: a medallion joystick (left), a large attack medallion and two skill
    /// medallions (right), with auto-aim at the nearest enemy. Feeds the asset's InputAdapter so the existing
    /// player controller and melee work unchanged; skills go straight to the AbilityRunner.
    /// Layout adapts to portrait or landscape. Also usable with a mouse for testing.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        const int SortingOrder = 400; // below SessionHud so result screens stay on top
        const float EdgeMargin = 70f;

        /// <summary>Controls hide themselves whenever gameplay is paused: dialogue, menus, cutscenes.</summary>
        public static bool GameplayActive { get; set; } = true;

        [SerializeField] MobileUiSkin skin = new MobileUiSkin();
        [SerializeField] AbilityDefinition dashAbility;
        [Tooltip("Every skill the Warden can learn, in the order they open; filled from Docs/skills.json.")]
        [SerializeField] Progress.SkillBook.Entry[] skills;
        [SerializeField, Min(0.5f)] float autoAimRange = 5f;

        [Header("Layout (reference pixels)")]
        [SerializeField, Min(40f)] float joystickRadius = 130f;
        [SerializeField, Min(80f)] float attackButtonSize = 270f;
        [SerializeField, Min(60f)] float skillButtonSize = 165f;

        GameObject controlsRoot;
        FloatingJoystick joystick;
        bool leftHanded;
        MedallionButton dashButton;
        readonly MedallionButton[] slotButtons = new MedallionButton[Progress.SkillBook.Slots];
        float nextSlotCheck;
        Vector2 aimDirection = Vector2.down;
        Transform aimTarget;

        void Awake()
        {
            if (!skin.IsComplete)
                Debug.LogError("[MobileControls] UI skin sprites are missing; controls will render without art.", this);
            Progress.SkillBook.Register(skills);

            BuildUi();
            InputAdapter.VirtualControlsActive = true;
            Application.targetFrameRate = 60;           // Android defaults to 30fps
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void OnDestroy()
        {
            InputAdapter.VirtualControlsActive = false;
            InputAdapter.ReleaseAllVirtualKeys();
        }

        void Update()
        {
            bool playing = GameplayActive && !PlayerHealth.IsPlayerDead && !Dialogue.DialogueRunner.IsConversationActive;
            if (controlsRoot.activeSelf != playing)
            {
                controlsRoot.SetActive(playing);
                if (!playing)
                    InputAdapter.ReleaseAllVirtualKeys();
            }

            InputAdapter.VirtualMove = playing ? joystick.Value : Vector2.zero;
            UpdateAim();
            if (playing)
            {
                RefreshSkill(dashButton, dashAbility);
                RefreshSlots();
            }
        }

        /// <summary>
        /// Each slot button shows the skill carried in it and hides while the slot is empty. Until the first skill
        /// is learned the right thumb has the sword and the dodge, which is all the first fights ask for.
        /// </summary>
        void RefreshSlots()
        {
            if (Time.unscaledTime >= nextSlotCheck)
            {
                nextSlotCheck = Time.unscaledTime + 0.5f;
                Progress.SkillBook.FillEmptySlots();
            }
            for (int slot = 0; slot < slotButtons.Length; slot++)
            {
                MedallionButton button = slotButtons[slot];
                if (!button)
                    continue;
                Progress.SkillBook.Entry entry = Progress.SkillBook.InSlot(slot);
                bool carried = entry != null && entry.ability;
                if (button.gameObject.activeSelf != carried)
                    button.gameObject.SetActive(carried);
                if (!carried)
                    continue;
                button.SetIcon(entry.ability.Icon);
                RefreshSkill(button, entry.ability);
            }
        }

        /// <summary>The skill in a slot, if any; read by the smoke test.</summary>
        public MedallionButton SlotButton(int slot) => slot >= 0 && slot < slotButtons.Length ? slotButtons[slot] : null;

        void UpdateAim()
        {
            var player = PlayerHealth.Instance;
            var cam = Camera.main;
            if (!player || !cam)
                return;

            Vector2 playerPos = player.transform.position;
            if (InputAdapter.VirtualMove.sqrMagnitude > 0.01f)
                aimDirection = InputAdapter.VirtualMove.normalized;

            aimTarget = FindNearestEnemy(playerPos);
            Vector2 aimWorld = aimTarget ? (Vector2)aimTarget.position : playerPos + aimDirection * 2f;
            InputAdapter.VirtualPointerScreen = cam.WorldToScreenPoint(aimWorld);
        }

        Transform FindNearestEnemy(Vector2 from)
        {
            Transform best = null;
            float bestDistance = autoAimRange;
            foreach (var enemy in EnemyHealth2D.All)
            {
                if (!enemy || enemy.IsDead)
                    continue;
                float distance = Vector2.Distance(from, enemy.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy.transform;
                }
            }
            return best;
        }

        void ActivateSkill(MedallionButton button, AbilityDefinition ability, bool aimAtEnemy)
        {
            var runner = PlayerHealth.Instance ? PlayerHealth.Instance.GetComponent<AbilityRunner>() : null;
            if (!runner || !ability)
                return;

            Vector2 direction = aimDirection;
            if (aimAtEnemy && aimTarget)
                direction = ((Vector2)aimTarget.position - (Vector2)runner.transform.position).normalized;

            var parameters = new AbilityActivationParameters
            {
                DesiredDirection = direction,
                Target = aimAtEnemy ? aimTarget : null,
            };
            if (runner.TryActivateAbility(ability, parameters))
                Core.GameServices.Audio.PlaySfx(ability == dashAbility ? "skill_dash" : "skill_area");
            else
            {
                button.FlashDenied();
                Core.GameServices.Audio.PlaySfx("denied", 0.7f, 0f);
            }
        }

        void RefreshSkill(MedallionButton button, AbilityDefinition ability)
        {
            if (!button || !ability)
                return;
            var runner = PlayerHealth.Instance ? PlayerHealth.Instance.GetComponent<AbilityRunner>() : null;
            var state = runner ? runner.GetState(ability) : null;
            float fill = state != null ? state.GetCooldownFillAmount() : 0f;
            button.SetCooldown(fill, fill * ability.CooldownSeconds);
            button.SetUsable(!PlayerMana.Instance || PlayerMana.Instance.HasMana(ability.ManaCost));
        }

        void BuildUi()
        {
            bool portrait = Screen.height >= Screen.width;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0f : 1f; // size buttons by the short screen side
            gameObject.AddComponent<GraphicRaycaster>();

            controlsRoot = new GameObject("Controls", typeof(RectTransform));
            controlsRoot.transform.SetParent(transform, false);
            StretchFull((RectTransform)controlsRoot.transform);

            // Left-handed players get the joystick under the right thumb and the attack under the left.
            leftHanded = Core.GameServices.Settings.LeftHanded;
            float referenceWidth = portrait ? 1080f : 1920f;

            var zone = new GameObject("JoystickZone", typeof(RectTransform)).GetComponent<RectTransform>();
            zone.SetParent(controlsRoot.transform, false);
            zone.anchorMin = new Vector2(leftHanded ? 0.5f : 0f, 0f);
            zone.anchorMax = new Vector2(leftHanded ? 1f : 0.5f, portrait ? 0.45f : 0.75f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            float joystickInset = EdgeMargin + joystickRadius * 1.15f;
            // The joystick rests measured from the zone's bottom-left, so on the right half it rests at the far edge.
            float restX = leftHanded ? referenceWidth * 0.5f - joystickInset : joystickInset;
            joystick = FloatingJoystick.Create(zone, skin, joystickRadius, new Vector2(restX, joystickInset + 40f));

            float attackInset = EdgeMargin + attackButtonSize * 0.5f;
            var attack = MedallionButton.Create("AttackButton", controlsRoot.transform, skin, skin.attackIcon, attackButtonSize);
            PlaceBottomCorner(attack, new Vector2(attackInset, attackInset + 20f));
            attack.Pressed += () =>
            {
                InputAdapter.SetVirtualKey(KeyCode.Mouse0, true);
                Core.GameServices.Audio.PlaySfx("sword_swing", 0.85f, 0.12f);
            };
            attack.Released += () => InputAdapter.SetVirtualKey(KeyCode.Mouse0, false);

            // Skills sit on an arc around the attack button, reachable by rolling the right thumb.
            float arc = attackButtonSize * 0.5f + skillButtonSize * 0.5f + 25f;
            dashButton = CreateSkillButton("DashButton", dashAbility, attackInset, arc, 180f, aimAtEnemy: false);
            float[] slotAngles = { 128f, 76f };
            for (int slot = 0; slot < slotButtons.Length; slot++)
                slotButtons[slot] = CreateSlotButton(slot, attackInset, arc, slotAngles[slot]);

            controlsRoot.SetActive(false);
        }

        MedallionButton CreateSkillButton(string name, AbilityDefinition ability, float attackInset, float arcRadius, float angleDegrees, bool aimAtEnemy)
        {
            if (!ability)
                return null;
            var button = MedallionButton.Create(name, controlsRoot.transform, skin, ability.Icon, skillButtonSize);
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * arcRadius;
            // Offsets are measured inward from the button corner, so the arc mirrors with the layout.
            PlaceBottomCorner(button, new Vector2(attackInset - offset.x, attackInset + 20f + offset.y));
            button.Pressed += () => ActivateSkill(button, ability, aimAtEnemy);
            return button;
        }

        /// <summary>A button whose skill is whatever the book has in that slot at the moment it is pressed.</summary>
        MedallionButton CreateSlotButton(int slot, float attackInset, float arcRadius, float angleDegrees)
        {
            var button = MedallionButton.Create($"SkillSlot{slot + 1}", controlsRoot.transform, skin, null, skillButtonSize);
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * arcRadius;
            PlaceBottomCorner(button, new Vector2(attackInset - offset.x, attackInset + 20f + offset.y));
            button.Pressed += () =>
            {
                Progress.SkillBook.Entry entry = Progress.SkillBook.InSlot(slot);
                if (entry != null && entry.ability)
                    ActivateSkill(button, entry.ability, aimAtEnemy: true);
            };
            button.gameObject.SetActive(false);
            return button;
        }

        /// <summary>Anchors a button in the bottom corner on the action side of the screen.</summary>
        void PlaceBottomCorner(MedallionButton button, Vector2 insetFromCorner)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(leftHanded ? 0f : 1f, 0f);
            rect.anchoredPosition = new Vector2(leftHanded ? insetFromCorner.x : -insetFromCorner.x, insetFromCorner.y);
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
