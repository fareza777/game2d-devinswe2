using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps a character out of the settlement until the story has sent for them. The object is built into
    /// the scene once and simply is not there until its flag is set, so arrivals need no spawning code.
    /// </summary>
    public class FlagGatedPresence : MonoBehaviour
    {
        [SerializeField] string requiresFlag;
        [SerializeField] string blockedByFlag;
        [SerializeField] GameObject body;

        void Awake()
        {
            // The gate has to stay awake to notice the flag, so the character must be a separate child.
            if (!body || body == gameObject)
            {
                Debug.LogError($"[Oathfire] {name} needs a child body to gate; disabling the gate");
                enabled = false;
                return;
            }
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            bool present = save != null &&
                           (string.IsNullOrEmpty(requiresFlag) || save.HasFlag(requiresFlag)) &&
                           (string.IsNullOrEmpty(blockedByFlag) || !save.HasFlag(blockedByFlag));
            if (body.activeSelf != present)
                body.SetActive(present);
        }
    }
}
