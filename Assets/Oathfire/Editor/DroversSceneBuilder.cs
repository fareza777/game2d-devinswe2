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
    /// Builds the Drover's Rest, a wayside camp off the east of the toll road: open meadow,
    /// a fire ring at its heart, a wagon row along the north, tents east, and a railed corral
    /// the jackals have been working at night. Side-quest ground — small, open, lived-in.
    /// Map from Tools/compose_drovers.py (composed from scratch — no example-map crop).
    /// </summary>
    public static class DroversSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/DroversRest.unity";

        [MenuItem("Oathfire/Build Drover's Rest Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("drovers");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "DroversRest");

            BuildPeople(map, at);
            if (at.arrivals != null && at.arrivals.Length > 0)
                BuildJackals(map.At(at.arrivals[0]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.droversOut", "");

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
            Debug.Log("[Oathfire] Drover's Rest scene built");
        }

        /// <summary>The camp's people: head drover by the wagons, cook at the ring, harness-boy
        /// at the corral, old Fen under the tents, Whip Nell walking the lane.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_drovers.py: ossel, brigid, pate, fen, nell.
            if (spots.Length > 0)
                MapScene.BuildTalker("Ossel the Head Drover", "speaker.ossel", map.At(spots[0]), "NPC2",
                    new Color(0.78f, 0.72f, 0.62f), 1f, new[] { ("drove_ossel", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Cook Brigid", "speaker.brigid", map.At(spots[1]), "NPC1",
                    new Color(0.86f, 0.8f, 0.7f), 0.97f, new[] { ("drove_brigid", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Harness-boy Pate", "speaker.pate", map.At(spots[2]), "NPC1",
                    new Color(0.9f, 0.86f, 0.74f), 0.88f, new[] { ("drove_pate", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Old Fen", "speaker.fen", map.At(spots[3]), "NPC2",
                    new Color(0.7f, 0.66f, 0.6f), 0.93f, new[] { ("drove_fen", "", "") });
            if (spots.Length > 4)
                MapScene.BuildTalker("Whip Nell", "speaker.nell", map.At(spots[4]), "NPC1",
                    new Color(0.8f, 0.74f, 0.68f), 0.95f, new[] { ("drove_nell", "", "") });

            // A couple of drovers drift between the fire and the wagons all day.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject hand = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!hand)
                    continue;
                hand.name = "Drover";
                foreach (var trader in hand.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = hand.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                hand.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(hand, "NPC1", new Color(0.82f, 0.76f, 0.66f));
                ActorSorting.Raise(hand);
                var wanderer = hand.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 3f;
                wander.FindProperty("walkSpeed").floatValue = 0.85f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(hand.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.drover";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "drove_hand";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(hand);
            }
        }

        /// <summary>The jackal pack working the corral mouth — thinner than bandits, quicker to the throat.</summary>
        static void BuildJackals(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Corral jackals", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("drove_corral", "drove.corral_cleared");

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = 4;
            serialized.FindProperty("unlockedLooks").intValue = 2;
            serialized.FindProperty("healthMultiplier").floatValue = 0.85f;
            serialized.FindProperty("hitDamage").intValue = 9;
            serialized.FindProperty("triggerRadius").floatValue = 4f;
            serialized.FindProperty("spread").floatValue = 3f;
            serialized.FindProperty("bannerKey").stringValue = "event.drove.corral";
            serialized.FindProperty("clearedFlag").stringValue = "drove.corral_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.drove";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.droveCorral";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the camp wants found: three dropped harness pieces and four supper fixings.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: tack pieces x3, supper fixings x4.
            string[] tackTiles = { "Misc A1_E", "Misc B26_N", "Misc B60_N" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Dropped tack", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"drove_tack_{i + 1}", tackTiles[i], "quest_tack",
                    $"drove.tack_{i + 1}", "quest.sq_drove_tack.taken");
            string[] supperTiles = { "Misc E6_N", "Misc E7_E", "Misc E8_N", "Misc C2_N" };
            for (int i = 3; i < 7 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("A supper fixing", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"drove_supper_{i - 2}", supperTiles[i - 3], "quest_supper",
                    $"drove.supper_{i - 2}", "quest.sq_drove_supper.taken");
            // The wagon pins Nell's crew dropped on the night run in.
            if (spots.Length > 0)
                MapScene.BuildSearchSpot("A wagon pin", map.At(spots[0]) + new Vector3(-0.5f, -0.5f, 0f),
                    "drove_pin_1", "Misc B52_E", "quest_wagon_pin",
                    "drove.pin_1", "quest.sq_drove_pins.taken");
            if (spots.Length > 6)
                MapScene.BuildSearchSpot("A wagon pin", map.At(spots[6]) + new Vector3(-0.5f, 0.4f, 0f),
                    "drove_pin_2", "Misc B52_E", "quest_wagon_pin",
                    "drove.pin_2", "quest.sq_drove_pins.taken");
            if (at.arrivals != null && at.arrivals.Length > 1)
                MapScene.BuildSearchSpot("A wagon pin", map.At(at.arrivals[1]) + new Vector3(0.6f, -0.4f, 0f),
                    "drove_pin_3", "Misc B52_E", "quest_wagon_pin",
                    "drove.pin_3", "quest.sq_drove_pins.taken");
        }
    }
}
