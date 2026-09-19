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
    /// Builds the Saltpans, the Salt League's brine-works: an open camp of pale dry pans, a boiling
    /// row of cauldrons, canvas tents, and the salt track the wagons come in on. Salt-thieves —
    /// deserters gone soft on easy crust — squat the pans at night. The League counts every load;
    /// the tents count what the count forgot.
    /// Map from Tools/compose_saltpans.py (composed from scratch — no example-map crop).
    /// </summary>
    public static class SaltpansSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Saltpans.unity";

        [MenuItem("Oathfire/Build Saltpans Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("saltpans");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Saltpans");

            BuildPeople(map, at);
            BuildThieves(map.At(at.arrivals[0]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.saltpansOut", "");

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
            Debug.Log("[Oathfire] Saltpans scene built");
        }

        /// <summary>The works' people: the League counts, the tents wait.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_saltpans.py: factor, pan-keeper, mirren, tess, lene.
            if (spots.Length > 0)
                MapScene.BuildTalker("Factor Hask", "speaker.hask", map.At(spots[0]), "NPC2",
                    new Color(0.84f, 0.76f, 0.58f), 1f, new[] { ("saltpans_hask", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Pan-keeper Collum", "speaker.collum", map.At(spots[1]), "NPC1",
                    new Color(0.8f, 0.74f, 0.62f), 0.97f, new[] { ("saltpans_collum", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Old Mirren", "speaker.mirren", map.At(spots[2]), "NPC1",
                    new Color(0.86f, 0.8f, 0.72f), 0.94f, new[] { ("saltpans_mirren", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Tess of the Tents", "speaker.tess", map.At(spots[3]), "NPC1",
                    new Color(0.72f, 0.72f, 0.68f), 0.95f, new[] { ("saltpans_tess", "", "") });
            if (spots.Length > 4)
                MapScene.BuildTalker("Watch-lene", "speaker.lene", map.At(spots[4]), "NPC2",
                    new Color(0.62f, 0.66f, 0.72f), 1f, new[] { ("saltpans_lene", "", "") });

            // Porters carry heap to scale to ledger; they are the only reason the flat looks like work.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject porter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!porter)
                    continue;
                porter.name = "Pans porter";
                foreach (var trader in porter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = porter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                porter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(porter, "NPC1", new Color(0.9f, 0.82f, 0.66f));
                ActorSorting.Raise(porter);
                var wanderer = porter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 2.6f;
                wander.FindProperty("walkSpeed").floatValue = 0.95f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(porter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.boru";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "saltpans_boru";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(porter);
            }

            // When the spill work is done the gleaner tents settle in earnest: a boy who now says
            // the Warden's name like it is a roof.
            if (spots.Length > 3)
            {
                var camp = new GameObject("Gleaner corner");
                camp.transform.position = map.At(spots[3]) + new Vector3(1.2f, -0.4f, 0f);
                MapScene.BuildProp("Tent boy", "Misc B52_E", camp.transform.position, camp.transform, solid: false, strikeable: false);
                MapScene.Gate(camp, "sq.spillage.done");
            }
        }

        /// <summary>Deserters gone soft on easy crust squat the pans at night, carrying off what dries.</summary>
        static void BuildThieves(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Salt thieves", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("salt_fight", "salt.pans_cleared");

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = 5;
            serialized.FindProperty("unlockedLooks").intValue = 3;
            serialized.FindProperty("healthMultiplier").floatValue = 0.95f;
            serialized.FindProperty("hitDamage").intValue = 11;
            serialized.FindProperty("triggerRadius").floatValue = 4f;
            serialized.FindProperty("spread").floatValue = 3f;
            serialized.FindProperty("bannerKey").stringValue = "event.salt.pans";
            serialized.FindProperty("clearedFlag").stringValue = "salt.pans_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.salt";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.saltPans";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: tally-sticks by the pans, spill on the crust, the store key by the boiling row.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: tally spots x3, the store key — then the spilled heaps around the pans.
            string[] stickTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot($"Dropped tally-stick", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"salt_tally_{i + 1}", stickTiles[i], "quest_salt_tally",
                    $"salt.tally_{i + 1}", "quest.sq_salt_tallies.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The store key", map.At(spots[3]), "salt_store_key", "Misc A2_E",
                    "quest_store_key", "salt.store_key_found", "quest.sq_store_key.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.salt.key");
            string[] heapTiles = { "Misc E6_N", "Misc E7_E", "Misc E8_N", "Misc E6_E" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Spilled salt", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"salt_spill_{i - 3}", heapTiles[i - 4], "quest_salt_spill",
                    $"salt.spill_{i - 3}", "quest.sq_salt_spill.taken");
        }
    }
}
