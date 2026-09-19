using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// The mark over someone who has work to give. Side work used to appear in the journal the moment the
    /// story unlocked it, which read as a list the game wrote for itself; now it is carried by a person, and
    /// this is how the player can see who is carrying it.
    /// </summary>
    [RequireComponent(typeof(NpcDialogue))]
    public class QuestOffers : MonoBehaviour
    {
        const float CheckSeconds = 0.5f;

        [SerializeField] Sprite markerSprite;

        NpcDialogue npc;
        GameObject marker;
        float nextCheck;

        void Awake() => npc = GetComponent<NpcDialogue>();

        void Update()
        {
            if (Time.time < nextCheck)
                return;
            nextCheck = Time.time + CheckSeconds;
            bool waiting = npc.PendingOffer() != null || npc.WaitingForHandIn();
            if (waiting && !marker)
            {
                marker = EventMarker.Attach(gameObject, markerSprite, new Color(1f, 0.78f, 0.32f)).gameObject;
                marker.transform.localScale = Vector3.one * 0.55f;
            }
            else if (marker)
                marker.SetActive(waiting);
        }
    }
}
