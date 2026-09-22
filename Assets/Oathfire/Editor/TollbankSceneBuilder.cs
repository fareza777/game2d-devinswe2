using System.Collections.Generic;
using System.Linq;
using Oathfire.Progress;
using Oathfire.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Builds the Tollbank, the League's ruined toll fort north of the valley road: a broken
    /// square of ramparts round a gravel court — plank tollhouse deck with its counter and
    /// strongbox row, the cutter's bench where the false dies were struck, the tollmen's cage
    /// pen, and the washing crew's fire. The end of the forged coin's road. Halvard the Cutter
    /// waits on the deck's step once the court is taken. Map from Tools/compose_tollbank.py.
    /// </summary>
    public static class TollbankSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Tollbank.unity";

        [MenuItem("Oathfire/Build Tollbank Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("tollbank");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Tollbank");

            GameObject[] washers = TradeRoadSceneBuilder.EnemyPrefabs();
            if (at.arrivals != null && at.arrivals.Length > 1)
            {
                BuildFight("Breach watch", map.At(at.arrivals[0]), washers, count: 5, unlocked: 3,
                    health: 1.2f, damage: 14, "event.toll.breach", "toll.breach_cleared", "toast.tollBreach");
                BuildFight("Court watch", map.At(at.arrivals[1]), washers, count: 5, unlocked: 4,
                    health: 1.2f, damage: 15, "event.toll.court", "toll.court_cleared", "toast.tollCourt");
                var boss = new GameObject("Halvard's dais");
                boss.transform.position = map.At(at.arrivals[2]);
                BuildCutter(boss, map.At(at.arrivals[2]));
                MapScene.Gate(boss, "toll.court_cleared");
            }
            BuildSurvivors(map, at);
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.tollbankOut", "");

            MapScene.SetUpUi(includeContractBoard: false);
            new GameObject("PlayerState", typeof(PlayerState));
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Oathfire] Tollbank scene built");
        }

        /// <summary>One washing crew, two watches: the breach and the court.</summary>
        static void BuildFight(string name, Vector3 position, GameObject[] prefabs, int count, int unlocked,
            float health, int damage, string bannerKey, string clearedFlag, string toastKey)
        {
            var fight = new GameObject(name, typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("toll_yard", clearedFlag);

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = count;
            serialized.FindProperty("unlockedLooks").intValue = unlocked;
            serialized.FindProperty("healthMultiplier").floatValue = health;
            serialized.FindProperty("hitDamage").intValue = damage;
            serialized.FindProperty("triggerRadius").floatValue = 4.5f;
            serialized.FindProperty("spread").floatValue = 3.5f;
            serialized.FindProperty("bannerKey").stringValue = bannerKey;
            serialized.FindProperty("clearedFlag").stringValue = clearedFlag;
            serialized.FindProperty("counterKey").stringValue = "kills.toll";
            serialized.FindProperty("clearedToastKey").stringValue = toastKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>
        /// Halvard the Cutter, the League's left hand: the die-sinker the pay-roll never named because
        /// he signed it himself. He waits on the tollhouse step in assay grey, and when the talking is
        /// done the washing crew's master comes out — the boss the whole valley's coin paid for.
        /// </summary>
        static void BuildCutter(GameObject parent, Vector3 position)
        {
            var encounter = new GameObject("Halvard the Cutter", typeof(BossEncounter), typeof(QuestSpot));
            encounter.transform.SetParent(parent.transform, false);
            encounter.transform.position = position;
            encounter.GetComponent<QuestSpot>().Configure("toll_boss", "act3.halvard_down");

            Color assay = new Color(0.62f, 0.66f, 0.72f);
            var standIn = new GameObject("Halvard (waiting)");
            standIn.transform.SetParent(encounter.transform, false);
            standIn.transform.position = position;
            MapScene.GiveNpcBody(standIn, "NPC2", assay);
            ActorSorting.Raise(standIn);
            standIn.transform.localScale *= 1.18f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "Enemy/Boss/EnemyBoss.prefab");
            if (!prefab)
                Debug.LogError("[Oathfire] Boss prefab missing; Halvard cannot fight");

            var serialized = new SerializedObject(encounter.GetComponent<BossEncounter>());
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("standIn").objectReferenceValue = standIn;
            serialized.FindProperty("nameKey").stringValue = "boss.halvard";
            serialized.FindProperty("introScriptId").stringValue = "toll_halvard";
            serialized.FindProperty("defeatedFlag").stringValue = "act3.halvard_down";
            serialized.FindProperty("defeatedToastKey").stringValue = "toast.halvardDown";
            serialized.FindProperty("healthMultiplier").floatValue = 1.25f;
            serialized.FindProperty("hitDamage").intValue = 15;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ActorLook look = MapScene.Look("Halvard the Cutter", "NPC2", assay, 1f, 1f, "kills.toll");
            var lookProperty = new SerializedObject(encounter.GetComponent<BossEncounter>());
            SerializedProperty entry = lookProperty.FindProperty("look");
            entry.FindPropertyRelative("name").stringValue = look.name;
            entry.FindPropertyRelative("rig").objectReferenceValue = look.rig;
            entry.FindPropertyRelative("tint").colorValue = look.tint;
            entry.FindPropertyRelative("minScale").floatValue = look.minScale;
            entry.FindPropertyRelative("maxScale").floatValue = look.maxScale;
            entry.FindPropertyRelative("killCounter").stringValue = look.killCounter;
            lookProperty.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The freed tollkeepers come back when the yard is quiet: the widow who counted the cages,
        /// the old tollman, the boy who ran the lane, the clerk who kept the books the crew couldn't read.
        /// </summary>
        static void BuildSurvivors(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_tollbank.py: widow, tollman, boy, clerk.
            if (spots.Length > 0)
                MapScene.BuildTalker("Widow Annet", "speaker.annet", map.At(spots[0]), "NPC1",
                    new Color(0.82f, 0.76f, 0.72f), 0.96f, new[] { ("toll_annet", "", "") },
                    requiresFlag: "toll.court_cleared");
            if (spots.Length > 1)
                MapScene.BuildTalker("Tollman Joss", "speaker.joss", map.At(spots[1]), "NPC2",
                    new Color(0.72f, 0.7f, 0.64f), 1f, new[] { ("toll_joss", "", "") },
                    requiresFlag: "toll.court_cleared");
            if (spots.Length > 2)
                MapScene.BuildTalker("Boy Pike", "speaker.pike", map.At(spots[2]), "NPC1",
                    new Color(0.9f, 0.86f, 0.74f), 0.9f, new[] { ("toll_pike", "", "") },
                    requiresFlag: "toll.court_cleared");
            if (spots.Length > 3)
                MapScene.BuildTalker("Clerk Effa", "speaker.effa", map.At(spots[3]), "NPC1",
                    new Color(0.78f, 0.8f, 0.72f), 0.94f, new[] { ("toll_effa", "", "") },
                    requiresFlag: "toll.court_cleared");

            // Two porters come back to work the court once it is safe — they wander.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject porter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!porter)
                    continue;
                porter.name = "Toll porter";
                foreach (var trader in porter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = porter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                porter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(porter, "NPC2", new Color(0.74f, 0.68f, 0.58f));
                ActorSorting.Raise(porter);
                var wanderer = porter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 2.8f;
                wander.FindProperty("walkSpeed").floatValue = 0.9f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(porter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.toll_porter";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "toll_porter";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(porter);
                MapScene.Gate(porter, "toll.court_cleared");
            }
        }

        /// <summary>
        /// What the work wants found: toll tallies on the court, the fort's banner at the pen,
        /// four strongboxes, the clerk's scattered pages — and the true ledger under the
        /// counter, which only opens once Halvard is down and still under watch.
        /// </summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: toll tallies x3, the toll banner, strongboxes x4.
            string[] tallyTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Dropped toll-tally", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"toll_tally_{i + 1}", tallyTiles[i], "quest_toll_tally",
                    $"toll.tally_{i + 1}", "quest.sq_toll_tallies.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The toll banner", map.At(spots[3]), "toll_banner", "Misc B43_N",
                    "quest_toll_banner", "toll.banner_taken", "quest.sq_toll_banner.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.toll.banner");
            string[] boxTiles = { "Chest A1_E", "Chest A2_E", "Chest A1_E", "Chest A2_E" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("A toll strongbox", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"toll_strongbox_{i - 3}", boxTiles[i - 4], "quest_strongbox",
                    $"toll.box_{i - 3}", "quest.sq_toll_strongboxes.taken");
            // The clerk's pages scattered when the books were burned.
            if (at.stone != null && at.stone.Length > 0)
                for (int i = 0; i < 3; i++)
                    MapScene.BuildSearchSpot("A torn ledger page", map.At(at.stone[0]) + new Vector3(0.5f * i, 0.3f * (i % 2), 0f),
                        $"toll_page_{i + 1}", "Misc A2_E", "quest_ledger_page",
                        $"toll.page_{i + 1}", "quest.sq_toll_pages.taken");
            // The true ledger: under the counter, behind the boss, still guarded.
            if (at.arrivals != null && at.arrivals.Length > 2)
                MapScene.BuildSearchSpot("The true ledger", map.At(at.arrivals[2]) + new Vector3(0.9f, -0.5f, 0f),
                    "toll_ledger", "Misc B8_E", "quest_true_ledger", "act3.ledger_taken", "act3.halvard_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.toll.ledger");
            // What the washer-captains skimmed off the tolls: the marshal's star and the black bow.
            if (at.arrivals != null && at.arrivals.Length > 0)
                MapScene.BuildSearchSpot("The marshal's star", map.At(at.arrivals[0]) + new Vector3(0.8f, -0.4f, 0f),
                    "cache_toll_star", "Chest A1_E", "trinket_marshal_star", "cache.toll.star", "act3.halvard_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
            if (at.villagers != null && at.villagers.Length > 0)
                MapScene.BuildSearchSpot("The black bow", map.At(at.villagers[0]) + new Vector3(0.75f, 0.35f, 0f),
                    "cache_toll_bow", "Chest A1_E", "bow_black", "cache.toll.bow", "act3.halvard_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
        }
    }
}
