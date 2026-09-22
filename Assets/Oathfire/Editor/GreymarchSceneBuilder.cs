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
using UnityEngine.SceneManagement;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Builds Greymarch, the walled city the valley answers to: keep, garrison halls, a market square and
    /// the gate that closes in Chapter 1. Composed by Tools/compose_greymarch.py, which places the pack's
    /// whole walled district as one piece rather than cutting a rectangle through it.
    /// </summary>
    public static class GreymarchSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Greymarch.unity";

        [MenuItem("Oathfire/Build Greymarch Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("greymarch");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Greymarch");

            SetUpCitizens(map, at);
            SetUpNoticeBoard(map.At(at.board));
            SetUpRoadHome(map.At(at.roadOut));
            SetUpStash(map, at);
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
            Debug.Log("[Oathfire] Greymarch scene built");
        }

        /// <summary>The city speaks with a different voice than the valley: officials, traders, a gate guard.</summary>
        static void SetUpCitizens(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            // Earlier conversations first: the story ones are tried in order, the everyday line is the fallback.
            (string name, string speaker, (string script, string requires, string consumed)[] talk)[] citizens =
            {
                ("Vesna Kell", "speaker.vesna", new[]
                {
                    ("vesna_ledger", "act2.prisoner_freed", "act2.ledger_delivered"),
                    ("vesna_offer", "", ""),
                }),
                ("Ser Galen", "speaker.galen", new[] { ("galen_loyalty", "", "") }),
                ("Sabel Orr", "speaker.sabel", new[] { ("merchant_sabel", "", "") }),
                ("Corporal Dace", "speaker.dace", new[]
                {
                    ("city_dace_raids", "act2.bandits_hinted", "act2.dace_briefed"),
                    ("city_dace", "", ""),
                }),
                ("Oona the Porter", "speaker.oona", new[] { ("city_oona", "", "") }),
                ("Pell the Crier", "speaker.pell", new[] { ("city_pell", "", "") }),
                ("Widow Asta", "speaker.asta", new[] { ("city_asta", "", "") }),
            };

            // The market has six standing places; the widow takes the spot kept for the valley's smith.
            var spots = (at.villagers ?? System.Array.Empty<MapScene.CellRef>()).ToList();
            if (at.brann != null)
                spots.Add(at.brann);
            for (int i = 0; i < citizens.Length && i < spots.Count; i++)
                BuildCitizen(citizens[i].name, citizens[i].speaker, map.At(spots[i]), citizens[i].talk);

            MapScene.PrependConversation(GameObject.Find("Ser Galen"), "galen_ledger", "quest.sq_unmarked_boots.taken",
                "act2.galen_saw_ledger", "quest_harrow_ledger");
            MapScene.PrependConversation(GameObject.Find("Oona the Porter"), "city_oona_letter", "quest.sq_oonas_letter.taken",
                "sq.letter.given");

            // Mira Holt, once freed from the Hollow, waits in the city for the League to pay her.
            if (at.maren != null)
                MapScene.BuildTalker("Mira Holt", "speaker.mira", map.At(at.maren), "NPC1", new Color(0.93f, 0.9f, 0.8f), 0.94f,
                    new[] { ("city_mira", "", "") }, requiresFlag: "act2.prisoner_freed");

            // Charter Warden Ansel only surfaces once the master die is in Maren's hands — he reads
            // the die's cut, names the cutter, and marks the dead toll fort on the Warden's map.
            if (at.hearth != null)
                MapScene.BuildTalker("Charter Warden Ansel", "speaker.ansel", map.At(at.hearth) + new Vector3(1.8f, 0.9f, 0f),
                    "NPC2", new Color(0.68f, 0.72f, 0.78f), 1f,
                    new[] { ("charter_ansel", "", "act3.tollbank_marked") }, requiresFlag: "act2.die_delivered");
        }

        static void BuildCitizen(string name, string speakerKey, Vector3 position, (string script, string requires, string consumed)[] talk)
        {
            GameObject citizen = MapScene.InstantiatePrefab(MapScene.Prefabs + "NPC/TraderTemplete.prefab", position);
            if (!citizen)
                return;
            citizen.name = name;
            foreach (var trader in citizen.GetComponentsInChildren<TraderComponent>())
                Object.DestroyImmediate(trader);

            var trigger = citizen.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            citizen.AddComponent<WorldInteractable>();
            // Soldiers wear the hood, townsfolk the robe.
            // Soldiers wear the hood; townsfolk the robe, each with a tint of their own.
            bool soldier = speakerKey == "speaker.galen" || speakerKey == "speaker.dace";
            Color tint = speakerKey switch
            {
                "speaker.galen" => new Color(0.85f, 0.88f, 0.95f),
                "speaker.dace" => new Color(0.95f, 0.85f, 0.8f),
                "speaker.oona" => new Color(0.9f, 0.95f, 1f),
                "speaker.pell" => new Color(1f, 0.85f, 0.7f),
                "speaker.asta" => new Color(0.82f, 0.82f, 0.86f),
                _ => new Color(1f, 0.92f, 0.82f),
            };
            MapScene.GiveNpcBody(citizen, soldier ? "NPC2" : "NPC1", tint);
            ActorSorting.Raise(citizen);

            // A market where nobody moves reads as a diorama: citizens drift between the stalls, and the
            // two soldiers walk a longer patrol than the porters do.
            var wanderer = citizen.AddComponent<World.NpcWanderer>();
            var wander = new SerializedObject(wanderer);
            wander.FindProperty("radius").floatValue = soldier ? 3.2f : 2.2f;
            wander.FindProperty("walkSpeed").floatValue = soldier ? 1.1f : 0.8f;
            wander.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(citizen.AddComponent<NpcDialogue>());
            serialized.FindProperty("speakerKey").stringValue = speakerKey;
            SerializedProperty conversations = serialized.FindProperty("conversations");
            conversations.arraySize = talk.Length;
            for (int i = 0; i < talk.Length; i++)
            {
                SerializedProperty entry = conversations.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("scriptId").stringValue = talk[i].script;
                entry.FindPropertyRelative("requiresFlag").stringValue = talk[i].requires;
                entry.FindPropertyRelative("consumedFlag").stringValue = talk[i].consumed;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            MapScene.GiveOfferMarker(citizen);
        }

        static void SetUpNoticeBoard(Vector3 position)
        {
            GameObject board = MapScene.BuildProp("City Notices", "Misc B54_E", position, null, solid: true, strikeable: false);
            board.AddComponent<WorldInteractable>();
            board.AddComponent<NoticeBoard>();
        }

        /// <summary>A marshal's chest left in an alley when the corps went north — the city never claimed it.</summary>
        static void SetUpStash(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            if (at.villagers != null && at.villagers.Length > 5)
                MapScene.BuildSearchSpot("The marshal's chest", map.At(at.villagers[5]) + new Vector3(0.8f, -0.4f, 0f),
                    "cache_city_helm", "Chest A1_E", "helm_kettle", "cache.city.helm", "");
        }

        static void SetUpRoadHome(Vector3 position)
        {
            var exit = new GameObject("Road to the valley",
                typeof(CircleCollider2D), typeof(WorldInteractable), typeof(SceneExit));
            exit.transform.position = position;

            var serialized = new SerializedObject(exit.GetComponent<SceneExit>());
            serialized.FindProperty("destinationScene").stringValue = "TradeRoad";
            serialized.FindProperty("promptKey").stringValue = "prompt.travel.valley";
            serialized.FindProperty("setsFlag").stringValue = "act2.left_greymarch";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
