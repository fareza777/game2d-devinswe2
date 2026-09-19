using System.Collections;
using System.Reflection;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A tree, stone pile or iron seam. Work it either way a player would try: tap Use beside it, or strike it
    /// with the weapon. It yields materials, says how many are now in the pack, and grows back after a while.
    ///
    /// Weapon hits reach it through a DestructibleProp2D that can never break (MapScene.BuildProp sets it up);
    /// each hit counts as a chop, every second one yields.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class GatherNode : MonoBehaviour
    {
        const int StrikesPerYield = 2;

        static readonly FieldInfo PropHitsField = typeof(DestructibleProp2D)
            .GetField("_hits", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] string itemId = "mat_timber";
        [SerializeField] int amount = 2;
        [SerializeField] float gatherSeconds = 1.1f;
        [SerializeField] float respawnSeconds = 45f;
        [SerializeField] string promptKey = "prompt.gather.timber";
        [SerializeField] AudioClip gatherSound;

        WorldInteractable interactable;
        DestructibleProp2D prop;
        SpriteRenderer[] renderers;
        int countedStrikes;
        bool busy;

        public string ItemId => itemId;

        /// <summary>False while the node is being worked or has not grown back yet.</summary>
        public bool IsAvailable => !busy;

        void Awake()
        {
            interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt(promptKey);
            interactable.Used += Gather;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            prop = GetComponentInChildren<DestructibleProp2D>();
            if (prop)
            {
                // The pack's destructible tree and stone prefabs are break-apart effects: they ship with
                // destroyOnSpawn on, so in Start they play their destruction and switch off every collider,
                // this node's interaction area included. That is what left the timber quest with nothing to use.
                // Awake runs before any Start, so switching it off here keeps the tree standing.
                prop.destroyOnSpawn = false;
                prop.maxHits = int.MaxValue;
            }
        }

        void Update()
        {
            if (!prop || busy || PropHitsField == null)
                return;
            int strikes = (int)PropHitsField.GetValue(prop);
            if (strikes - countedStrikes < 1)
                return;
            countedStrikes = strikes;
            Core.GameServices.Audio.PlaySfx(IsStone ? "gather_stone" : "gather_wood", 0.7f, 0.12f);
            if (strikes % StrikesPerYield == 0)
                Gather();
        }

        bool IsStone => itemId.Contains("stone") || itemId.Contains("iron");

        void Gather() => StartCoroutine(GatherRoutine());

        IEnumerator GatherRoutine()
        {
            if (busy)
                yield break;
            busy = true;

            Vector3 rest = transform.localPosition;
            for (float t = 0f; t < gatherSeconds; t += Time.deltaTime)
            {
                transform.localPosition = rest + new Vector3(Mathf.Sin(t * 28f) * 0.03f, 0f, 0f);
                yield return null;
            }
            transform.localPosition = rest;

            Progress.PlayerState state = Progress.PlayerState.Instance;
            state?.Inventory.Add(itemId, amount);
            Core.GameServices.Save.Current?.AddCounter($"gathered.{itemId}", amount);
            if (gatherSound)
                Core.GameServices.Audio.PlaySfx(gatherSound);
            else
                Core.GameServices.Audio.PlaySfx(IsStone ? "gather_stone" : "gather_wood");
            if (state != null)
                UI.Toast.ShowText(Core.GameServices.Localization.Format("toast.gathered", amount,
                    Core.GameServices.Localization.Get($"item.{itemId}.name"), state.Inventory.Count(itemId)));
            Quests.QuestRuntime.Evaluate();

            SetDepleted(true);
            yield return new WaitForSeconds(respawnSeconds);
            SetDepleted(false);
            busy = false;
        }

        /// <summary>A picked node stays standing, greyed, and its prompt says it is growing back.</summary>
        void SetDepleted(bool depleted)
        {
            foreach (SpriteRenderer renderer in renderers)
                renderer.color = depleted ? new Color(0.55f, 0.55f, 0.52f, 0.85f) : Color.white;
            interactable.SetPrompt(depleted ? "prompt.gather.regrowing" : promptKey);
        }
    }
}
