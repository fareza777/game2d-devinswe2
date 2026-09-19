using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Oathfire.World
{
    /// <summary>
    /// The oathfire at the centre of the settlement. Lighting it is the first quest and the mechanical
    /// heart of the game: its light is the safe radius, and it grows as the settlement grows.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class OathfireHearth : MonoBehaviour
    {
        public static OathfireHearth Instance { get; private set; }

        [SerializeField] float litRadius = 9f;
        [SerializeField] float unlitRadius = 2f;
        [SerializeField] GameObject flameVisual;
        [SerializeField] AudioClip lightSound;
        [SerializeField] string lightFlag = "act1.oathfire_lit";

        Light2D hearthLight;
        WorldInteractable interactable;
        float targetRadius;

        const int TimberToLight = 8;
        const int StoneToLight = 5;

        public bool IsLit { get; private set; }
        public float SafeRadius => IsLit ? litRadius : unlitRadius;

        void Awake()
        {
            Instance = this;
            interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt("prompt.hearth.light");
            interactable.Used += TryLight;

            hearthLight = GetComponentInChildren<Light2D>();
            targetRadius = unlitRadius;
            if (flameVisual)
                flameVisual.SetActive(false);
        }

        void Start()
        {
            if (Core.GameServices.Save.Current != null && Core.GameServices.Save.Current.HasFlag(lightFlag))
                Light(silent: true);
        }

        void Update()
        {
            if (!hearthLight)
                return;
            // Breathing flicker so the safe circle feels alive rather than a drawn ring.
            float flicker = 0.94f + 0.06f * Mathf.PerlinNoise(Time.time * 2.4f, 0f);
            hearthLight.pointLightOuterRadius = Mathf.Lerp(hearthLight.pointLightOuterRadius, targetRadius * flicker, Time.deltaTime * 2f);
            hearthLight.intensity = Mathf.Lerp(hearthLight.intensity, IsLit ? 1.15f * flicker : 0.25f, Time.deltaTime * 2f);

            // The prompt says what a tap will do: call the night when it can be called, otherwise just tend.
            if (IsLit)
            {
                Save.SaveData save = Core.GameServices.Save.Current;
                bool callable = NightDirector.Instance && !NightDirector.Instance.IsNightRunning && save != null
                                && ((save.HasFlag("act1.night1_ready") && !save.HasFlag("act1.night1_cleared"))
                                    || NightDirector.Instance.CanCallAnotherNight);
                interactable.SetPrompt(callable ? "prompt.hearth.callNight" : "prompt.hearth.tend");
            }
        }

        void TryLight()
        {
            if (!IsLit)
            {
                // The fire is built from what the Warden gathered: it takes the quest's timber and stone, and says
                // exactly what is still missing rather than lighting from nothing.
                Progress.PlayerState state = Progress.PlayerState.Instance;
                int timber = state?.Inventory.Count("mat_timber") ?? 0;
                int stone = state?.Inventory.Count("mat_stone") ?? 0;
                if (timber < TimberToLight || stone < StoneToLight)
                {
                    UI.Toast.ShowText(Core.GameServices.Localization.Format("toast.hearthNeeds",
                        Mathf.Min(timber, TimberToLight), TimberToLight, Mathf.Min(stone, StoneToLight), StoneToLight));
                    Core.GameServices.Audio.PlaySfx("denied", 0.8f, 0f);
                    return;
                }
                state.Inventory.Remove("mat_timber", TimberToLight);
                state.Inventory.Remove("mat_stone", StoneToLight);
                Light(silent: false);
                return;
            }

            // Once the story says the night is coming, tending the fire is how the player calls it in. After the
            // first night the fire can call the dead back whenever the valley has rested: that is where kills,
            // later kinds of enemy and nights survived come from, which half the side work asks for.
            Save.SaveData save = Core.GameServices.Save.Current;
            bool firstNight = save != null && save.HasFlag("act1.night1_ready") && !save.HasFlag("act1.night1_cleared");
            NightDirector night = NightDirector.Instance;
            if (night && !night.IsNightRunning && (firstNight || night.CanCallAnotherNight))
            {
                night.BeginNight();
                return;
            }
            if (night && night.IsNightRunning)
                return;
            UI.Toast.Show(save != null && save.HasFlag("act1.night1_cleared") ? "toast.hearthResting" : "toast.hearthTended");
        }

        void Light(bool silent)
        {
            IsLit = true;
            targetRadius = litRadius;
            if (flameVisual)
                flameVisual.SetActive(true);
            if (!silent)
            {
                if (lightSound)
                    Core.GameServices.Audio.PlaySfx(lightSound);
                else
                    Core.GameServices.Audio.PlaySfx("hearth_light", 1f, 0f);
                Core.GameServices.Save.Current?.SetFlag(lightFlag);
                StartCoroutine(FlareRoutine());
            }
            interactable.SetPrompt("prompt.hearth.tend");
        }

        /// <summary>A short flare when the fire catches, so the moment reads as an event, not a toggle.</summary>
        IEnumerator FlareRoutine()
        {
            float peak = litRadius * 1.6f;
            targetRadius = peak;
            yield return new WaitForSeconds(1.2f);
            targetRadius = litRadius;
        }

        /// <summary>Called when the settlement grows; every raised building widens the protected ground.</summary>
        public void Expand(float extraRadius)
        {
            litRadius += extraRadius;
            if (IsLit)
                targetRadius = litRadius;
        }

        public bool IsInsideLight(Vector3 position) => (position - transform.position).sqrMagnitude <= SafeRadius * SafeRadius;
    }
}
