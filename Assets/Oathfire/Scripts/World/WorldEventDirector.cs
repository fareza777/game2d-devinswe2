using System.Collections;
using System.Linq;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps the settlement from being a menu with trees. Between fights it sends people in from the dark:
    /// a Marshal who wants the dead counted, a refugee who arrives mid-wave, a trader whose offer expires.
    /// Events are data, their outcomes are flags, and the flags are what quests and contracts already read.
    /// </summary>
    public class WorldEventDirector : MonoBehaviour
    {
        public static WorldEventDirector Instance { get; private set; }

        /// <summary>What the HUD prints across the top while an event is live; empty when nothing is running.</summary>
        public static string Banner { get; private set; } = string.Empty;

        /// <summary>Lets other staged moments — a road ambush, a siege — use the same banner line.</summary>
        public static void SetBanner(string text) => Banner = text ?? string.Empty;

        [SerializeField] GameObject visitorPrefab;
        [Tooltip("The visitor template has no renderer of its own; this rig is what people actually see.")]
        [SerializeField] RuntimeAnimatorController visitorBody;
        [SerializeField] Sprite visitorStill;
        [SerializeField] Sprite apparitionSprite;
        [SerializeField] Sprite markerSprite;
        [SerializeField] float checkIntervalSeconds = 16f;
        [SerializeField] float walkSeconds = 4f;

        WorldEventDefinition[] events = System.Array.Empty<WorldEventDefinition>();

        /// <summary>
        /// How long an untimed visitor waits at the square with the banner up before simply staying in the
        /// settlement. After that they no longer occupy the director, so other arrivals can still happen.
        /// </summary>
        [SerializeField] float settleSeconds = 45f;

        readonly System.Collections.Generic.HashSet<string> standing = new System.Collections.Generic.HashSet<string>();

        public WorldEventDefinition Running { get; private set; }
        public GameObject Actor { get; private set; }

        /// <summary>Turned off while a scripted sequence (or the smoke test) is driving the world itself.</summary>
        public bool AutoEvents { get; set; } = true;

        void Awake()
        {
            Instance = this;
            var asset = Resources.Load<TextAsset>("Events/world");
            if (!asset)
            {
                Debug.LogError("[Oathfire] Missing Resources/Events/world.json");
                return;
            }
            events = JsonUtility.FromJson<WorldEventTable>(asset.text).events ?? events;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Banner = string.Empty;
        }

        IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkIntervalSeconds);
                if (!AutoEvents || Running != null || Dialogue.DialogueRunner.IsConversationActive)
                    continue;
                WorldEventDefinition next = Roll();
                if (next != null)
                    yield return StartCoroutine(RunEvent(next));
            }
        }

        /// <summary>Walks the eligible events best-first and takes the first one whose dice come up.</summary>
        WorldEventDefinition Roll() =>
            events.Where(IsEligible)
                .OrderByDescending(definition => definition.priority)
                .FirstOrDefault(definition => Random.value <= definition.chance);

        bool IsEligible(WorldEventDefinition definition)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || save.HasFlag(definition.DoneFlag) || standing.Contains(definition.id))
                return false;
            if (!string.IsNullOrEmpty(definition.requiresFlag) && !save.HasFlag(definition.requiresFlag))
                return false;
            if (!string.IsNullOrEmpty(definition.blockedByFlag) && save.HasFlag(definition.blockedByFlag))
                return false;
            if (save.GetCounter("nights.survived") < definition.minNights)
                return false;

            bool night = NightDirector.Instance && NightDirector.Instance.IsNightRunning;
            return definition.phase switch
            {
                WorldEventPhase.Day => !night,
                WorldEventPhase.Night => night,
                _ => true,
            };
        }

        /// <summary>Starts an event by name, whatever the dice say. Used by the smoke test and by debugging.</summary>
        public bool TryTrigger(string id)
        {
            WorldEventDefinition definition = events.FirstOrDefault(entry => entry.id == id);
            if (definition == null || Running != null)
                return false;
            StartCoroutine(RunEvent(definition));
            return true;
        }

        IEnumerator RunEvent(WorldEventDefinition definition)
        {
            Running = definition;
            yield return definition.kind == WorldEventKind.Apparition
                ? StartCoroutine(ApparitionRoutine(definition))
                : StartCoroutine(VisitorRoutine(definition));
            Running = null;
            Actor = null;
            Banner = string.Empty;
            Quests.QuestRuntime.Evaluate();
        }

        IEnumerator VisitorRoutine(WorldEventDefinition definition)
        {
            Vector3 arrival = PointAround(definition.approachAngle, definition.approachDistance);
            Vector3 stop = PointAround(definition.approachAngle, 0.55f);

            Actor = SpawnVisitor(definition, arrival);
            if (!Actor)
                yield break;

            Banner = BannerText(definition, definition.timeLimitSeconds);
            Core.GameServices.Audio.PlaySfx("event_alert", 0.9f, 0f);
            yield return StartCoroutine(WalkTo(Actor.transform, stop));

            Save.SaveData save = Core.GameServices.Save.Current;
            bool timed = definition.timeLimitSeconds > 0f;
            float deadline = timed ? Time.time + definition.timeLimitSeconds : Time.time + settleSeconds;
            while (save != null && !save.HasFlag(definition.DoneFlag) && Time.time < deadline)
            {
                // A conversation in progress must never be cut off by the clock running out.
                if (Dialogue.DialogueRunner.IsConversationActive)
                    deadline = Mathf.Max(deadline, Time.time + 1f);
                else if (timed)
                    Banner = BannerText(definition, deadline - Time.time);
                yield return null;
            }

            // An untimed visitor who has not been spoken to yet stays on in the settlement rather than leaving
            // or holding up the director. Story visitors cannot be missed, and nobody else is kept waiting.
            if (!timed && save != null && !save.HasFlag(definition.DoneFlag))
            {
                Banner = string.Empty;
                string id = definition.id;
                standing.Add(id);
                StandingVisitor.Hold(Actor, definition.DoneFlag, () => standing.Remove(id));
                yield break;
            }

            bool answered = save != null && save.HasFlag(definition.DoneFlag);
            if (!answered)
            {
                if (!string.IsNullOrEmpty(definition.expireFlag))
                    Quests.QuestRuntime.SetFlag(definition.expireFlag);
                Quests.QuestRuntime.SetFlag(definition.DoneFlag);
                UI.Toast.Show("toast.eventMissed");
            }

            Banner = string.Empty;
            yield return StartCoroutine(WalkTo(Actor.transform, arrival));
            Destroy(Actor);
        }

        /// <summary>A shape at the treeline that fades in, is looked at, and is gone. No one speaks to it.</summary>
        IEnumerator ApparitionRoutine(WorldEventDefinition definition)
        {
            Vector3 position = PointAround(definition.approachAngle, definition.approachDistance);
            var go = new GameObject($"Apparition {definition.id}", typeof(SpriteRenderer));
            go.transform.position = position;
            Actor = go;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = apparitionSprite;
            renderer.sortingOrder = 40;
            go.transform.localScale = Vector3.one * 1.6f;
            Color ink = ParseTint(definition.tint);

            Banner = BannerText(definition, 0f);
            Core.GameServices.Audio.PlaySfx("event_alert", 0.8f, 0f);
            float hold = definition.timeLimitSeconds > 0f ? definition.timeLimitSeconds : 7f;
            for (float t = 0f; t < hold; t += Time.deltaTime)
            {
                // Solid in the middle of its stay, ghosted at both ends, so it reads as a sighting.
                float alpha = Mathf.Sin(Mathf.Clamp01(t / hold) * Mathf.PI);
                renderer.color = new Color(ink.r, ink.g, ink.b, alpha * 0.9f);
                yield return null;
            }

            Quests.QuestRuntime.SetFlag(definition.DoneFlag);
            Destroy(go);
        }

        GameObject SpawnVisitor(WorldEventDefinition definition, Vector3 position)
        {
            GameObject actor = definition.shade ? SpawnShade(definition, position) : SpawnPerson(definition, position);
            if (!actor)
                return null;

            var trigger = actor.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            ActorSorting.Raise(actor);
            EventMarker.Attach(actor, markerSprite, ParseTint(definition.tint));
            actor.AddComponent<NpcDialogue>().Configure(definition.speakerKey, definition.scriptId);
            return actor;
        }

        GameObject SpawnPerson(WorldEventDefinition definition, Vector3 position)
        {
            if (!visitorPrefab)
            {
                Debug.LogError("[Oathfire] World event director has no visitor prefab");
                return null;
            }
            GameObject actor = Instantiate(visitorPrefab, position, visitorPrefab.transform.rotation);
            actor.name = $"Visitor {definition.id}";

            // The body is the tileset's trader; nobody here is running a shop.
            foreach (var shop in actor.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                Destroy(shop);

            if (!actor.GetComponentInChildren<SpriteRenderer>())
            {
                if (!visitorBody || !visitorStill)
                    Debug.LogError("[Oathfire] World event director has no visitor body; the visitor will be invisible");
                actor.AddComponent<SpriteRenderer>().sprite = visitorStill;
                actor.AddComponent<Animator>().runtimeAnimatorController = visitorBody;
            }

            Color tint = ParseTint(definition.tint);
            foreach (SpriteRenderer renderer in actor.GetComponentsInChildren<SpriteRenderer>())
                renderer.color = tint;
            return actor;
        }

        /// <summary>Something that walks in wearing the shape of a person without being one.</summary>
        GameObject SpawnShade(WorldEventDefinition definition, Vector3 position)
        {
            var actor = new GameObject($"Shade {definition.id}", typeof(SpriteRenderer));
            actor.transform.position = position;
            actor.transform.localScale = Vector3.one * 1.5f;

            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            renderer.sprite = apparitionSprite;
            renderer.sortingOrder = 40;
            Color ink = ParseTint(definition.tint);
            renderer.color = new Color(ink.r, ink.g, ink.b, 0.88f);
            return actor;
        }

        IEnumerator WalkTo(Transform actor, Vector3 target)
        {
            Vector3 from = actor.position;
            for (float t = 0f; t < walkSeconds && actor; t += Time.deltaTime)
            {
                actor.position = Vector3.Lerp(from, target, t / walkSeconds);
                yield return null;
            }
            if (actor)
                actor.position = target;
        }

        /// <summary>
        /// What the player is told. The first line sets the scene; the second says what to do about it and
        /// how long there is to do it. A bare clock with no instruction reads as a bug, not as urgency.
        /// </summary>
        string BannerText(WorldEventDefinition definition, float secondsLeft)
        {
            string text = Core.GameServices.Localization.Get(definition.bannerKey);
            if (definition.kind == WorldEventKind.Apparition)
                return text;
            if (definition.timeLimitSeconds <= 0f)
                return Line(text, Core.GameServices.Localization.Get("event.goSpeak"));

            int remaining = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            string clock = $"{remaining / 60}:{remaining % 60:00}";
            return Line(text, Core.GameServices.Localization.Format("event.goSpeakTimed", clock));
        }

        /// <summary>Scene-setting line above, the instruction in smaller type below it.</summary>
        static string Line(string headline, string instruction) =>
            headline + "\n<size=80%>" + instruction + "</size>";

        /// <summary>A point on the squashed isometric ring around the hearth.</summary>
        static Vector3 PointAround(float degrees, float radiusScale)
        {
            OathfireHearth hearth = OathfireHearth.Instance;
            Vector3 center = hearth ? hearth.transform.position : Vector3.zero;
            float radius = (hearth ? hearth.SafeRadius : 8f) * Mathf.Max(0.2f, radiusScale);
            float angle = degrees * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.5f, 0f);
        }

        static Color ParseTint(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
    }
}
