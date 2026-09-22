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
    /// Builds the Windrest, the mill knoll on the rise north of Rennfall: a green hill of stooked
    /// grain under the League's windmill, its cobbled yard cluttered with the work of grinding —
    /// sacks, the winnowing frame, the spare stone, the loaded cart that cannot risk the lane.
    /// Grain-raiders squat the field rows at dusk. Map from Tools/compose_windrest.py
    /// (composed from scratch — no example-map crop).
    /// </summary>
    public static class WindrestSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Windrest.unity";

        [MenuItem("Oathfire/Build Windrest Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("windrest");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Windrest");

            BuildPeople(map, at);
            BuildRaiders(map.At(at.arrivals[0]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "Rennfall", "prompt.travel.windrestOut", "");

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
            Debug.Log("[Oathfire] Windrest scene built");
        }

        /// <summary>The mill hill's people: the miller, his count-keeping wife, the mill-lad, the League's weigher.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_windrest.py: corl at the mill door, bryd at the racks,
            // tam in the field, odo at the weigh canopy.
            if (spots.Length > 0)
                MapScene.BuildTalker("Miller Corl", "speaker.corl", map.At(spots[0]), "NPC2",
                    new Color(0.88f, 0.8f, 0.6f), 1f, new[] { ("windrest_corl", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Bryd of the Count", "speaker.bryd", map.At(spots[1]), "NPC1",
                    new Color(0.76f, 0.72f, 0.88f), 0.97f, new[] { ("windrest_bryd", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Tam the Mill-lad", "speaker.tam", map.At(spots[2]), "NPC1",
                    new Color(0.9f, 0.86f, 0.7f), 0.9f, new[] { ("windrest_tam", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Odo the Weigher", "speaker.odo", map.At(spots[3]), "NPC2",
                    new Color(0.7f, 0.74f, 0.78f), 1f, new[] { ("windrest_odo", "", "") });

            // Odle the carter walks the lane between cart and gate — the ruts remember wheels
            // because he keeps walking them.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject carter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!carter)
                    continue;
                carter.name = "Odle the Carter";
                foreach (var trader in carter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = carter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                carter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(carter, "NPC1", new Color(0.86f, 0.78f, 0.58f));
                ActorSorting.Raise(carter);
                var wanderer = carter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 3.4f;
                wander.FindProperty("walkSpeed").floatValue = 1.1f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(carter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.odle";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "windrest_odle";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(carter);
            }

            // When the field stands clear the mill yard remembers it: a sack-load finally leaves
            // the yard by the cart, stacked for the ride down the lane.
            if (spots.Length > 0)
            {
                var yard = new GameObject("Bound sacks");
                yard.transform.position = map.At(spots[0]) + new Vector3(1.4f, -0.5f, 0f);
                MapScene.BuildProp("Sack pile", "Misc B15_E", yard.transform.position, yard.transform, solid: false, strikeable: false);
                MapScene.Gate(yard, "sq.wind.done");
            }
        }

        /// <summary>Grain-raiders — deserters mostly — rush the stook rows when the light goes long.</summary>
        static void BuildRaiders(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Grain-raiders", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("mill_fight", "mill.field_cleared");

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = 4;
            serialized.FindProperty("unlockedLooks").intValue = 3;
            serialized.FindProperty("healthMultiplier").floatValue = 1f;
            serialized.FindProperty("hitDamage").intValue = 12;
            serialized.FindProperty("triggerRadius").floatValue = 4f;
            serialized.FindProperty("spread").floatValue = 3f;
            serialized.FindProperty("bannerKey").stringValue = "event.mill.field";
            serialized.FindProperty("clearedFlag").stringValue = "mill.field_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.mill";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.millField";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: Bryd's tally-sticks in the stooks, Tam's spilled sacks on the lane.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: tally-sticks x3 among the stook rows, then spilled meal sacks x4 down the lane.
            string[] stickTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot($"Dropped tally-stick", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"mill_tally_{i + 1}", stickTiles[i], "quest_mill_tally",
                    $"mill.tally_{i + 1}", "quest.sq_wind_tallies.taken");
            string[] sackTiles = { "Misc B15_N", "Misc B15_E", "Misc B14_N", "Misc B15_N" };
            for (int i = 3; i < 7 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Spilled meal sack", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"mill_sack_{i - 2}", sackTiles[i - 3], "quest_mill_sack",
                    $"mill.sack_{i - 2}", "quest.sq_wind_sacks.taken");
            // The miller's winter ring, and a raider's bow stashed by the field.
            if (at.villagers != null && at.villagers.Length > 2)
                MapScene.BuildSearchSpot("The miller's ring", map.At(at.villagers[2]) + new Vector3(0.75f, -0.35f, 0f),
                    "cache_mill_ring", "Chest A1_E", "ring_copper", "cache.mill.ring", "");
            if (at.arrivals != null && at.arrivals.Length > 2)
                MapScene.BuildSearchSpot("The raider's stash", map.At(at.arrivals[2]) + new Vector3(0.7f, 0.3f, 0f),
                    "cache_mill_bow", "Chest A1_E", "bow_yew", "cache.mill.bow", "",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
        }
    }
}
