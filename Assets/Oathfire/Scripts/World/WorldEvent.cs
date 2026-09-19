namespace Oathfire.World
{
    /// <summary>When an event is allowed to happen. Night events arrive while the dead are still walking.</summary>
    public enum WorldEventPhase { Any, Day, Night }

    /// <summary>
    /// A visitor walks in and can be spoken to; an apparition only shows itself at the edge of the light
    /// and leaves, which is the whole point of it.
    /// </summary>
    public enum WorldEventKind { Visitor, Apparition }

    /// <summary>
    /// One scripted thing that can happen in the settlement between fights. Written in Docs/world_events.json
    /// and built into Resources/Events/world.json, so adding an encounter never means touching a scene.
    /// </summary>
    [System.Serializable]
    public class WorldEventDefinition
    {
        public string id;
        public WorldEventKind kind;
        public WorldEventPhase phase;

        [UnityEngine.Tooltip("Dialogue script played when the player speaks to the visitor.")]
        public string scriptId;
        public string speakerKey;
        public string bannerKey;

        [UnityEngine.Tooltip("Only offered once this flag is set (empty = from the start).")]
        public string requiresFlag;
        [UnityEngine.Tooltip("Never offered once this flag is set.")]
        public string blockedByFlag;
        public int minNights;

        [UnityEngine.Tooltip("Higher priority events are considered first.")]
        public int priority;
        [UnityEngine.Tooltip("Chance of firing each time the event is considered, so arrivals stay unscheduled.")]
        public float chance = 0.5f;

        [UnityEngine.Tooltip("Where the visitor comes in from, in degrees around the hearth.")]
        public float approachAngle;
        [UnityEngine.Tooltip("How far out they start, as a multiple of the hearth's safe radius.")]
        public float approachDistance = 1.8f;
        public string tint = "#FFFFFF";
        [UnityEngine.Tooltip("Arrives as a silhouette rather than a person, but can still be spoken to.")]
        public bool shade;

        [UnityEngine.Tooltip("Seconds before the visitor gives up and leaves (0 = they wait indefinitely).")]
        public float timeLimitSeconds;
        [UnityEngine.Tooltip("Set when the visitor leaves unanswered.")]
        public string expireFlag;

        /// <summary>Set by the dialogue's end nodes, and again on expiry, so an event never repeats.</summary>
        public string DoneFlag => $"event.{id}.done";
    }

    [System.Serializable]
    public class WorldEventTable
    {
        public WorldEventDefinition[] events;
    }
}
