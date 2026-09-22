using System.Collections.Generic;
using System.Linq;
using Oathfire.Progress;
using Oathfire.World;
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
    /// Builds Blackthorn Hollow: the raiders' camp in a ravine off the trade road. The walk down is the story
    /// of the place: tents round a cookfire where the crew is waiting, a narrow pass where the lookouts are,
    /// and an overgrown ruin at the end where their captain, Rook Varr, keeps a Salt League carter tied up.
    /// Map from Tools/build_maps.py (region "hollow").
    /// </summary>
    public static class HollowSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Hollow.unity";

        [MenuItem("Oathfire/Build Blackthorn Hollow Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("hollow");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Hollow");
            LightTheCookfire(map);

            GameObject[] bandits = TradeRoadSceneBuilder.EnemyPrefabs();
            ActorLook[] looks = TradeRoadSceneBuilder.BanditLooks();
            // The crew round the fire are the plain ones; the lookouts in the pass include the turned warden and the caster.
            BuildFight("Camp fight", map.At(at.arrivals[0]), bandits, looks, count: 5, unlocked: 2, health: 0.9f, damage: 10,
                "event.hollow.camp", "act2.hollow_camp_cleared", "toast.hollowCamp");
            BuildFight("Pass fight", map.At(at.arrivals[1]) + new Vector3(5.5f, 1.25f, 0f), bandits, looks, count: 4, unlocked: 4, health: 0.95f, damage: 11,
                "event.hollow.pass", "act2.hollow_pass_cleared", "toast.hollowPass", spread: 2.0f);

            BuildCaptain(map.At(at.hearth));
            BuildPrisoner(map.At(at.villagers[0]));
            BuildStores(map, at);
            // The road mouth stands in a walled pocket; anyone walking in lands on the open strip beside it.
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.hollowOut", "",
                arrival: map.At(new MapScene.CellRef { x = 26, y = 7 }));

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
            Debug.Log("[Oathfire] Blackthorn Hollow scene built");
        }

        /// <summary>The camp's fire burns day and night; it is the warm light the whole ravine is lit by.</summary>
        static void LightTheCookfire(MapScene.MapFile map)
        {
            MapScene.MapCell fire = map.layers.SelectMany(layer => layer.cells).FirstOrDefault(cell => cell.tile.StartsWith("FirePlace"));
            if (fire == null)
            {
                Debug.LogWarning("[Oathfire] The Hollow has no cookfire tile to light");
                return;
            }
            var glowGo = new GameObject("Cookfire light", typeof(Light2D));
            glowGo.transform.position = map.At(new MapScene.CellRef { x = fire.x, y = fire.y }) + new Vector3(0f, 0.3f, 0f);
            Light2D glow = glowGo.GetComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.28f);
            glow.pointLightOuterRadius = 4.5f;
            glow.pointLightInnerRadius = 0.6f;
            glow.intensity = 0.55f;
            glow.shadowsEnabled = false;
        }

        static void BuildFight(string name, Vector3 position, GameObject[] prefabs, ActorLook[] looks, int count, int unlocked,
            float health, int damage, string bannerKey, string clearedFlag, string toastKey, float spread = 3.5f)
        {
            var fight = new GameObject(name, typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("hollow_fight", clearedFlag);

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = count;
            serialized.FindProperty("unlockedLooks").intValue = unlocked;
            serialized.FindProperty("healthMultiplier").floatValue = health;
            serialized.FindProperty("hitDamage").intValue = damage;
            // The ravine is narrow: they come from close by, and only once the hero is actually among the tents.
            serialized.FindProperty("triggerRadius").floatValue = 3.6f;
            serialized.FindProperty("spread").floatValue = spread;
            serialized.FindProperty("bannerKey").stringValue = bannerKey;
            serialized.FindProperty("clearedFlag").stringValue = clearedFlag;
            serialized.FindProperty("counterKey").stringValue = "kills.hollow";
            serialized.FindProperty("clearedToastKey").stringValue = toastKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(looks);
        }

        /// <summary>
        /// Rook Varr, a road warden who took the Greymarch coin: waiting in the ruin in a warden's grey hood gone
        /// rust-brown, taller than his crew. The body standing there is only a figure; the fighter comes when he
        /// has finished talking.
        /// </summary>
        static void BuildCaptain(Vector3 position)
        {
            var encounter = new GameObject("Rook Varr", typeof(BossEncounter), typeof(QuestSpot));
            encounter.transform.position = position;
            encounter.GetComponent<QuestSpot>().Configure("hollow_boss", "act2.rook_defeated");

            Color rust = new Color(0.78f, 0.52f, 0.44f);
            var standIn = new GameObject("Rook Varr (waiting)");
            standIn.transform.SetParent(encounter.transform, false);
            standIn.transform.position = position;
            MapScene.GiveNpcBody(standIn, "NPC2", rust);
            ActorSorting.Raise(standIn);
            standIn.transform.localScale *= 1.18f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "Enemy/Elites/EliteSkeletonWarriorPrefab.prefab");
            if (!prefab)
                Debug.LogError("[Oathfire] Elite warrior prefab missing; Rook Varr cannot fight");

            var serialized = new SerializedObject(encounter.GetComponent<BossEncounter>());
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("standIn").objectReferenceValue = standIn;
            serialized.FindProperty("nameKey").stringValue = "boss.rook";
            serialized.FindProperty("introScriptId").stringValue = "hollow_rook";
            serialized.FindProperty("defeatedFlag").stringValue = "act2.rook_defeated";
            serialized.FindProperty("defeatedToastKey").stringValue = "toast.rookDown";
            serialized.FindProperty("healthMultiplier").floatValue = 0.9f;
            serialized.FindProperty("hitDamage").intValue = 11;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ActorLook look = MapScene.Look("Rook Varr", "NPC2", rust, 1f, 1f, "kills.bandit");
            var lookProperty = new SerializedObject(encounter.GetComponent<BossEncounter>());
            SerializedProperty entry = lookProperty.FindProperty("look");
            entry.FindPropertyRelative("name").stringValue = look.name;
            entry.FindPropertyRelative("rig").objectReferenceValue = look.rig;
            entry.FindPropertyRelative("tint").colorValue = look.tint;
            entry.FindPropertyRelative("minScale").floatValue = look.minScale;
            entry.FindPropertyRelative("maxScale").floatValue = look.maxScale;
            entry.FindPropertyRelative("killCounter").stringValue = look.killCounter;
            lookProperty.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Mira Holt, the Salt League carter taken with her wagon. Bound while Rook stands; once he falls she
        /// points the Warden at his strongbox, then makes for Greymarch, where she can be found again.
        /// </summary>
        static void BuildPrisoner(Vector3 position)
        {
            MapScene.BuildTalker("Mira Holt", "speaker.mira", position, "NPC1", new Color(0.93f, 0.9f, 0.8f), 0.94f, new[]
            {
                ("hollow_mira_after", "act2.prisoner_freed", ""),
                ("hollow_mira_freed", "act2.rook_defeated", "act2.prisoner_freed"),
                ("hollow_mira_bound", "", "act2.rook_defeated"),
            });
        }

        /// <summary>What the crew lived on: a store of salt meat by the tents and a medicine chest at the pass.</summary>
        static void BuildStores(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            var parent = new GameObject("Stores").transform;
            (string item, int amount, string tile, float respawn)[] stores =
            {
                ("food_salt_meat", 2, "Misc B8_E", 240f),
                ("potion_small", 1, "Misc B5_E", 420f),
            };
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            for (int i = 0; i < stores.Length && i < spots.Length; i++)
            {
                GameObject node = MapScene.BuildProp("Raiders' stores", stores[i].tile, map.At(spots[i]), parent, solid: true, strikeable: true);
                node.AddComponent<WorldInteractable>();
                var serialized = new SerializedObject(node.AddComponent<GatherNode>());
                serialized.FindProperty("itemId").stringValue = stores[i].item;
                serialized.FindProperty("promptKey").stringValue = "prompt.stores";
                serialized.FindProperty("amount").intValue = stores[i].amount;
                serialized.FindProperty("respawnSeconds").floatValue = stores[i].respawn;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // The glaive a Marshal broke and re-hafted lies where the last raid spilled it.
            if (at.arrivals != null && at.arrivals.Length > 1)
                MapScene.BuildSearchSpot("The re-hafted glaive", map.At(at.arrivals[1]) + new Vector3(0.85f, -0.45f, 0f),
                    "cache_hollow_glaive", "Chest A1_E", "spear_ash", "cache.hollow.glaive", "",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.cacheGuard");
        }
    }
}
