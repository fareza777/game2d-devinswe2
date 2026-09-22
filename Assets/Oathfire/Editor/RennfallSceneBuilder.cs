using System.Collections.Generic;
using System.Linq;
using Oathfire.Progress;
using Oathfire.World;
using SmallScale.FantasyKingdomTileset;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Builds Rennfall from Docs/rennfall_map.json: a real village of houses, market stalls, walls and
    /// roads, with the oathfire in its square. The map is cut from the tileset's own example town by
    /// Tools/build_maps.py, so the layout is the pack artist's and the staging is ours.
    /// </summary>
    public static class RennfallSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Rennfall.unity";

        [MenuItem("Oathfire/Build Rennfall Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("rennfall");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Rennfall");

            SetUpHearth(map.At(at.hearth));
            SetUpVillagers(map, at);
            SetUpSideWork(map, at);
            SetUpNoticeBoard(map.At(at.board));
            SetUpRoadOut(map.At(at.roadOut));
            SetUpMillLane(map, at);
            SetUpGathering(map, at);
            SetUpBuildSites(map, at.plots);
            SetUpNight(map.At(at.hearth));
            SetUpWorldEvents();
            MapScene.SetUpUi(includeContractBoard: true);

            new GameObject("PlayerState", typeof(PlayerState));
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Oathfire] Rennfall scene built");
        }

        static void SetUpHearth(Vector3 position)
        {
            var hearth = new GameObject("Oathfire", typeof(CircleCollider2D), typeof(WorldInteractable), typeof(OathfireHearth));
            hearth.transform.position = position;

            GameObject flame = MapScene.InstantiatePrefab(MapScene.Prefabs + "Props/FireProp.prefab", position);
            if (flame)
            {
                flame.name = "Flame";
                flame.transform.SetParent(hearth.transform, true);
            }

            var lightGo = new GameObject("HearthLight", typeof(Light2D));
            lightGo.transform.SetParent(hearth.transform, false);
            Light2D light = lightGo.GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.68f, 0.32f);
            light.intensity = 0.25f;
            light.pointLightOuterRadius = 2f;
            light.pointLightInnerRadius = 0.5f;

            var serialized = new SerializedObject(hearth.GetComponent<OathfireHearth>());
            serialized.FindProperty("flameVisual").objectReferenceValue = flame;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The pieces side work needs in the valley: a place to search, and lines that only side work unlocks.</summary>
        static void SetUpSideWork(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            if (at.search != null)
            {
                GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "Enemy/Regular/SkeletonWarriorPrefab.prefab");
                MapScene.BuildSearchSpot("Crane's journal", map.At(at.search), "search_records", "Misc A1_E", "quest_crane_journal",
                    "act1.found_crane_journal", "quest.q_records_house.taken", new[] { skeleton }, 3, "toast.graveStir");
            }
            MapScene.PrependConversation(GameObject.Find("Hesketh the Cooper"), "villager_hesketh_pot", "quest.sq_stew.taken", "sq.stew.pot");
            MapScene.PrependConversation(GameObject.Find("Brann Hollowell"), "brann_letter", "quest.sq_oonas_letter.taken",
                "sq.letter.delivered", "quest_oona_letter");
            // Carrying the keep's manifest home makes it hers to deal with.
            MapScene.PrependConversation(GameObject.Find("Inspector Maren"), "keep_manifest", "act2.manifest_found",
                "act2.manifest_delivered", "quest_keep_manifest");
            // The pay-roll answers the manifest's last question: where the coin came from. It names
            // the League's dead lime pit — and sends the Warden to see who is cutting it now.
            MapScene.PrependConversation(GameObject.Find("Inspector Maren"), "chalkpit_writ", "sq.payroll.done",
                "act2.chalkpit_rumoured");
            // And once it is delivered, the manifest names the League's waylaid grain train — her writ
            // marks the toll lane on the map. (Prepended last so it fires before the chalk writ.)
            MapScene.PrependConversation(GameObject.Find("Inspector Maren"), "saltpans_writ", "act2.manifest_delivered",
                "act2.saltpans_rumoured");
            // The master die out of the coiners' yard is hers to read — and to answer for.
            MapScene.PrependConversation(GameObject.Find("Inspector Maren"), "stamp_die", "act2.die_taken",
                "act2.die_delivered", "quest_stamp_die");
            // The true ledger out of the Tollbank is hers to open on the lamp — and to answer for.
            MapScene.PrependConversation(GameObject.Find("Inspector Maren"), "maren_ledger", "act3.ledger_taken",
                "act3.ledger_closed", "quest_true_ledger");
            // Once the village has survived a night, Winna's flour barrel runs low: the mill's
            // cart hasn't come down the north lane, and her asking puts the knoll on the map.
            MapScene.PrependConversation(GameObject.Find("Old Winna"), "windrest_writ", "act1.night1_survived",
                "act1.windrest_rumoured");
            // A spare shield left in the lane by the wardens who never came back for it.
            if (at.arrivals != null && at.arrivals.Length > 0)
                MapScene.BuildSearchSpot("The wardens' spare", map.At(at.arrivals[0]) + new Vector3(0.8f, -0.4f, 0f),
                    "cache_rennfall", "Chest A1_E", "shield_oak", "cache.rennfall", "");
        }

        static void SetUpVillagers(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            // Brann answers differently as the chapter turns: first meeting, after the fire, after the night.
            BuildVillager("Brann Hollowell", "speaker.brann", map.At(at.brann), null, ("NPC2", new Color(1f, 0.93f, 0.84f)),
                new[]
                {
                    ("prologue_intro", "", "prologue.met_brann"),
                    ("hearth_lit", "act1.oathfire_lit", "act1.night1_ready"),
                    ("after_first_night", "act1.night1_cleared", "act1.rite_performed"),
                });

            // Maren rides in only once Brann's rite sends word to the Marshals. Until then she is not here,
            // and neither is the work that depends on her.
            BuildVillager("Inspector Maren", "speaker.maren", map.At(at.maren), "act1.maren_incoming", ("NPC2", new Color(0.82f, 0.88f, 1f)),
                new[] { ("maren_arrival", "", "act1.maren_arrived") });

            // The people the settlement is actually for. They say one thing each, and that is enough for the
            // square to feel inhabited rather than staffed.
            (string name, string speaker, string script, string rig, Color tint)[] residents =
            {
                ("Sabel Orr", "speaker.sabel", "merchant_sabel", "NPC1", new Color(1f, 0.9f, 0.72f)),
                ("Iskra Vell", "speaker.iskra", "villager_iskra", "NPC1", new Color(0.9f, 1f, 0.9f)),
                ("Hesketh the Cooper", "speaker.hesketh", "villager_hesketh", "NPC2", new Color(1f, 0.86f, 0.76f)),
                ("Old Winna", "speaker.winna", "villager_winna", "NPC1", new Color(0.92f, 0.9f, 0.98f)),
                ("Gethin the Digger", "speaker.gethin", "villager_gethin", "NPC2", new Color(0.88f, 0.84f, 0.78f)),
                ("Wick", "speaker.wick", "villager_wick", "NPC1", new Color(1f, 1f, 1f)),
            };
            MapScene.CellRef[] spots = at.villagers ?? System.Array.Empty<MapScene.CellRef>();
            for (int i = 0; i < residents.Length && i < spots.Length; i++)
                BuildVillager(residents[i].name, residents[i].speaker, map.At(spots[i]), null, (residents[i].rig, residents[i].tint),
                    new[] { (residents[i].script, "", "") });
        }

        /// <summary>
        /// One talkable villager. When <paramref name="requiresFlag"/> is set the character hangs off a gate
        /// object, so the scene can hold people who have not arrived yet without spawning them at runtime.
        /// </summary>
        static void BuildVillager(string name, string speakerKey, Vector3 position, string requiresFlag, (string rig, Color tint) body,
            (string script, string requires, string consumed)[] conversationData)
        {
            GameObject villager = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", position);
            if (!villager)
                return;
            villager.name = name;
            foreach (var trader in villager.GetComponentsInChildren<TraderComponent>())
                Object.DestroyImmediate(trader);

            var trigger = villager.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            villager.AddComponent<WorldInteractable>();
            MapScene.GiveNpcBody(villager, body.rig, body.tint);
            ActorSorting.Raise(villager);
            // People are not all one height: the boy who fills the lamps, the old weaver, the cooper.
            villager.transform.localScale *= name switch
            {
                "Wick" => 0.72f,
                "Old Winna" => 0.92f,
                "Hesketh the Cooper" => 1.08f,
                "Brann Hollowell" => 1.04f,
                _ => 1f,
            };

            // People who live here do not stand rooted to one tile: everyone drifts a little around their
            // post. Brann keeps closest to the hearth he tends; Wick the lamp boy covers the most ground.
            var wanderer = villager.AddComponent<World.NpcWanderer>();
            var wander = new SerializedObject(wanderer);
            wander.FindProperty("radius").floatValue = name switch
            {
                "Brann Hollowell" => 1.6f,
                "Inspector Maren" => 2.0f,
                "Wick" => 1.9f,
                "Old Winna" => 1.8f,
                _ => 2.4f,
            };
            wander.FindProperty("walkSpeed").floatValue = name == "Wick" ? 1.15f : 0.8f;
            wander.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(villager.AddComponent<NpcDialogue>());
            serialized.FindProperty("speakerKey").stringValue = speakerKey;
            SerializedProperty conversations = serialized.FindProperty("conversations");
            conversations.arraySize = conversationData.Length;
            for (int i = 0; i < conversationData.Length; i++)
            {
                SerializedProperty entry = conversations.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("scriptId").stringValue = conversationData[i].script;
                entry.FindPropertyRelative("requiresFlag").stringValue = conversationData[i].requires;
                entry.FindPropertyRelative("consumedFlag").stringValue = conversationData[i].consumed;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            MapScene.GiveOfferMarker(villager);

            if (string.IsNullOrEmpty(requiresFlag))
                return;

            var gate = new GameObject($"{name} (gate)", typeof(FlagGatedPresence));
            gate.transform.position = position;
            villager.transform.SetParent(gate.transform, true);
            var gateSerialized = new SerializedObject(gate.GetComponent<FlagGatedPresence>());
            gateSerialized.FindProperty("requiresFlag").stringValue = requiresFlag;
            gateSerialized.FindProperty("body").objectReferenceValue = villager;
            gateSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Where the east street leaves the village, and the chapter's travel begins.</summary>
        static void SetUpRoadOut(Vector3 position)
        {
            var exit = new GameObject("Road to the trade road",
                typeof(CircleCollider2D), typeof(WorldInteractable), typeof(SceneExit));
            exit.transform.position = position;

            var serialized = new SerializedObject(exit.GetComponent<SceneExit>());
            serialized.FindProperty("destinationScene").stringValue = "TradeRoad";
            serialized.FindProperty("promptKey").stringValue = "prompt.travel.road";
            serialized.FindProperty("setsFlag").stringValue = "act2.left_the_valley";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The north lane out to the mill knoll — it exists on the map once Winna asks after her flour.</summary>
        static void SetUpMillLane(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            if (at.trail == null)
                return;
            var lane = new GameObject("Mill lane");
            lane.transform.position = map.At(at.trail);
            GameObject exit = MapScene.BuildExit(map.At(at.trail), "Windrest", "prompt.travel.windrest", "act2.found_windrest");
            exit.transform.SetParent(lane.transform, true);
            MapScene.BuildProp("Lane post", "Misc B52_E", map.At(at.trail) + new Vector3(-0.45f, 0.2f, 0f), exit.transform,
                solid: false, strikeable: false);
            MapScene.Gate(lane, "act1.windrest_rumoured");
        }

        static void SetUpNoticeBoard(Vector3 position)
        {
            GameObject board = MapScene.BuildProp("Notice Board", "Misc B54_E", position, null, solid: true, strikeable: false);
            board.AddComponent<WorldInteractable>();
            board.AddComponent<NoticeBoard>();
        }

        static void SetUpGathering(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            var parent = new GameObject("GatherNodes").transform;
            BuildNodes(map, parent, at.timber, "mat_timber", "prompt.gather.timber", 3, new[] { "Tree A1_S", "Tree A3_S" });
            // Three per pile: two piles must cover the hearth's five stone in one pass, not a forty-second wait.
            BuildNodes(map, parent, at.stone, "mat_stone", "prompt.gather.stone", 3, new[] { "Stone A10_E", "Stone A1_E" });
        }

        static void BuildNodes(MapScene.MapFile map, Transform parent, MapScene.CellRef[] cells,
            string item, string prompt, int amount, string[] tiles)
        {
            int index = 0;
            foreach (MapScene.CellRef cell in cells ?? System.Array.Empty<MapScene.CellRef>())
            {
                GameObject node = MapScene.BuildProp($"{item} {cell.x},{cell.y}", tiles[index++ % tiles.Length], map.At(cell), parent,
                    solid: true, strikeable: true);
                node.AddComponent<WorldInteractable>();

                var serialized = new SerializedObject(node.AddComponent<GatherNode>());
                serialized.FindProperty("itemId").stringValue = item;
                serialized.FindProperty("promptKey").stringValue = prompt;
                serialized.FindProperty("amount").intValue = amount;
                serialized.FindProperty("respawnSeconds").floatValue = 40f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetUpBuildSites(MapScene.MapFile map, MapScene.CellRef[] plots)
        {
            var parent = new GameObject("BuildSites").transform;
            var ghostSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");

            foreach (MapScene.CellRef plot in plots ?? System.Array.Empty<MapScene.CellRef>())
            {
                Vector3 position = map.At(plot);
                var site = new GameObject($"Plot {plot.x},{plot.y}",
                    typeof(CircleCollider2D), typeof(WorldInteractable), typeof(BuildSite));
                site.transform.position = position;
                site.transform.SetParent(parent, true);

                var ghost = new GameObject("Ghost", typeof(SpriteRenderer));
                ghost.transform.SetParent(site.transform, false);
                SpriteRenderer ghostRenderer = ghost.GetComponent<SpriteRenderer>();
                ghostRenderer.sprite = ghostSprite;
                ghostRenderer.color = new Color(0.8f, 0.7f, 0.45f, 0.25f);
                ghost.transform.localScale = Vector3.one * 1.6f;

                GameObject built = MapScene.InstantiatePrefab(
                    MapScene.Prefabs + "Destructible tiles/Destructible Straw roof.prefab", position);
                if (built)
                {
                    built.name = "Shelter";
                    built.transform.SetParent(site.transform, true);
                    built.SetActive(false);
                }

                var serialized = new SerializedObject(site.GetComponent<BuildSite>());
                serialized.FindProperty("ghostVisual").objectReferenceValue = ghost;
                serialized.FindProperty("builtVisual").objectReferenceValue = built;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetUpNight(Vector3 hearth)
        {
            var director = new GameObject("NightDirector", typeof(NightDirector));
            director.transform.position = hearth;
            var serialized = new SerializedObject(director.GetComponent<NightDirector>());

            string[] regularPaths =
            {
                MapScene.Prefabs + "Enemy/Regular/SkeletonWarriorPrefab.prefab",
                MapScene.Prefabs + "Enemy/Regular/SkeletonMagePrefab.prefab",
                MapScene.Prefabs + "Enemy/Regular/SkeletonKnightPrefab.prefab",
            };
            GameObject[] regulars = regularPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab).ToArray();
            if (regulars.Length == 0)
                Debug.LogError("[Oathfire] No enemy prefabs found for the night director");

            SerializedProperty regular = serialized.FindProperty("regularEnemies");
            regular.arraySize = regulars.Length;
            for (int i = 0; i < regulars.Length; i++)
                regular.GetArrayElementAtIndex(i).objectReferenceValue = regulars[i];

            SerializedProperty elites = serialized.FindProperty("eliteEnemies");
            GameObject[] elitePrefabs = new[] { "Warrior", "Knight", "Mage" }
                .Select(kind => AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + $"Enemy/Elites/EliteSkeleton{kind}Prefab.prefab"))
                .Where(prefab => prefab).ToArray();
            elites.arraySize = elitePrefabs.Length;
            for (int i = 0; i < elitePrefabs.Length; i++)
                elites.GetArrayElementAtIndex(i).objectReferenceValue = elitePrefabs[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The valley's dead are not all alike: fresh from the grave, rotted green in the marsh graves, grey from
            // the old burnings; their champions smoulder.
            director.GetComponent<NightDirector>().SetLooks(
                new[]
                {
                    // Plain bone first; the stranger dead come on later nights.
                    MapScene.Look("Grave-risen", "", Color.white, 0.95f, 1.02f),
                    MapScene.Look("Mossbound", "", new Color(0.72f, 0.88f, 0.68f), 0.95f, 1.1f),
                    MapScene.Look("Ashen", "", new Color(0.7f, 0.74f, 0.86f), 0.9f, 1.02f),
                    MapScene.Look("Barrow-pale", "", new Color(0.86f, 0.84f, 0.74f), 1.02f, 1.14f),
                    MapScene.Look("Rot-brown", "", new Color(0.76f, 0.66f, 0.52f), 0.92f, 1.06f),
                },
                new[]
                {
                    MapScene.Look("Emberborn", "", new Color(1f, 0.62f, 0.45f), 1.15f, 1.25f),
                    MapScene.Look("Cold crown", "", new Color(0.62f, 0.78f, 0.95f), 1.1f, 1.22f),
                });
        }

        static void SetUpWorldEvents()
        {
            var director = new GameObject("WorldEventDirector", typeof(WorldEventDirector));
            var serialized = new SerializedObject(director.GetComponent<WorldEventDirector>());
            serialized.FindProperty("visitorPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "NPC/TraderTemplete.prefab");
            serialized.FindProperty("visitorBody").objectReferenceValue = MapScene.RigController("NPC1");
            serialized.FindProperty("visitorStill").objectReferenceValue = MapScene.RigIdle("NPC1");
            serialized.FindProperty("apparitionSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/watcher.png");
            serialized.FindProperty("markerSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
