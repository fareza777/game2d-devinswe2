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
    /// Builds the Stampworks, the coiners' yard up the chalk spur: a walled night-yard where the
    /// keep's diverted coin is struck into fresh seals — press benches under a scaffold beam, a
    /// crucible line that never goes cold, a counting dais for the master die, and a pens-corner
    /// where the League's borrowed clerks keep the false books. The end of the pay-roll's trail.
    /// Map from Tools/compose_stampworks.py (composed from scratch — no example-map crop).
    /// </summary>
    public static class StampworksSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Stampworks.unity";

        [MenuItem("Oathfire/Build Stampworks Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("stampworks");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Stampworks");

            BuildPeople(map, at);
            if (at.arrivals != null && at.arrivals.Length > 1)
                BuildWatch(map.At(at.arrivals[1]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "Chalkpit", "prompt.travel.stampworksOut", "");

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
            Debug.Log("[Oathfire] Stampworks scene built");
        }

        /// <summary>The yard's people: the penned clerk who keeps the false books, the camp cook,
        /// the die-setter who knows where the cracked plate went, and a runner watching the lane.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_stampworks.py: clerk, cook, die-setter, runner.
            if (spots.Length > 0)
                MapScene.BuildTalker("Clerk Novak", "speaker.novak", map.At(spots[0]), "NPC1",
                    new Color(0.8f, 0.78f, 0.7f), 0.97f, new[] { ("stamp_novak", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Cook Vall", "speaker.vall", map.At(spots[1]), "NPC1",
                    new Color(0.84f, 0.76f, 0.66f), 1f, new[] { ("stamp_vall", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Die-setter Jory", "speaker.jory", map.At(spots[2]), "NPC2",
                    new Color(0.7f, 0.68f, 0.62f), 1f, new[] { ("stamp_jory", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Runner Wicke", "speaker.wicke", map.At(spots[3]), "NPC1",
                    new Color(0.88f, 0.84f, 0.76f), 0.94f, new[] { ("stamp_wicke", "", "") });

            // Yard hands pace the weigh line while the presses stand quiet — muscle borrowed from
            // the same deserters' wage that bought the pit.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject hand = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!hand)
                    continue;
                hand.name = "Yard hand";
                foreach (var trader in hand.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = hand.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                hand.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(hand, "NPC2", new Color(0.76f, 0.7f, 0.6f));
                ActorSorting.Raise(hand);
                var wanderer = hand.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 2.6f;
                wander.FindProperty("walkSpeed").floatValue = 0.9f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(hand.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.yard_hand";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "stamp_hand";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(hand);
            }
        }

        /// <summary>The coiners' watch — better paid and better turned out than the pit's jacks.</summary>
        static void BuildWatch(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Coiners' watch", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("stamp_yard", "stamp.yard_cleared");

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = 5;
            serialized.FindProperty("unlockedLooks").intValue = 4;
            serialized.FindProperty("healthMultiplier").floatValue = 1.15f;
            serialized.FindProperty("hitDamage").intValue = 13;
            serialized.FindProperty("triggerRadius").floatValue = 4.5f;
            serialized.FindProperty("spread").floatValue = 3.5f;
            serialized.FindProperty("bannerKey").stringValue = "event.stamp.yard";
            serialized.FindProperty("clearedFlag").stringValue = "stamp.yard_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.stamp";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.stampClear";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: writ-slips on the benches, the master die on the
        /// counting dais, a cracked die-plate under the presses, silver blanks in the spoil run.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: slip spots x3, the master die on the dais, the cracked plate, blanks x4.
            string[] slipTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Dropped writ-slip", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"stamp_slip_{i + 1}", slipTiles[i], "quest_stamp_slip",
                    $"stamp.slip_{i + 1}", "quest.sq_stamp_slips.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The master die", map.At(spots[3]), "stamp_die", "Misc A2_E",
                    "quest_stamp_die", "act2.die_taken", "stamp.yard_cleared",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.stamp.die");
            if (spots.Length > 4)
                MapScene.BuildSearchSpot("The cracked die-plate", map.At(spots[4]), "stamp_plate", "Misc B26_N",
                    "quest_stamp_plate", "stamp.plate_found", "quest.sq_stamp_plates.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.stamp.plate");
            string[] blankTiles = { "Misc E6_N", "Misc E7_E", "Misc E8_N", "Misc E6_E" };
            for (int i = 5; i < 9 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Spilled silver blanks", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"stamp_blank_{i - 4}", blankTiles[i - 5], "quest_stamp_blank",
                    $"stamp.blank_{i - 4}", "quest.sq_stamp_blanks.taken");
            // A stamped blade, the forgemaster's apron, and the yard's deepest cache.
            if (spots.Length > 4)
                MapScene.BuildSearchSpot("The flicker-blade", map.At(spots[4]) + new Vector3(0.9f, -0.45f, 0f),
                    "cache_stamp_dagger", "Chest A1_E", "dagger_flicker", "cache.stamp.dagger", "",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
            if (at.villagers != null && at.villagers.Length > 0)
                MapScene.BuildSearchSpot("The forgemaster's apron", map.At(at.villagers[0]) + new Vector3(0.8f, 0.4f, 0f),
                    "cache_stamp_apron", "Chest A1_E", "armor_forgemaster", "cache.stamp.apron", "");
            if (at.stone != null && at.stone.Length > 1)
                MapScene.BuildSearchSpot("The lattice cache", map.At(at.stone[1]) + new Vector3(0.7f, -0.3f, 0f),
                    "cache_stamp_lattice", "Chest A1_E", "armor_bone_lattice", "cache.stamp.lattice", "",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.cacheGuard");
        }
    }
}
