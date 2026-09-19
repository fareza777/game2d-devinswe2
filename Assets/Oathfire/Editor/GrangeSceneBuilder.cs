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
    /// Builds Saltwold Grange, the Salt League's walled weigh-court: a gated lane past the wall where
    /// the gleaner families wait, a weigh-floor under the manor terrace, and the barn bays where
    /// deserter-foragers have been slipping in at night. The League counts every sack; the valley
    /// counts what it owes.
    /// Map from Tools/build_maps.py (region "grange").
    /// </summary>
    public static class GrangeSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Grange.unity";

        [MenuItem("Oathfire/Build Saltwold Grange Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("grange");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Grange");

            BuildPeople(map, at);
            BuildForagers(map.At(at.arrivals[0]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.grangeOut", "");

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
            Debug.Log("[Oathfire] Saltwold Grange scene built");
        }

        /// <summary>The weigh-court's people: the League counts, the lane waits.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from build_maps.py: factor, reeve, mirren, tess, lene.
            if (spots.Length > 0)
                MapScene.BuildTalker("Factor Hask", "speaker.hask", map.At(spots[0]), "NPC2",
                    new Color(0.84f, 0.76f, 0.58f), 1f, new[] { ("grange_hask", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Reeve Collum", "speaker.collum", map.At(spots[1]), "NPC1",
                    new Color(0.8f, 0.74f, 0.62f), 0.97f, new[] { ("grange_collum", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Old Mirren", "speaker.mirren", map.At(spots[2]), "NPC1",
                    new Color(0.86f, 0.8f, 0.72f), 0.94f, new[] { ("grange_mirren", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Tess of the Lane", "speaker.tess", map.At(spots[3]), "NPC1",
                    new Color(0.72f, 0.72f, 0.68f), 0.95f, new[] { ("grange_tess", "", "") });
            if (spots.Length > 4)
                MapScene.BuildTalker("Gate-warden Lene", "speaker.lene", map.At(spots[4]), "NPC2",
                    new Color(0.62f, 0.66f, 0.72f), 1f, new[] { ("grange_lene", "", "") });

            // Porters carry sack to scale to ledger; they are the only reason the floor looks like work.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject porter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!porter)
                    continue;
                porter.name = "Grange porter";
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
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "grange_boru";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(porter);
            }

            // When the spillage work is done the lane camp settles in earnest: a second fire, bedrolls,
            // and a boy who now says the Warden's name like it is a roof.
            if (spots.Length > 3)
            {
                var camp = new GameObject("Gleaner camp");
                camp.transform.position = map.At(spots[3]) + new Vector3(1.2f, -0.4f, 0f);
                MapScene.BuildProp("Second fire", "Misc C8_S", camp.transform.position, camp.transform, solid: false, strikeable: false);
                MapScene.BuildProp("Bedrolls", "Misc B3_N", camp.transform.position + new Vector3(0.8f, 0.35f, 0f), camp.transform, solid: false, strikeable: false);
                MapScene.Gate(camp, "sq.spillage.done");
            }
        }

        /// <summary>Foragers of the broken Line squat the opened bays at night, taking sacks by the armful.</summary>
        static void BuildForagers(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Bay foragers", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("grange_fight", "grange.bays_cleared");

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
            serialized.FindProperty("bannerKey").stringValue = "event.grange.bays";
            serialized.FindProperty("clearedFlag").stringValue = "grange.bays_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.grange";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.grangeBays";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: tally-sticks in the bays, spillage in the court, the silo key on the terrace.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: tally bays x2, sack row, terrace key — then the spilled sacks around the court.
            string[] stickTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot($"Dropped tally-stick", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"grange_tally_{i + 1}", stickTiles[i], "quest_grange_tally",
                    $"grange.tally_{i + 1}", "quest.sq_tally_sticks.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The silo key", map.At(spots[3]), "grange_silo_key", "Misc A2_E",
                    "quest_silo_key", "grange.silo_key_found", "quest.sq_silo_key.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.grange.key");
            string[] sackTiles = { "Misc B45_N", "Misc B44_E", "Misc B45_N", "Misc B44_E" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Spilled grain", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"grange_spill_{i - 3}", sackTiles[i - 4], "quest_spill_sack",
                    $"grange.spill_{i - 3}", "quest.sq_spillage.taken");
        }
    }
}
