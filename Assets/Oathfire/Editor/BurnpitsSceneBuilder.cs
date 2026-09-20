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
    /// Builds the Burnpits, the charcoal-burners' camp at the valley's east edge: a scorched
    /// clearing of smouldering clamps, cordwood ricks along the treeline, and the burners' tents.
    /// Wood-poachers — deserters who learned coal beats crust — strip the burn at dusk. The pans
    /// cannot boil without this camp's coal; the camp cannot stand while the poachers can.
    /// Map from Tools/compose_burnpits.py (composed from scratch — no example-map crop).
    /// </summary>
    public static class BurnpitsSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Burnpits.unity";

        [MenuItem("Oathfire/Build Burnpits Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("burnpits");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Burnpits");

            BuildPeople(map, at);
            BuildPoachers(map.At(at.arrivals[0]));
            BuildSearchSpots(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "Saltpans", "prompt.travel.burnpitsOut", "");

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
            Debug.Log("[Oathfire] Burnpits scene built");
        }

        /// <summary>The burn camp's people: the keeper who tends the clamps and the hands who count the wood.</summary>
        static void BuildPeople(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            // villagers order from compose_burnpits.py: wren at the clamps, pyke on the dragway,
            // penn by the tents, clod at the camp fire, ferra in the stackyard.
            if (spots.Length > 0)
                MapScene.BuildTalker("Kiln-keeper Wren", "speaker.wren", map.At(spots[0]), "NPC1",
                    new Color(0.62f, 0.58f, 0.55f), 1f, new[] { ("burnpits_wren", "", "") });
            if (spots.Length > 1)
                MapScene.BuildTalker("Tally-foreman Pyke", "speaker.pyke", map.At(spots[1]), "NPC2",
                    new Color(0.8f, 0.74f, 0.62f), 0.97f, new[] { ("burnpits_pyke", "", "") });
            if (spots.Length > 2)
                MapScene.BuildTalker("Soot-boy Penn", "speaker.penn", map.At(spots[2]), "NPC1",
                    new Color(0.7f, 0.66f, 0.6f), 0.9f, new[] { ("burnpits_penn", "", "") });
            if (spots.Length > 3)
                MapScene.BuildTalker("Old Clod", "speaker.clod", map.At(spots[3]), "NPC1",
                    new Color(0.84f, 0.8f, 0.72f), 0.94f, new[] { ("burnpits_clod", "", "") });
            if (spots.Length > 4)
                MapScene.BuildTalker("Rack-warden Ferra", "speaker.ferra", map.At(spots[4]), "NPC2",
                    new Color(0.72f, 0.68f, 0.62f), 1f, new[] { ("burnpits_ferra", "", "") });

            // Carters haul coal to the pans and wood back to the stacks; the camp only looks like
            // work because they keep moving.
            foreach (MapScene.CellRef home in at.stone ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject carter = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", map.At(home));
                if (!carter)
                    continue;
                carter.name = "Pits carter";
                foreach (var trader in carter.GetComponentsInChildren<SmallScale.FantasyKingdomTileset.TraderComponent>())
                    Object.DestroyImmediate(trader);
                var trigger = carter.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = 1.8f;
                carter.AddComponent<WorldInteractable>();
                MapScene.GiveNpcBody(carter, "NPC1", new Color(0.82f, 0.76f, 0.62f));
                ActorSorting.Raise(carter);
                var wanderer = carter.AddComponent<NpcWanderer>();
                var wander = new SerializedObject(wanderer);
                wander.FindProperty("radius").floatValue = 2.4f;
                wander.FindProperty("walkSpeed").floatValue = 0.9f;
                wander.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(carter.AddComponent<NpcDialogue>());
                serialized.FindProperty("speakerKey").stringValue = "speaker.kell";
                SerializedProperty conversations = serialized.FindProperty("conversations");
                conversations.arraySize = 1;
                conversations.GetArrayElementAtIndex(0).FindPropertyRelative("scriptId").stringValue = "burnpits_kell";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MapScene.GiveOfferMarker(carter);
            }

            // When the poachers are gone the camp finishes the interrupted burn: fresh cordwood
            // appears by the wagon, stacked for the next load out.
            if (spots.Length > 0)
            {
                var yard = new GameObject("Stacked cordwood");
                yard.transform.position = map.At(spots[0]) + new Vector3(1.4f, -0.5f, 0f);
                MapScene.BuildProp("Cordwood pile", "Misc B59_E", yard.transform.position, yard.transform, solid: false, strikeable: false);
                MapScene.Gate(yard, "sq.pits.done");
            }
        }

        /// <summary>Wood-poachers out of the treeline strip the clamps and carry off cordwood at dusk.</summary>
        static void BuildPoachers(Vector3 position)
        {
            GameObject[] prefabs = TradeRoadSceneBuilder.EnemyPrefabs();
            var fight = new GameObject("Wood-poachers", typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("char_fight", "char.pits_cleared");

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
            serialized.FindProperty("bannerKey").stringValue = "event.char.pits";
            serialized.FindProperty("clearedFlag").stringValue = "char.pits_cleared";
            serialized.FindProperty("counterKey").stringValue = "kills.char";
            serialized.FindProperty("clearedToastKey").stringValue = "toast.charPits";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(KeepSceneBuilder.DeserterLooks());
        }

        /// <summary>What the work wants found: cord-tallies in the stackyard, the keeper's rod at the burn ground, spilled coal.</summary>
        static void BuildSearchSpots(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            // timber order: cord-tallies x3 in the stackyard, the keeper's rod by the clamps,
            // then four spilled heaps around the burn ground.
            string[] stickTiles = { "Misc A1_E", "Misc B26_N", "Misc A2_E" };
            for (int i = 0; i < 3 && i < spots.Length; i++)
                MapScene.BuildSearchSpot($"Dropped cord-tally", map.At(spots[i]) + new Vector3(0.35f * (i - 1), 0.2f, 0f),
                    $"char_tally_{i + 1}", stickTiles[i], "quest_cord_tally",
                    $"char.tally_{i + 1}", "quest.sq_cord_tallies.taken");
            if (spots.Length > 3)
                MapScene.BuildSearchSpot("The keeper's rod", map.At(spots[3]), "char_rod", "Misc B26_N",
                    "quest_keeper_rod", "char.rod_found", "quest.sq_keeper_rod.taken",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.char.rod");
            string[] heapTiles = { "Misc C3_N", "Misc C3_E", "Misc C4_N", "Misc C4_W" };
            for (int i = 4; i < 8 && i < spots.Length; i++)
                MapScene.BuildSearchSpot("Spilled coal", map.At(spots[i]) + new Vector3(0.3f, -0.15f, 0f),
                    $"char_coal_{i - 3}", heapTiles[i - 4], "quest_char_coal",
                    $"char.coal_{i - 3}", "quest.sq_char_coal.taken");
        }
    }
}
