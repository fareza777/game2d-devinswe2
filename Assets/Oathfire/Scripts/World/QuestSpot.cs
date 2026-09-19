using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A place a quest can point the arrow at when no person, hearth or road stands there: a camp to break, a
    /// captain's clearing. Hidden once its flag is set, so a second spot with the same id takes over.
    /// </summary>
    public class QuestSpot : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] string hiddenIfFlag;

        public string Id => id;

        public bool IsActive
        {
            get
            {
                Save.SaveData save = Core.GameServices.Save.Current;
                return string.IsNullOrEmpty(hiddenIfFlag) || save == null || !save.HasFlag(hiddenIfFlag);
            }
        }

        public void Configure(string spotId, string hideWhen)
        {
            id = spotId;
            hiddenIfFlag = hideWhen;
        }
    }
}
