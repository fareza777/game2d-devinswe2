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
    /// Builds the trade road south of Rennfall: cobbles through woodland, a farmstead, hay fields and
    /// wayside ruins. It is where the chapter's travel happens, and where the road closes behind you.
    /// </summary>
    public static class TradeRoadSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/TradeRoad.unity";

        [MenuItem("Oathfire/Build Trade Road Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("trade_road");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "TradeRoad");

            SetUpAmbush(map.At(at.hearth));
            SetUpWanderingEnemies();
            SetUpWayfinding(map, at);
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
            Debug.Log("[Oathfire] Trade Road scene built");
        }

        /// <summary>The bend in the road where the Greymarch men are waiting.</summary>
        static void SetUpAmbush(Vector3 position)
        {
            var ambush = new GameObject("RoadAmbush", typeof(RoadAmbush), typeof(QuestSpot));
            ambush.transform.position = position;
            ambush.GetComponent<QuestSpot>().Configure("road_ambush", "act2.road_ambush_cleared");

            GameObject[] prefabs = EnemyPrefabs();

            var serialized = new SerializedObject(ambush.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // The Greymarch men on the road are people, not the dead: hooded and robed bodies in road leathers.
            ambush.GetComponent<RoadAmbush>().SetLooks(BanditLooks());
        }

        internal static World.ActorLook[] BanditLooks() => new[]
        {
            // Plain to strange: the ambush uses the first two, the road unlocks the rest as the story moves.
            MapScene.Look("Cutthroat", "NPC2", new Color(0.78f, 0.7f, 0.62f), 0.95f, 1.05f, "kills.bandit"),
            MapScene.Look("Unmarked boots", "NPC1", new Color(0.55f, 0.52f, 0.5f), 0.95f, 1.05f, "kills.bandit"),
            MapScene.Look("Road warden turned", "NPC2", new Color(0.62f, 0.64f, 0.7f), 1f, 1.12f, "kills.bandit"),
            MapScene.Look("Hedge caster", "NPC1", new Color(0.7f, 0.62f, 0.78f), 0.92f, 1.02f, "kills.bandit"),
            MapScene.Look("Marshfoot", "NPC2", new Color(0.58f, 0.64f, 0.52f), 0.98f, 1.1f, "kills.bandit"),
        };

        /// <summary>What walks the road when nothing has been scripted to.</summary>
        static void SetUpWanderingEnemies()
        {
            var wild = new GameObject("WildernessEncounters", typeof(WildernessEncounters));
            var serialized = new SerializedObject(wild.GetComponent<WildernessEncounters>());
            SerializedProperty list = serialized.FindProperty("enemies");
            GameObject[] prefabs = EnemyPrefabs();
            list.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Wanderers are mostly road thieves, with the odd ash-grey dead thing come down from the hills.
            World.ActorLook[] bandits = BanditLooks();
            wild.GetComponent<WildernessEncounters>().SetLooks(new[]
            {
                bandits[0], bandits[1], bandits[2], bandits[3],
                MapScene.Look("Hill dead", "", new Color(0.7f, 0.74f, 0.86f), 0.9f, 1.05f),
            });
        }

        internal static GameObject[] EnemyPrefabs()
        {
            string[] paths =
            {
                MapScene.Prefabs + "Enemy/Regular/SkeletonWarriorPrefab.prefab",
                MapScene.Prefabs + "Enemy/Regular/SkeletonMagePrefab.prefab",
            };
            GameObject[] prefabs = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p).ToArray();
            if (prefabs.Length == 0)
                Debug.LogError("[Oathfire] No enemy prefabs found for the trade road");
            return prefabs;
        }

        /// <summary>The road home, and something worth stopping for on the way.</summary>
        static void SetUpWayfinding(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            BuildExit(map.At(at.board), "Rennfall", "prompt.travel.rennfall", "act2.road_travelled");
            // The far end of the road: Greymarch, where the gate closes in Chapter 1.
            if (at.arrivals != null && at.arrivals.Length > 0)
                BuildExit(map.At(at.arrivals[0]), "Greymarch", "prompt.travel.greymarch", "act2.reached_greymarch");

            // A goat track down into Blackthorn Hollow, where the raiders camp. It is only a track once
            // Corporal Dace has said where to look; before that it is a post nobody reads.
            if (at.trail != null)
            {
                var trail = new GameObject("Hidden trail");
                trail.transform.position = map.At(at.trail);
                GameObject exit = MapScene.BuildExit(map.At(at.trail), "Hollow", "prompt.travel.hollow", "act2.found_hollow");
                exit.transform.SetParent(trail.transform, true);
                MapScene.BuildProp("Trail post", "Misc B52_E", map.At(at.trail) + new Vector3(0.45f, 0.2f, 0f), exit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(trail, "act2.dace_briefed");
            }

            // The old keep road, overgrown since the garrison walked off it. It reads as a road again only
            // once Vesna's ledger says someone has been making deliveries up there.
            if (at.trail2 != null)
            {
                var keepRoad = new GameObject("Keep road");
                keepRoad.transform.position = map.At(at.trail2);
                GameObject keepExit = MapScene.BuildExit(map.At(at.trail2), "Keep", "prompt.travel.keep", "act2.found_keep");
                keepExit.transform.SetParent(keepRoad.transform, true);
                MapScene.BuildProp("Trail post", "Misc B52_E", map.At(at.trail2) + new Vector3(-0.45f, 0.2f, 0f), keepExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(keepRoad, "act2.keep_rumoured");
            }

            // The salt track to the pans, the League's brine-works: an open camp, short of hands.
            // It is a marked turning only once Maren has read the manifest and sent word of the
            // League asking the valley road for wardens.
            if (at.trail3 != null)
            {
                var tollLane = new GameObject("Salt track");
                tollLane.transform.position = map.At(at.trail3);
                GameObject pansExit = MapScene.BuildExit(map.At(at.trail3), "Saltpans", "prompt.travel.saltpans", "act2.found_saltpans");
                pansExit.transform.SetParent(tollLane.transform, true);
                MapScene.BuildProp("Salt post", "Misc B53_E", map.At(at.trail3) + new Vector3(0.45f, -0.2f, 0f), pansExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(tollLane, "act2.saltpans_rumoured");
            }

            // The chalk spur: a dust-white turning to the League's abandoned lime pit. It reads as
            // a turning only once the pay-roll names it — Maren's third writ sends the Warden up it.
            if (at.trail4 != null)
            {
                var chalkSpur = new GameObject("Chalk spur");
                chalkSpur.transform.position = map.At(at.trail4);
                GameObject pitExit = MapScene.BuildExit(map.At(at.trail4), "Chalkpit", "prompt.travel.chalkpit", "act2.found_chalkpit");
                pitExit.transform.SetParent(chalkSpur.transform, true);
                MapScene.BuildProp("Lime post", "Misc B53_E", map.At(at.trail4) + new Vector3(-0.45f, -0.2f, 0f), pitExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(chalkSpur, "act2.chalkpit_rumoured");
            }

            // The old toll road north: it is a turning only once the charter warden has read the die
            // and named the ruined toll fort where the false coin is being washed.
            if (at.trail5 != null)
            {
                var tollSpur = new GameObject("Toll spur");
                tollSpur.transform.position = map.At(at.trail5);
                GameObject tollExit = MapScene.BuildExit(map.At(at.trail5), "Tollbank", "prompt.travel.tollbank", "act3.found_tollbank");
                tollExit.transform.SetParent(tollSpur.transform, true);
                MapScene.BuildProp("Toll post", "Misc B52_E", map.At(at.trail5) + new Vector3(-0.45f, 0.3f, 0f), tollExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(tollSpur, "act3.tollbank_marked");
            }

            // The drovers' turning: Tess's wagons come down out of the rest camp east of the road.
            // Her asking after them — once the spilled salt is gathered — marks it on the map.
            if (at.trail6 != null)
            {
                var droverSpur = new GameObject("Drovers' turning");
                droverSpur.transform.position = map.At(at.trail6);
                GameObject restExit = MapScene.BuildExit(map.At(at.trail6), "DroversRest", "prompt.travel.drovers", "act2.found_drovers");
                restExit.transform.SetParent(droverSpur.transform, true);
                MapScene.BuildProp("Drover's post", "Misc B53_E", map.At(at.trail6) + new Vector3(-0.55f, 0.25f, 0f), restExit.transform,
                    solid: false, strikeable: false);
                MapScene.Gate(droverSpur, "act2.drovers_rumoured");
            }

            if (at.search != null)
                MapScene.BuildSearchSpot("Wick's lamp", map.At(at.search), "search_lamp", "Misc A2_E", "quest_wick_lamp",
                    "sq.lamp.found", "quest.sq_wicks_lamp.taken");

            // A courier's star spilled beside the toll road — whoever buried it left watchers.
            if (at.brann != null)
                MapScene.BuildSearchSpot("The courier's cache", map.At(at.brann) + new Vector3(0.85f, -0.4f, 0f),
                    "cache_road_star", "Chest A1_E", "trinket_road_charm", "cache.road.star", "",
                    EnemyPrefabs(), 2, "event.cacheGuard");

            // Wayside salvage: the carts that did not make it are worth searching.
            var parent = new GameObject("Salvage").transform;
            string[] wrecks = { "Misc B5_E", "Misc B8_E" };
            int index = 0;
            foreach (MapScene.CellRef cell in (at.timber ?? System.Array.Empty<MapScene.CellRef>()).Take(2))
            {
                GameObject node = MapScene.BuildProp("Wayside salvage", wrecks[index++ % wrecks.Length], map.At(cell), parent,
                    solid: true, strikeable: true);
                node.AddComponent<WorldInteractable>();

                var serialized = new SerializedObject(node.AddComponent<GatherNode>());
                serialized.FindProperty("itemId").stringValue = "mat_timber";
                serialized.FindProperty("promptKey").stringValue = "prompt.salvage";
                serialized.FindProperty("amount").intValue = 4;
                serialized.FindProperty("respawnSeconds").floatValue = 90f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildExit(Vector3 position, string destination, string promptKey, string flag)
        {
            var exit = new GameObject($"Road to {destination}", typeof(CircleCollider2D), typeof(WorldInteractable), typeof(SceneExit));
            exit.transform.position = position;

            var serialized = new SerializedObject(exit.GetComponent<SceneExit>());
            serialized.FindProperty("destinationScene").stringValue = destination;
            serialized.FindProperty("promptKey").stringValue = promptKey;
            serialized.FindProperty("setsFlag").stringValue = flag;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
