using System.Collections;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;

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
            if (lost > 0)
                state.Inventory.coin -= lost;

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
    }
}
