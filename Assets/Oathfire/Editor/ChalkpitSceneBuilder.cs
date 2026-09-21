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
    /// Builds the Chalkpit, the Salt League's abandoned lime workings: a pale square cut into the
    /// valley floor, rimmed by the faces the pick crews left, with a derrick platform over the deep
    /// end and the foreman's camp still standing at the rim. The crew walked off when their pay
    /// vanished into the keep's diverted wagons; pit-jacks squat the cut now, and the League's
    /// seal-stamp is still up on the hoist where the last tally-clerk dropped it.
    /// Map from Tools/compose_chalkpit.py (composed from scratch — no example-map crop).
    /// </summary>
    public static class ChalkpitSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Chalkpit.unity";

        [MenuItem("Oathfire/Build Chalkpit Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("chalkpit");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Chalkpit");

            BuildPeople(map, at);
            if (at.arrivals != null && at.arrivals.Length > 1)
                BuildJacks(map.At(at.arrivals[1]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.chalkpitOut", "");

            // The deep-end spur: the way the jacks' spoil climbs to the coiners' yard. It only
            // reads as a way up once the floor is theirs no longer.
            if (at.trail != null)
            {
                var stampSpur = new GameObject("Stamp spur");
                stampSpur.transform.position = map.At(at.trail);
                GameObject yardExit = MapScene.BuildExit(map.At(at.trail), "Stampworks", "prompt.travel.stampworks", "act2.found_stampworks");
                yardExit.transform.SetParent(stampSpur.transform, true);
                MapScene.BuildProp("Die-setter's mark", "Misc B53_E", map.At(at.trail) + new Vector3(-0.45f, -0.2f, 0f), yardExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(stampSpur, "pit.floor_cleared");
            }

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
            Debug.Log("[Oathfire] Chalkpit scene built");
        }

        /// <summary>The pit's people: the foreman's camp on the rim, the clerk by the tally post,
        /// a squatter on the pit floor, a cutter still working the fresh bench.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_chalkpit.py: foreman, tally-clerk, seal-warden, squatter, cutter.
            if (spots.Length > 0)
                MapScene.BuildTalker("Foreman Brack", "speaker.brack", map.At(spots[0]), "NPC2",
                    new Color(0.78f, 0.72f, 0.6f), 1.02f, new[] { ("chalkpit_brack", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Tally-clerk Ines", "speaker.ines", map.At(spots[1]), "NPC1",
                    new Color(0.84f, 0.8f, 0.72f), 0.96f, new[] { ("chalkpit_ines", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Seal-warden Simm", "speaker.simm", map.At(spots[2]), "NPC2",
                    new Color(0.68f, 0.7f, 0.74f), 0.98f, new[] { ("chalkpit_simm", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Ket of the Floor", "speaker.ket", map.At(spots[3]), "NPC1",
                    new Color(0.9f, 0.86f, 0.8f), 0.93f, new[] { ("chalkpit_ket", "", "") });
            if (spots.Length > 4)
                MapScene.BuildTalker("Cutter Rill", "speaker.rill", map.At(spots[4]), "NPC1",
                    new Color(0.74f, 0.74f, 0.7f), 1f, new[] { ("chalkpit_rill", "", "") });

            // Pit porters pace the rim between the slab tables and the lane — the pit's last habit.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject porter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!porter)
                    continue;
                porter.name = "Pit porter";
                foreach (var trader in porter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = porter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                porter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(porter, "NPC2", new Color(0.88f, 0.84f, 0.74f));
                ActorSorting.Raise(porter);
                var wanderer = porter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 2.8f;
                wander.FindProperty("walkSpeed").floatValue = 0.9f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(porter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.dess";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "chalkpit_dess";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(porter);
            }
        }

        /// <summary>Pit-jacks — deserters who found chalk dust easier than crust — squat the cut.</summary>
        static void BuildJacks(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Pit-jacks", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("pit_fight", "pit.floor_cleared");

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = 4;
            serialized.FindProperty("unlockedLooks").intValue = 3;
            serialized.FindProperty("healthMultiplier").floatValue = 1.1f;
            serialized.FindProperty("hitDamage").intValue = 12;
            serialized.FindProperty("triggerRadius").floatValue = 4f;
            serialized.FindProperty("spread").floatValue = 3f;
            serialized.FindProperty("bannerKey").stringValue = "event.pit.floor";
            serialized.FindProperty("clearedFlag").stringValue = "pit.floor_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.pit";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.pitClear";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: tally-sticks in the cut, lime bags along the floor,
        /// and the League's seal-stamp up on the derrick platform where the last clerk left it.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: tally spots x3, the seal-stamp on the platform, then the lime bags.
            string[] stickTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot($"Dropped tally-stick", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"pit_tally_{i + 1}", stickTiles[i], "quest_pit_tally",
                    $"pit.tally_{i + 1}", "quest.sq_pit_tallies.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The pit's seal-stamp", map.At(spots[3]), "pit_seal_stamp", "Misc A2_E",
                    "quest_pit_seal", "pit.seal_found", "quest.sq_pit_seal.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.pit.seal");
            string[] bagTiles = { "Misc E6_N", "Misc E7_E", "Misc E8_N", "Misc E6_E" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Split lime bag", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"pit_lime_{i - 3}", bagTiles[i - 4], "quest_pit_lime",
                    $"pit.lime_{i - 3}", "quest.sq_pit_lime.taken");
        }
    }
}
