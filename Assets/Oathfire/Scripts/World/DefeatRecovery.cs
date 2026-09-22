using System.Collections;
using SmallScale.FantasyKingdomTileset;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oathfire.World
{
    /// <summary>
    /// What happens when the Warden falls. The pack's own answer is a respawn panel this game never built, so a
    /// death simply ended play: the body lay there and nothing could be touched. Now the screen goes to ink,
    /// the Warden wakes where they last arrived (by the fire, if there is one) with a few coins gone, and the
    /// fight is still there to be won.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class DefeatRecovery : MonoBehaviour
    {
        const float LieSeconds = 2.2f;
        const float CoinLost = 0.1f;

        PlayerHealth health;
        bool recovering;

        /// <summary>How many times the Warden has been brought back this session; read by the smoke test.</summary>
        public static int Recoveries { get; private set; }

        void Awake() => health = GetComponent<PlayerHealth>();
        void OnEnable() => PlayerHealth.OnPlayerDied += OnDied;
        void OnDisable() => PlayerHealth.OnPlayerDied -= OnDied;

        void OnDied()
        {
            if (!recovering && isActiveAndEnabled)
                StartCoroutine(Recover());
        }

        IEnumerator Recover()
        {
            recovering = true;
            yield return new WaitForSeconds(LieSeconds);

            Core.SceneFlow flow = Core.GameServices.Flow;
            if (flow)
                yield return flow.FadeRoutine(1f, 0.6f);

            if (OathfireHearth.Instance && OathfireHearth.Instance.IsLit)
            {
                Vector2 fire = OathfireHearth.Instance.transform.position;
                health.SetSpawnPoint(SpawnPlanner.FindOpenPoint(fire, fire, 1.2f, 2.2f), transform.rotation);
            }

            Progress.PlayerState state = Progress.PlayerState.Instance;
            int lost = state != null ? Mathf.FloorToInt(state.Inventory.coin * CoinLost) : 0;

            // The Benefactor may spare the toll: one opt-in reward keeps the purse whole.
            if (lost > 0)
                yield return BenefactorChoice(lost, keepCoins => { if (!keepCoins) state.Inventory.coin -= lost; });

            health.RespawnAtSpawn(health.maxHealth);
            Camera camera = Camera.main;
            if (camera)
                camera.transform.position = new Vector3(transform.position.x, transform.position.y, camera.transform.position.z);
            Recoveries++;

            if (flow)
                yield return flow.FadeRoutine(0f, 0.6f);
            UI.Toast.ShowText(lost > 0
                ? Core.GameServices.Localization.Format("toast.recoveredCoin", lost)
                : Core.GameServices.Localization.Get("toast.recovered"));
            recovering = false;
        }

        /// <summary>
        /// While the screen is inked, the Benefactor's offer: watch a short tale and keep the coins,
        /// or wake and pay the toll. No offer appears at all when no reward can be shown.
        /// </summary>
        IEnumerator BenefactorChoice(int lost, System.Action<bool> resolve)
        {
            Ads.AdsService ads = Core.GameServices.Ads;
            if (ads == null || !ads.RewardedReady)
            {
                resolve(false);
                yield break;
            }

            GameObject canvasGo = new GameObject("DefeatBenefactor", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;

            var sheet = NewImage("Sheet", canvas.transform, new Color(0.12f, 0.12f, 0.09f, 0.98f));
            sheet.rectTransform.anchorMin = new Vector2(0.12f, 0.34f);
            sheet.rectTransform.anchorMax = new Vector2(0.88f, 0.56f);
            sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;

            var title = NewText("Title", sheet.transform, 44, new Color(0.79f, 0.58f, 0.23f));
            title.fontStyle = FontStyles.SmallCaps;
            title.text = Core.GameServices.Localization.Get("defeat.benefactor");
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.72f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            var body = NewText("Body", sheet.transform, 34, new Color(0.87f, 0.84f, 0.76f));
            body.text = Core.GameServices.Localization.Format("defeat.offer", lost);
            body.rectTransform.anchorMin = new Vector2(0.08f, 0.42f);
            body.rectTransform.anchorMax = new Vector2(0.92f, 0.72f);
            body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;

            bool decided = false;
            var watch = NewButton("Watch", sheet.transform, "defeat.watch", new Vector2(0.08f, 0.08f), new Vector2(0.55f, 0.34f));
            watch.onClick.AddListener(() =>
            {
                Core.GameServices.Audio.PlaySfx("ui_tap", 0.7f, 0f);
                ads.ShowRewarded(ok =>
                {
                    decided = true;
                    resolve(ok);
                });
            });
            var wake = NewButton("Wake", sheet.transform, "defeat.wake", new Vector2(0.58f, 0.08f), new Vector2(0.95f, 0.34f));
            wake.onClick.AddListener(() =>
            {
                Core.GameServices.Audio.PlaySfx("ui_tap", 0.7f, 0f);
                decided = true;
                resolve(false);
            });

            while (!decided)
                yield return null;
            Destroy(canvasGo);
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
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
            return text;
        }

        static Button NewButton(string name, Transform parent, string key, Vector2 min, Vector2 max)
        {
            var plate = NewImage(name, parent, new Color(0.13f, 0.12f, 0.09f, 0.95f));
            plate.raycastTarget = true;
            plate.rectTransform.anchorMin = min;
            plate.rectTransform.anchorMax = max;
            plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;
            var button = plate.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.79f, 0.58f, 0.23f);
            button.colors = colors;
            var label = NewText("Label", plate.transform, 32, new Color(0.87f, 0.84f, 0.76f));
            label.fontStyle = FontStyles.SmallCaps;
            label.gameObject.AddComponent<Localization.LocalizedLabel>().Key = key;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}
