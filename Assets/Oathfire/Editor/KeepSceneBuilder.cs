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
    /// Builds the Iron Keep: a dead Crown store-fort on the rise off the trade road, held now by deserters
    /// of the Ash Line under a self-styled castellan. The scene is a gauntlet: the bare forecourt, the
    /// breached vestibule where the watch waits, the torch-lit corridor, the great hall, and on the dais
    /// Castellan Durn — with the quartermaster's manifest sealed in the vault behind him.
    /// Map from Tools/build_maps.py (region "keep").
    /// </summary>
    public static class KeepSceneBuilder
    {
        const string ScenePath = "Assets/Oathfire/Scenes/Keep.unity";

        [MenuItem("Oathfire/Build Iron Keep Scene")]
        public static void Build()
        {
            MapScene.MapFile map = MapScene.Load("keep");
            if (map == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MapScene.ConfigureIsometricSorting();
            MapScene.Paint(map);

            MapScene.MapAnchors at = map.anchors;
            GameObject player = MapScene.SetUpPlayer(map.At(at.player));
            MapScene.SetUpCamera(player.transform, map.Ground, "Keep", globalLight: 0.78f);
            LightTheKeep(map);

            GameObject[] deserters = TradeRoadSceneBuilder.EnemyPrefabs();
            ActorLook[] looks = DeserterLooks();
            // The watch has fallen back inside the breach; the corridor guard holds the narrow ground.
            BuildFight("Vestibule watch", map.At(at.arrivals[0]), deserters, looks, count: 3, unlocked: 2, health: 1f, damage: 12,
                "event.keep.watch", "act2.keep_watch_cleared", "toast.keepWatch", triggerRadius: 4.2f, spread: 3.4f);
            BuildFight("Corridor guard", map.At(at.arrivals[1]), deserters, looks, count: 4, unlocked: 3, health: 1.05f, damage: 13,
                "event.keep.corridor", "act2.keep_corridor_cleared", "toast.keepCorridor", triggerRadius: 3.0f, spread: 2.4f);

            BuildCastellan(map.At(at.hearth));
            BuildClerk(map.At(at.villagers[0]));
            BuildManifest(map.At(new MapScene.CellRef { x = 12, y = 48 }), deserters);
            BuildStores(map, at);
            BuildStrewnRecords(map, at);
            MapScene.BuildExit(map.At(at.roadOut), "TradeRoad", "prompt.travel.keepOut", "");

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
            Debug.Log("[Oathfire] Iron Keep scene built");
        }

        /// <summary>Braziers and torches still burn in a keep that has not been a ruin to everyone.</summary>
        static void LightTheKeep(MapScene.MapFile map)
        {
            int lit = 0;
            foreach (MapScene.MapCell cell in map.layers.SelectMany(layer => layer.cells)
                         .Where(cell => cell.tile.StartsWith("FirePlace") || cell.tile.StartsWith("Brazier")
                                     || cell.tile.StartsWith("Torch")))
            {
                var glowGo = new GameObject("Keep fire light", typeof(Light2D));
                glowGo.transform.position = map.At(new MapScene.CellRef { x = cell.x, y = cell.y }) + new Vector3(0f, 0.3f, 0f);
                Light2D glow = glowGo.GetComponent<Light2D>();
                glow.lightType = Light2D.LightType.Point;
                glow.color = new Color(1f, 0.58f, 0.26f);
                glow.pointLightOuterRadius = 4f;
                glow.pointLightInnerRadius = 0.5f;
                glow.intensity = 0.5f;
                glow.shadowsEnabled = false;
                lit++;
            }
            Debug.Log($"[Oathfire] Keep lit {lit} fire points");
        }

        /// <summary>Deserters of the Ash Line: still in issue grey, but stripped of its marks.</summary>
        internal static World.ActorLook[] DeserterLooks() => new[]
        {
            MapScene.Look("Line deserter", "NPC2", new Color(0.66f, 0.68f, 0.72f), 0.95f, 1.05f, "kills.deserter"),
            MapScene.Look("Stripped sash", "NPC1", new Color(0.72f, 0.7f, 0.64f), 0.95f, 1.05f, "kills.deserter"),
            MapScene.Look("Keep watch", "NPC2", new Color(0.5f, 0.54f, 0.62f), 1f, 1.12f, "kills.deserter"),
            MapScene.Look("Powder caster", "NPC1", new Color(0.74f, 0.66f, 0.78f), 0.92f, 1.02f, "kills.deserter"),
            MapScene.Look("Mustered-out knight", "NPC2", new Color(0.42f, 0.44f, 0.5f), 1.08f, 1.22f, "kills.deserter"),
        };

        static void BuildFight(string name, Vector3 position, GameObject[] prefabs, ActorLook[] looks, int count, int unlocked,
            float health, int damage, string bannerKey, string clearedFlag, string toastKey, float triggerRadius, float spread)
        {
            var fight = new GameObject(name, typeof(RoadAmbush), typeof(QuestSpot));
            fight.transform.position = position;
            fight.GetComponent<QuestSpot>().Configure("keep_fight", clearedFlag);

            var serialized = new SerializedObject(fight.GetComponent<RoadAmbush>());
            SerializedProperty attackers = serialized.FindProperty("attackers");
            attackers.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                attackers.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            serialized.FindProperty("count").intValue = count;
            serialized.FindProperty("unlockedLooks").intValue = unlocked;
            serialized.FindProperty("healthMultiplier").floatValue = health;
            serialized.FindProperty("hitDamage").intValue = damage;
            serialized.FindProperty("triggerRadius").floatValue = triggerRadius;
            serialized.FindProperty("spread").floatValue = spread;
            serialized.FindProperty("bannerKey").stringValue = bannerKey;
            serialized.FindProperty("clearedFlag").stringValue = clearedFlag;
            serialized.FindProperty("counterKey").stringValue = "kills.keep";
            serialized.FindProperty("clearedToastKey").stringValue = toastKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fight.GetComponent<RoadAmbush>().SetLooks(looks);
        }

        /// <summary>
        /// Castellan Durn, the Ash Line quartermaster who kept the keys when the Line walked south. He sits
        /// the dais in issue grey gone iron-dark; when he has finished talking, the knight comes out of him.
        /// </summary>
        static void BuildCastellan(Vector3 position)
        {
            var encounter = new GameObject("Castellan Durn", typeof(BossEncounter), typeof(QuestSpot));
            encounter.transform.position = position;
            encounter.GetComponent<QuestSpot>().Configure("keep_boss", "act2.durn_defeated");

            Color iron = new Color(0.58f, 0.6f, 0.68f);
            var standIn = new GameObject("Castellan Durn (waiting)");
            standIn.transform.SetParent(encounter.transform, false);
            standIn.transform.position = position;
            MapScene.GiveNpcBody(standIn, "NPC2", iron);
            ActorSorting.Raise(standIn);
            standIn.transform.localScale *= 1.22f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapScene.Prefabs + "Enemy/Elites/EliteSkeletonKnightPrefab.prefab");
            if (!prefab)
                Debug.LogError("[Oathfire] Elite knight prefab missing; Castellan Durn cannot fight");

            var serialized = new SerializedObject(encounter.GetComponent<BossEncounter>());
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("standIn").objectReferenceValue = standIn;
            serialized.FindProperty("nameKey").stringValue = "boss.durn";
            serialized.FindProperty("introScriptId").stringValue = "keep_durn";
            serialized.FindProperty("defeatedFlag").stringValue = "act2.durn_defeated";
            serialized.FindProperty("defeatedToastKey").stringValue = "toast.durnDown";
            serialized.FindProperty("healthMultiplier").floatValue = 1.05f;
            serialized.FindProperty("hitDamage").intValue = 13;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ActorLook look = MapScene.Look("Castellan Durn", "NPC2", iron, 1f, 1f, "kills.deserter");
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
        /// Roane, Durn's ledger-clerk, barred himself in the vault the day the keep stopped being a garrison.
        /// While Durn stands he whispers where the manifest is; once Durn falls the Warden decides whether
        /// a deserter's clerk gets to walk south.
        /// </summary>
        static void BuildClerk(Vector3 position)
        {
            MapScene.BuildTalker("Roane", "speaker.roane", position, "NPC1", new Color(0.78f, 0.76f, 0.7f), 0.92f, new[]
            {
                ("keep_roane_spared", "act2.roane_spared", ""),
                ("keep_roane_turns", "act2.durn_defeated", "act2.roane_answered"),
                ("keep_roane_shut", "", "act2.durn_defeated"),
            }, blockedByFlag: "act2.roane_driven");
        }

        /// <summary>
        /// The quartermaster's manifest, sealed in the red chest in the vault. It waits until the keep's
        /// master is down — and the old garrison's dead still stand one watch over it.
        /// </summary>
        static void BuildManifest(Vector3 position, GameObject[] guardians)
        {
            MapScene.BuildSearchSpot("The sealed manifest", position, "keep_manifest", "Misc B8_E",
                "quest_keep_manifest", "act2.manifest_found", "act2.durn_defeated",
                guardians, 2, "event.keep.vault");
        }

        /// <summary>
        /// Two small finds for side work: Roane's tally-book, dropped in the corridor the day the clerks
        /// scattered (worth looking for only once he has been spared and asks for it), and the deserters'
        /// pay-roll in the vestibule stores, which Maren wants brought back for the count.
        /// </summary>
        static void BuildStrewnRecords(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            if (at.arrivals != null && at.arrivals.Length > 1)
                MapScene.BuildSearchSpot("Roane's tally-book", map.At(at.arrivals[1]) + new Vector3(-0.55f, 0.25f, 0f),
                    "keep_tally", "Misc A1_E", "quest_roane_tally", "act2.tally_found", "quest.sq_tallybook.taken");
            if (at.timber != null && at.timber.Length > 1)
                MapScene.BuildSearchSpot("Deserter pay-roll", map.At(at.timber[1]) + new Vector3(-0.6f, 0.25f, 0f),
                    "keep_pay", "Misc A2_E", "quest_pay_roll", "act2.payroll_found", "quest.sq_pay_roll.taken");
            // What the vault was holding for a war that never came: the captain's sword, the oath-wall,
            // and a warden's plate — all of it still under the old garrison's watch.
            if (at.arrivals != null && at.arrivals.Length > 2)
            {
                MapScene.BuildSearchSpot("The captain's chest", map.At(at.arrivals[2]) + new Vector3(0.8f, -0.3f, 0f),
                    "cache_keep_sword", "Chest A1_E", "sword_crowned", "cache.keep.sword", "act2.durn_defeated",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.keep.vault");
                MapScene.BuildSearchSpot("The oath-wall", map.At(at.arrivals[0]) + new Vector3(0.85f, 0.35f, 0f),
                    "cache_keep_shield", "Chest A1_E", "shield_oathwall", "cache.keep.shield", "act2.durn_defeated",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 3, "event.keep.vault");
                MapScene.BuildSearchSpot("The marshal's glaive", map.At(at.arrivals[1]) + new Vector3(0.75f, -0.35f, 0f),
                    "cache_keep_glaive", "Chest A1_E", "spear_glaive_ash", "cache.keep.glaive", "act2.durn_defeated",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.keep.vault");
                MapScene.BuildSearchSpot("The marshal's helm", map.At(at.arrivals[2]) + new Vector3(-0.8f, 0.35f, 0f),
                    "cache_keep_helm", "Chest A1_E", "helm_marshal", "cache.keep.helm", "act2.durn_defeated",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.keep.vault");
            }
            if (at.timber != null && at.timber.Length > 0)
                MapScene.BuildSearchSpot("The warden's plate", map.At(at.timber[0]) + new Vector3(0.8f, -0.4f, 0f),
                    "cache_keep_plate", "Chest A1_E", "armor_warden_plate", "cache.keep.plate", "act2.durn_defeated",
                    TradeRoadSceneBuilder.EnemyPrefabs(), 2, "event.cacheGuard");
        }

        /// <summary>What the keep was built to hold: iron in the vault, salt meat in the vestibule.</summary>
        static void BuildStores(MapScene.MapFile map, MapScene.MapAnchors at)
        {
            var parent = new GameObject("Stores").transform;
            (string item, int amount, string tile, float respawn)[] stores =
            {
                ("mat_iron", 2, "Misc B45_N", 300f),
                ("food_salt_meat", 2, "Misc B8_E", 240f),
            };
            MapScene.CellRef[] spots = at.timber ?? System.Array.Empty<MapScene.CellRef>();
            for (int i = 0; i < stores.Length && i < spots.Length; i++)
            {
                GameObject node = MapScene.BuildProp("Quartermaster's stores", stores[i].tile, map.At(spots[i]), parent, solid: true, strikeable: true);
                node.AddComponent<WorldInteractable>();
                var serialized = new SerializedObject(node.AddComponent<GatherNode>());
                serialized.FindProperty("itemId").stringValue = stores[i].item;
                serialized.FindProperty("promptKey").stringValue = "prompt.keepStores";
                serialized.FindProperty("amount").intValue = stores[i].amount;
                serialized.FindProperty("respawnSeconds").floatValue = stores[i].respawn;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
