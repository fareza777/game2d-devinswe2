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
    /// Builds the Tallykeep, the League's mint-fortress on its crag — the chapter's grand
    /// finale: a processional causeway up to the outer curtain, the washing army's camp in
    /// the bailey, a second curtain, the mint court with its press row and counting desks,
    /// and the great press hall where Coinmaster Hane waits on the dais. The washed coin's
    /// last address. Map from Tools/compose_tallykeep.py.
    /// </summary>
    public static class TallykeepSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Tallykeep.unity";

        [MenuItem("Oathfire/Build Tallykeep Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("tallykeep");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Tallykeep");

            GameObject[] mintGuard = TradeRoadSceneBuilder.EnemyPrefabs();
            if (at.arrivals != null && at.arrivals.Length > 3)
            {
                BuildFight("Gatehouse watch", map.At(at.arrivals[0]), mintGuard, count: 4, unlocked: 4,
                    health: 1.35f, damage: 16, "event.tally.gate", "tally.gate_cleared", "toast.tallyGate");
                BuildFight("Bailey watch", map.At(at.arrivals[1]), mintGuard, count: 4, unlocked: 5,
                    health: 1.35f, damage: 17, "event.tally.bailey", "tally.bailey_cleared", "toast.tallyBailey");
                BuildFight("Court watch", map.At(at.arrivals[2]), mintGuard, count: 5, unlocked: 5,
                    health: 1.4f, damage: 17, "event.tally.court", "tally.court_cleared", "toast.tallyCourt");
                var dais = new GameObject("The press dais");
                dais.transform.position = map.At(at.arrivals[3]);
                BuildCoinmaster(dais, map.At(at.arrivals[3]));
                MapScene.Gate(dais, "tally.court_cleared");
            }
            BuildSurvivors(map, at);
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "Tollbank", "prompt.travel.tallykeepOut", "");

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
            Debug.Log("[Oathfire] Tallykeep scene built");
        }

        /// <summary>Three watches of the mint-guard: the gatehouse, the bailey camp, the mint court.</summary>
        static void BuildFight(string name, Vector3 position, GameObject[] prefabs, int count, int unlocked,
            float health, int damage, string bannerKey, string clearedFlag, string toastKey)
        {
            var fight = new GameObject(name, typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("tally_keep", clearedFlag);

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
            serialized.FindProperty("counterKey").stringValue = "kills.tally";
            serialized.FindProperty("clearedToastKey").stringValue = toastKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>
        /// Coinmaster Hane, the mint's weigher: the man the pay-roll, the dies and the wash
        /// all answered to. He waits on the press dais in assay gold, and when the talking is
        /// done the League's left hand makes his stand where the coin was struck.
        /// </summary>
        static void BuildCoinmaster(GameObject parent, Vector3 position)
        {
            var encounter = new GameObject("Coinmaster Hane", typeof(BossEncounter), typeof(QuestSpot));
            encounter.transform.SetParent(parent.transform, false);
            encounter.transform.position = position;
            encounter.GetComponent<QuestSpot>().Configure("tally_boss", "act3.hane_down");

            Color assay = new Color(0.85f, 0.78f, 0.55f);
            var standIn = new GameObject("Hane (waiting)");
            standIn.transform.SetParent(encounter.transform, false);
            standIn.transform.position = position;
            MapScene.GiveNpcBody(standIn, "NPC2", assay);
            ActorSorting.Raise(standIn);
            standIn.transform.localScale *= 1.2f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "Enemy/Boss/EnemyBoss.prefab");
            if (!prefab)
                Debug.LogError("[Oathfire] Boss prefab missing; Hane cannot fight");

            var serialized = new SerializedObject(encounter.GetComponent<BossEncounter>());
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("standIn").objectReferenceValue = standIn;
            serialized.FindProperty("nameKey").stringValue = "boss.hane";
            serialized.FindProperty("introScriptId").stringValue = "tally_hane";
            serialized.FindProperty("defeatedFlag").stringValue = "act3.hane_down";
            serialized.FindProperty("defeatedToastKey").stringValue = "toast.haneDown";
            serialized.FindProperty("healthMultiplier").floatValue = 1.45f;
            serialized.FindProperty("hitDamage").intValue = 18;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ActorLook look = MapScene.Look("Coinmaster Hane", "NPC2", assay, 1f, 1f, "kills.tally");
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
        /// The mint's own come back when the Coinmaster falls: the keeper who hid the true tally,
        /// the clerk the dies were struck for, the presshand, the boy who ran the gate bell.
        /// </summary>
        static void BuildSurvivors(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_tallykeep.py: keeper, clerk, presshand, boy.
            if (spots.Length > 0)
                MapScene.BuildTalker("Keeper Soll", "speaker.soll", map.At(spots[0]), "NPC2",
                    new Color(0.8f, 0.74f, 0.62f), 1f, new[] { ("tally_soll", "", "") },
                    requiresFlag: "act3.hane_down");
            if (spots.Length > 1)
                MapScene.BuildTalker("Clerk Nance", "speaker.nance", map.At(spots[1]), "NPC1",
                    new Color(0.78f, 0.82f, 0.74f), 0.95f, new[] { ("tally_nance", "", "") },
                    requiresFlag: "act3.hane_down");
            if (spots.Length > 2)
                MapScene.BuildTalker("Presshand Dory", "speaker.dory", map.At(spots[2]), "NPC1",
                    new Color(0.86f, 0.78f, 0.66f), 0.98f, new[] { ("tally_dory", "", "") },
                    requiresFlag: "act3.hane_down");
            if (spots.Length > 3)
                MapScene.BuildTalker("Boy Kit", "speaker.kit", map.At(spots[3]), "NPC1",
                    new Color(0.92f, 0.88f, 0.76f), 0.88f, new[] { ("tally_kit", "", "") },
                    requiresFlag: "act3.hane_down");

            // Two mint porters come back to work the yard once it is safe — they wander.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject porter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!porter)
                    continue;
                porter.name = "Mint porter";
                foreach (var trader in porter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = porter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                porter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(porter, "NPC2", new Color(0.76f, 0.72f, 0.6f));
                ActorSorting.Raise(porter);
                var wanderer = porter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 3.2f;
                wander.FindProperty("walkSpeed").floatValue = 0.9f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(porter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.tally_porter";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "tally_porter";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(porter);
                MapScene.Gate(porter, "act3.hane_down");
            }
        }

        /// <summary>
        /// What the mint's fall leaves to find: mint tallies on the desks, the die-rack under
        /// watch, four seal-boxes, the clerk's assay pages — and the true seals at the dais,
        /// which only open once Hane is down. The chapter's last caches hide here too.
        /// </summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: mint tallies x3, the die-rack, seal-boxes x4.
            string[] tallyTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Dropped mint-tally", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"tally_tally_{i + 1}", tallyTiles[i], "quest_mint_tally",
                    $"tally.tally_{i + 1}", "quest.sq_tally_tallies.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The die-rack", map.At(spots[3]), "tally_dierack", "Misc A3_E",
                    "quest_die_rack", "tally.dierack_taken", "quest.sq_tally_tallies.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.tally.dierack");
            string[] boxTiles = { "Chest A1_E", "Chest A2_E", "Chest B1_E", "Chest A2_E" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("A seal-box", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"tally_sealbox_{i - 3}", boxTiles[i - 4], "quest_sealbox",
                    $"tally.box_{i - 3}", "quest.sq_tally_sealboxes.taken");
            // The assay pages the clerks burned in the yard furnace.
            if (at.stone != null && at.stone.Length > 0)
                for (int i = 0; i < 3; i++)
                    MapScene.BuildSearchSpot("An assay page", map.At(at.stone[0]) + new Vector3(0.5f * i, 0.3f * (i % 2), 0f),
                        $"tally_page_{i + 1}", "Misc A2_E", "quest_mint_page",
                        $"tally.page_{i + 1}", "quest.sq_tally_pages.taken");
            // The true seals: beside the press, behind the Coinmaster, still under the last guard.
            if (at.arrivals != null && at.arrivals.Length > 3)
            {
                MapScene.BuildSearchSpot("The true seals", map.At(at.arrivals[3]) + new Vector3(0.9f, -0.5f, 0f),
                    "tally_seals", "Misc B8_E", "quest_true_seals", "act3.seals_taken", "act3.hane_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.tally.seals");
                // The mint's own hoard, opened only when the Coinmaster falls.
                MapScene.BuildSearchSpot("The sealbreaker's chest", map.At(at.arrivals[3]) + new Vector3(-0.9f, -0.5f, 0f),
                    "cache_tally_sword", "Chest A2_E", "sword_sealbreaker", "cache.tally.sword", "act3.hane_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
            }
            if (at.villagers != null && at.villagers.Length > 1)
                MapScene.BuildSearchSpot("The assayer's ring", map.At(at.villagers[1]) + new Vector3(0.75f, 0.35f, 0f),
                    "cache_tally_ring", "Chest A1_E", "ring_assay", "cache.tally.ring", "act3.hane_down",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
            // The gate's tally bell — taken down by the washers so the counts stopped being heard.
            if (at.arrivals != null && at.arrivals.Length > 0)
                MapScene.BuildSearchSpot("The gate's tally bell", map.At(at.arrivals[0]) + new Vector3(-1.1f, 0.6f, 0f),
                    "tally_bell", "Misc B43_N", "quest_gate_bell", "tally.bell_taken", "quest.sq_tally_bell.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.tally.bell");
        }
    }
}
