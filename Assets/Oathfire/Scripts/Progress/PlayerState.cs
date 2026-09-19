using System;
using UnityEngine;

namespace Oathfire.Progress
{
    /// <summary>
    /// The player's living state during a session: level, experience, inventory and derived combat stats.
    /// It mirrors into the save file so a save captures everything without a second serialization path.
    /// </summary>
    public class PlayerState : MonoBehaviour
    {
        public const int MaxLevel = 20;
        public const int HealthPerLevel = 12;
        public const int DamagePerLevel = 2;

        public static PlayerState Instance { get; private set; }

        [SerializeField] int baseHealth = 100;
        [SerializeField] int baseDamage = 10;

        public Items.Inventory Inventory { get; private set; } = new Items.Inventory();
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }

        public event Action<int> LeveledUp;
        public event Action Changed;

        // A chapter's worth of work is about five levels: quests alone will not carry the Warden up, and a
        // player who fights and takes side work is a level or two ahead rather than ten.
        public int ExperienceForNextLevel => 200 + (Level - 1) * 180;
        public int MaxHealth => baseHealth + (Level - 1) * HealthPerLevel + Inventory.TotalHealthBonus();
        public int Damage => baseDamage + (Level - 1) * DamagePerLevel + Inventory.TotalDamage();
        public int Armor => Inventory.TotalArmor();

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Core.GameServices.EnsureCreated();
            Items.ItemDatabase.EnsureLoaded();
            Inventory.Changed += () => Changed?.Invoke();
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0)
                return;
            Experience += amount;
            while (Level < MaxLevel && Experience >= ExperienceForNextLevel)
            {
                Experience -= ExperienceForNextLevel;
                Level++;
                LeveledUp?.Invoke(Level);
            }
            Changed?.Invoke();
        }

        /// <summary>Writes the live state into the save file before it is written to disk.</summary>
        public void WriteTo(Save.SaveData save)
        {
            save.playerLevel = Level;
            save.AddCounter("xp", Experience - save.GetCounter("xp"));
        }

        public void ReadFrom(Save.SaveData save)
        {
            Level = Mathf.Max(1, save.playerLevel);
            Experience = save.GetCounter("xp");
            Changed?.Invoke();
        }
    }
}
