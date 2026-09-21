using System.Collections.Generic;
using System.Linq;
using Oathfire.Controls;
using Oathfire.UI;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScaleInc.TopDownPixelCharactersPack1;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// The parts every location shares: reading a map cut from the tileset's example town, painting its
    /// tile layers in the artist's own draw order, and standing up the player, camera and HUD on top.
    /// Scene builders add what makes their location that location.
    /// </summary>
    public static class MapScene
    {
        public const string Package = "Assets/SmallScaleInt/Fantasy kingdom Tileset";
        public const string Prefabs = Package + "/Example scene/Prefabs/";
        const string Tiles = Package + "/Environment/Tiles/";
        const string AnimatedTiles = Package + "/Environment/Animated tiles/";

        /// <summary>Tile assets live in Tiles/, but animated ones (windmills, water) live beside them.</summary>
        static TileBase LoadTile(string tileName)
        {
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + tileName + ".asset");
            return tile ? tile : AssetDatabase.LoadAssetAtPath<TileBase>(AnimatedTiles + tileName + ".asset");
        }
        const string UiSheet = Package + "/Example scene/UI/UISprites.png";
        const string SkillSheet = Package + "/Example scene/UI/SkillIcons.png";
        const string LootSheet = Package + "/Example scene/UI/LootIcons.png";
        const string Abilities = Package + "/Example scene/Scripts/AbilitySystem/Abilities Active/";

        /// <summary>Layers whose tiles must sort against the player one by one, not as a batch.</summary>
        static readonly string[] PerTileLayers = { "Walls", "WallDetail1", "WallDetail2", "Objects", "BrokenObjects" };

        [System.Serializable] public class MapCell { public int x; public int y; public string tile; }
        [System.Serializable] public class MapLayer { public string name; public int order; public MapCell[] cells; }
        [System.Serializable] public class CellRef { public int x; public int y; }

        [System.Serializable]
        public class MapAnchors
        {
            public CellRef hearth, player, board, brann, maren, roadOut, trail, trail2, trail3, trail4, trail5, trail6, search;
            public CellRef[] plots, timber, stone, arrivals, villagers;
        }

        [System.Serializable]
        public class House
        {
            public CellRef door, outside;
            public string tile;
            public CellRef[] interior, roof;
        }

        [System.Serializable]
        public class MapFile
        {
            public MapLayer[] layers;
            public MapAnchors anchors;
            public House[] houses;
            public Tilemap Ground { get; set; }

            public Vector3 At(CellRef cell) =>
                Ground && cell != null ? Ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)) : Vector3.zero;
        }

        public static MapFile Load(string mapName)
        {
            string relative = $"Docs/{mapName}_map.json";
            string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", relative);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"[Oathfire] Missing {relative}. Run: python Tools/build_maps.py");
                return null;
            }
            var map = JsonUtility.FromJson<MapFile>(System.IO.File.ReadAllText(path));
            if (map?.layers == null || map.layers.Length == 0)
            {
                Debug.LogError($"[Oathfire] {relative} has no layers");
                return null;
            }
            return map;
        }

        /// <summary>
        /// Isometric art sorts by world Y, not by distance to camera, or the player walks in front of roofs.
        /// </summary>
        public static void ConfigureIsometricSorting()
        {
            UnityEngine.Rendering.GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            UnityEngine.Rendering.GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);

            foreach (string guid in AssetDatabase.FindAssets("t:Renderer2DData", new[] { "Assets/Settings" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                if (!data)
                    continue;
                var serialized = new SerializedObject(data);
                serialized.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
                serialized.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0f, 1f, 0f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>Recreates the location's tile layers, keeping the draw order the pack's artist used.</summary>
        public static void Paint(MapFile map)
        {
            var gridGo = new GameObject("Grid", typeof(Grid));
            Grid grid = gridGo.GetComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            var tileCache = new Dictionary<string, TileBase>();
            foreach (MapLayer layer in map.layers)
            {
                bool collision = layer.name.StartsWith("Collider");
                var layerGo = new GameObject(layer.name, typeof(Tilemap), typeof(TilemapRenderer));
                layerGo.transform.SetParent(gridGo.transform, false);

                var tilemap = layerGo.GetComponent<Tilemap>();
                var renderer = layerGo.GetComponent<TilemapRenderer>();
                // The pack paints every tilemap TopRight. TopLeft drew each tile's thick side face over the tile in front of it,
                // so flat ground read as a staircase of brown steps.
                renderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
                renderer.sortingOrder = layer.order;
                renderer.mode = PerTileLayers.Contains(layer.name) ? TilemapRenderer.Mode.Individual : TilemapRenderer.Mode.Chunk;

                if (collision)
                {
                    // Collision tiles are shapes, not scenery: they block movement and are never drawn.
                    renderer.enabled = false;
                    layerGo.layer = LayerMask.NameToLayer("World");
                    layerGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                    // Kept per tile on purpose: merging these isometric shapes into a CompositeCollider2D
                    // produces no geometry at all, which would let the hero walk through every wall.
                    layerGo.AddComponent<TilemapCollider2D>();
                }

                foreach (MapCell cell in layer.cells)
                {
                    if (!tileCache.TryGetValue(cell.tile, out TileBase tile))
                        tileCache[cell.tile] = tile = LoadTile(cell.tile);
                    if (tile)
                        tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
                }
                tilemap.CompressBounds();

                if (layer.name == "Ground")
                    map.Ground = tilemap;
            }
            Debug.Log($"[Oathfire] painted {map.layers.Sum(layer => layer.cells.Length)} tiles across {map.layers.Length} layers");

            // Every wall torch gets a small warm light that the day/night cycle kindles after dark.
            Torches.Clear();
            MapLayer objects = map.layers.FirstOrDefault(layer => layer.name == "Objects");
            // Braziers (the stone fire bowls on Rennfall's square) burn like torches after dark.
            foreach (MapCell torch in (objects?.cells ?? System.Array.Empty<MapCell>())
                         .Where(cell => cell.tile.StartsWith("Torch") || cell.tile.StartsWith("Misc C8"))
                         .OrderByDescending(cell => cell.tile.StartsWith("Misc C8")).Take(MaxTorchLights))
            {
                var torchGo = new GameObject($"Torch light {torch.x},{torch.y}", typeof(Light2D));
                torchGo.transform.SetParent(gridGo.transform, false);
                torchGo.transform.position = grid.GetCellCenterWorld(new Vector3Int(torch.x, torch.y, 0)) + new Vector3(0f, 0.7f, 0f);
                Light2D glow = torchGo.GetComponent<Light2D>();
                glow.lightType = Light2D.LightType.Point;
                glow.color = new Color(1f, 0.66f, 0.3f);
                glow.pointLightOuterRadius = 3.2f;
                glow.pointLightInnerRadius = 0.4f;
                glow.intensity = 0.12f;
                glow.shadowsEnabled = false;
                Torches.Add(glow);
            }

            // Roofs lift off the building the hero is inside or behind, one building at a time.
            var fader = gridGo.AddComponent<World.RoofFader>();
            foreach (House house in map.houses ?? System.Array.Empty<House>())
            {
                fader.AddHouse(
                    (house.interior ?? System.Array.Empty<CellRef>()).Select(c => new Vector3Int(c.x, c.y, 0)).ToArray(),
                    (house.roof ?? System.Array.Empty<CellRef>()).Select(c => new Vector3Int(c.x, c.y, 0)).ToArray());
                var cell = new Vector3Int(house.door.x, house.door.y, 0);
                var door = new GameObject($"Door {cell.x},{cell.y}", typeof(CircleCollider2D), typeof(World.WorldInteractable), typeof(World.HouseDoor));
                door.transform.SetParent(gridGo.transform, false);
                door.transform.position = grid.GetCellCenterWorld(cell);
                door.GetComponent<World.HouseDoor>().Configure(cell, new Vector3Int(house.outside.x, house.outside.y, 0));
            }
            Debug.Log($"[Oathfire] {map.houses?.Length ?? 0} houses can be entered");
        }

        /// <summary>
        /// A standing prop drawn with a tile's own sprite: a tree, a rock, a notice board, a wrecked cart.
        ///
        /// Not the pack's "Destructible" prefabs: all eighteen are break-apart effects that ship with
        /// destroyOnSpawn on, so on the first frame they play their destruction and switch every collider off.
        /// Built from them, the timber trees, stone piles, notice boards and wayside wrecks were rubble nobody
        /// could use.
        /// </summary>
        /// <param name="solid">Gives it a small trunk on the World layer, so the hero walks around it.</param>
        /// <param name="strikeable">Lets weapon hits land on it (a hit flash and shake), for chopping and quarrying.</param>
        public static GameObject BuildProp(string name, string tileName, Vector3 position, Transform parent,
            bool solid, bool strikeable, float reach = 1.7f)
        {
            var prop = new GameObject(name, typeof(SpriteRenderer), typeof(CircleCollider2D));
            prop.transform.position = position;
            if (parent)
                prop.transform.SetParent(parent, true);

            SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
            renderer.sprite = TileSprite(tileName);
            // Same order as people, so they sort against it by height on screen instead of always in front.
            renderer.sortingOrder = World.ActorSorting.ActorOrder;
            if (!renderer.sprite)
                Debug.LogError($"[Oathfire] Tile {tileName} has no sprite; {name} will be invisible");

            // The reach doubles as the weapon's target area, so a swing anywhere near the tree lands.
            var reachArea = prop.GetComponent<CircleCollider2D>();
            reachArea.isTrigger = true;
            reachArea.radius = reach;

            if (solid)
            {
                var trunk = new GameObject("Trunk", typeof(CircleCollider2D));
                trunk.transform.SetParent(prop.transform, false);
                trunk.layer = LayerMask.NameToLayer("World");
                trunk.GetComponent<CircleCollider2D>().radius = 0.3f;
            }

            if (strikeable)
            {
                prop.layer = LayerMask.NameToLayer("Props");
                var body = new SerializedObject(prop.AddComponent<DestructibleProp2D>());
                body.FindProperty("destroyOnSpawn").boolValue = false;
                body.FindProperty("maxHits").intValue = int.MaxValue;
                body.FindProperty("showCombatText").boolValue = false;
                body.FindProperty("flashColor").colorValue = new Color(1f, 0.86f, 0.62f, 1f);
                body.ApplyModifiedPropertiesWithoutUndo();
            }
            return prop;
        }

        /// <summary>The sprite a tile asset draws, whatever tile class the pack used for it.</summary>
        public static Sprite TileSprite(string tileName)
        {
            var tile = LoadTile(tileName);
            if (!tile)
                return null;
            SerializedProperty sprite = new SerializedObject(tile).FindProperty("m_Sprite");
            if (sprite?.objectReferenceValue is Sprite still)
                return still;
            SerializedProperty frames = new SerializedObject(tile).FindProperty("m_AnimatedSprites");
            return frames is { isArray: true, arraySize: > 0 }
                ? frames.GetArrayElementAtIndex(0).objectReferenceValue as Sprite
                : null;
        }

        /// <summary>Lights at the torches painted by the last Paint; SetUpCamera hands them to the day/night cycle.</summary>
        static readonly List<Light2D> Torches = new List<Light2D>();

        /// <summary>Point lights cost fill on a phone; beyond this many, extra torches stay unlit decoration.</summary>
        const int MaxTorchLights = 24;

        public static GameObject SetUpPlayer(Vector3 position)
        {
            GameObject player = InstantiatePrefab(Package + "/Characters/GenericPlayer.prefab", position);
            player.name = "Kael";
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Player");

            var body = Require<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.freezeRotation = true;
            // The body that collides is the feet, not the torso. In an isometric view a wall behind the hero
            // sits above them on screen, so a chest-high circle hits walls the hero is not touching.
            var feet = Require<CircleCollider2D>(player);
            feet.radius = 0.13f;
            feet.offset = new Vector2(0f, -0.18f);
            Require<World.BodyUnstuck>(player);
            Require<World.WallSlideMover>(player);

            Require<PlayerHealth>(player);
            Require<PlayerMana>(player);
            Require<PlayerStats>(player);
            Require<PlayerExperience>(player);
            Require<SmallScale.FantasyKingdomTileset.AbilitySystem.AbilityRunner>(player);

            var melee = Require<PlayerMeleeHitbox>(player);
            Require<World.PlayerDamageSync>(player);
            Require<World.PlayerVitalsSync>(player);
            Require<World.PlayerArrival>(player);
            Require<World.DefeatRecovery>(player);
            melee.enemyMask = LayerMask.GetMask("Enemy");
            melee.destructibleMask = LayerMask.GetMask("Props");

            var controller = new SerializedObject(player.GetComponent<GenericTopDownController>());
            // The controller's own sweep stops the hero dead on any contact, even a glancing one, which is what
            // made walls sticky. With no mask it only reads input and animates; WallSlideMover does the swept,
            // sliding move afterwards. (MovePosition alone ignores static colliders: the hero walked through walls.)
            controller.FindProperty("worldMask").intValue = 0;
            controller.FindProperty("attachedRigidbody").objectReferenceValue = body;
            controller.FindProperty("movementMode").enumValueIndex = (int)GenericTopDownController.MovementMode.Cardinal;
            controller.FindProperty("usePhysicsMovement").boolValue = true;
            // The prefab's speeds are for a body at scale 1. The hero is drawn 1.5x larger, so the stride is
            // scaled with it; at the prefab's 1 unit/s the feet visibly slide and crossing a street drags.
            controller.FindProperty("runSpeed").floatValue = 1f * World.ActorSorting.ActorScale + 0.1f;
            controller.FindProperty("walkSpeed").floatValue = 0.5f * World.ActorSorting.ActorScale + 0.05f;
            controller.FindProperty("primaryAttackKey").intValue = (int)KeyCode.Mouse0;
            controller.FindProperty("defaultAttackKey").intValue = (int)KeyCode.Mouse0;
            controller.ApplyModifiedPropertiesWithoutUndo();

            var health = new SerializedObject(player.GetComponent<PlayerHealth>());
            health.FindProperty("enableRegeneration").boolValue = true;
            health.FindProperty("regenPercentPerTick").floatValue = 0.02f;
            health.ApplyModifiedPropertiesWithoutUndo();

            World.ActorSorting.Raise(player);
            GiveThePlayerABody(player);
            return player;
        }

        /// <summary>
        /// The view. A settlement has to be seen to read as one, so the camera sits far enough back to hold
        /// a house and its street, and is clamped to the painted map so the edges never show the void.
        /// </summary>
        public static void SetUpCamera(Transform target, Tilemap ground, string place, float globalLight = 0.82f)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(SmoothCameraFollow), typeof(World.CameraBounds));
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // An isometric map is a diamond and the screen is a rectangle, so the corners of the view can show
            // past the painted ground. Deep forest green there reads as dark woodland, not as a hole.
            camera.backgroundColor = new Color(0.07f, 0.11f, 0.07f);
            cameraGo.transform.position = target.position + new Vector3(0f, 0f, -10f);

            SmoothCameraFollow follow = cameraGo.GetComponent<SmoothCameraFollow>();
            follow.target = target;
            follow.zoom = 7.5f;
            follow.zoomLimits = new Vector2(6f, 10f);
            follow.mouseWheelZoom = false;

            cameraGo.GetComponent<World.CameraBounds>().FitTo(ground);

            var lightGo = new GameObject("Global Light", typeof(Light2D), typeof(World.DayNightLighting));
            Light2D light = lightGo.GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            // Each place has its own daylight: a warm valley, a bright open road, an overcast stone city.
            (Color colour, float intensity) day = place switch
            {
                "Rennfall" => (new Color(1f, 0.94f, 0.84f), 0.92f),
                "TradeRoad" => (new Color(1f, 0.97f, 0.9f), 1f),
                "Greymarch" => (new Color(0.86f, 0.9f, 0.96f), 0.86f),
                // A ravine the sun reaches late: dusty amber light, a little dimmer than the open road.
                "Hollow" => (new Color(1f, 0.88f, 0.74f), 0.84f),
                // A walled granary court: long late-day light, wheat-coloured, a touch cooler on stone.
                "Saltpans" => (new Color(1f, 0.93f, 0.78f), 0.9f),
                // A camp under its own smoke: dimmer, grey-green light that never quite clears.
                "Burnpits" => (new Color(0.84f, 0.87f, 0.8f), 0.82f),
                _ => (new Color(0.9f, 0.9f, 0.9f), globalLight),
            };
            light.color = day.colour;
            light.intensity = day.intensity;
            var cycle = lightGo.GetComponent<World.DayNightLighting>();
            cycle.Configure(day.colour, day.intensity, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/vignette.png"));
            foreach (Light2D torch in Torches)
                cycle.AddTorch(torch);
            EditorUtility.SetDirty(cycle);
        }

        /// <summary>The screens that follow the player from place to place: HUD, controls, pack, dialogue.</summary>
        public static void SetUpUi(bool includeContractBoard)
        {
            var hudGo = new GameObject("GameplayHud", typeof(GameplayHud));
            var hud = new SerializedObject(hudGo.GetComponent<GameplayHud>());
            hud.FindProperty("barFrame").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_17");
            hud.FindProperty("buttonRing").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_99");
            SerializedProperty hudSkin = hud.FindProperty("skin");
            hudSkin.FindPropertyRelative("ring").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_99");
            hudSkin.FindPropertyRelative("disc").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_102");
            hudSkin.FindPropertyRelative("medallion").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_101");
            hudSkin.FindPropertyRelative("glow").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");
            hudSkin.FindPropertyRelative("attackIcon").objectReferenceValue = LoadSubSprite(SkillSheet, "SkillIcons_5");
            hud.FindProperty("packIcon").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_56");
            hud.FindProperty("bookIcon").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_107");
            hud.FindProperty("menuIcon").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_48");
            hud.ApplyModifiedPropertiesWithoutUndo();

            var controlsGo = new GameObject("MobileControls", typeof(MobileControls));
            var controls = new SerializedObject(controlsGo.GetComponent<MobileControls>());
            SerializedProperty skin = controls.FindProperty("skin");
            skin.FindPropertyRelative("ring").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_99");
            skin.FindPropertyRelative("disc").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_102");
            skin.FindPropertyRelative("medallion").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_101");
            skin.FindPropertyRelative("glow").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");
            skin.FindPropertyRelative("attackIcon").objectReferenceValue = LoadSubSprite(SkillSheet, "SkillIcons_5");
            controls.FindProperty("dashAbility").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Object>(Abilities + "Offensive/Dodge R1.asset");
            var book = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Oathfire/Resources/Skills/skills.json");
            SkillRow[] rows = book ? JsonUtility.FromJson<SkillTable>(book.text).skills : System.Array.Empty<SkillRow>();
            SerializedProperty skillList = controls.FindProperty("skills");
            skillList.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty entry = skillList.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = rows[i].id;
                entry.FindPropertyRelative("level").intValue = rows[i].level;
                var ability = AssetDatabase.LoadAssetAtPath<Object>(Abilities + rows[i].ability + ".asset");
                if (!ability)
                    Debug.LogError($"[Oathfire] Skill {rows[i].id}: no ability asset at {rows[i].ability}");
                entry.FindPropertyRelative("ability").objectReferenceValue = ability;
            }
            controls.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("QuestCompass", typeof(QuestCompass));
            new GameObject("KillRewards", typeof(Progress.KillRewards));

            var hitGo = new GameObject("HitFeedback", typeof(HitFeedback));
            var hit = new SerializedObject(hitGo.GetComponent<HitFeedback>());
            hit.FindProperty("vignetteSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/vignette.png");
            hit.ApplyModifiedPropertiesWithoutUndo();

            if (includeContractBoard)
            {
                var boardGo = new GameObject("ContractBoardPanel", typeof(ContractBoardPanel));
                var board = new SerializedObject(boardGo.GetComponent<ContractBoardPanel>());
                board.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
                board.ApplyModifiedPropertiesWithoutUndo();
            }

            var shopGo = new GameObject("ShopPanel", typeof(ShopPanel));
            var shop = new SerializedObject(shopGo.GetComponent<ShopPanel>());
            shop.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            Sprite[] shopIcons = AssetDatabase.LoadAllAssetsAtPath(LootSheet).OfType<Sprite>()
                .Concat(AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Oathfire/Art/Icons" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid))))
                .Where(sprite => sprite).ToArray();
            SerializedProperty shopIconList = shop.FindProperty("icons");
            shopIconList.arraySize = shopIcons.Length;
            for (int i = 0; i < shopIcons.Length; i++)
                shopIconList.GetArrayElementAtIndex(i).objectReferenceValue = shopIcons[i];
            shop.ApplyModifiedPropertiesWithoutUndo();

            var offerGo = new GameObject("QuestOfferPanel", typeof(QuestOfferPanel));
            var offer = new SerializedObject(offerGo.GetComponent<QuestOfferPanel>());
            offer.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            offer.ApplyModifiedPropertiesWithoutUndo();

            var heroGo = new GameObject("HeroPanel", typeof(HeroPanel));
            var hero = new SerializedObject(heroGo.GetComponent<HeroPanel>());
            hero.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            hero.FindProperty("frameSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_17");
            hero.ApplyModifiedPropertiesWithoutUndo();

            var menuGo = new GameObject("PauseMenuPanel", typeof(PauseMenuPanel));
            var menu = new SerializedObject(menuGo.GetComponent<PauseMenuPanel>());
            menu.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            menu.ApplyModifiedPropertiesWithoutUndo();

            var inventoryGo = new GameObject("InventoryPanel", typeof(InventoryPanel));
            var inventory = new SerializedObject(inventoryGo.GetComponent<InventoryPanel>());
            inventory.FindProperty("panelSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            inventory.FindProperty("anatomySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/UI/warden_anatomy.png");
            Sprite[] loot = AssetDatabase.LoadAllAssetsAtPath(LootSheet).OfType<Sprite>()
                .Concat(AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Oathfire/Art/Icons" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid))))
                .Where(sprite => sprite)
                .ToArray();
            if (loot.Length == 0)
                Debug.LogError($"[Oathfire] No item icons found in {LootSheet}; the pack will show names only");
            SerializedProperty icons = inventory.FindProperty("icons");
            icons.arraySize = loot.Length;
            for (int i = 0; i < loot.Length; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = loot[i];
            inventory.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("Dialogue", typeof(Dialogue.DialogueRunner), typeof(Dialogue.DialoguePanel));
            var panel = new SerializedObject(Object.FindAnyObjectByType<Dialogue.DialoguePanel>());
            panel.FindProperty("boxSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            panel.FindProperty("portraitFrame").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_101");
            panel.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Gives the hero something to look like.
        ///
        /// GenericPlayer.prefab points at a sprite and an animator controller that are not in the package —
        /// both references are dead, so the renderer draws nothing and the player is simply not on screen.
        ///
        /// The obvious replacement, Characters/player1, is the character creator's *base body*: a nude
        /// figure meant to be layered with clothing this package does not ship. NPC3 is the same rig fully
        /// dressed and carrying a weapon, and its controller declares the identical 26 parameters the
        /// movement code drives, so Kael wears that.
        /// </summary>
        /// <summary>
        /// The pack's NPC template (NPC/TraderTemplete) is a trading component with no renderer at all, so every
        /// villager built from it was an invisible voice. This gives a character a drawn, animated body from one
        /// of the pack's dressed rigs (NPC1 robed, NPC2 hooded), tinted so neighbours do not look like twins.
        /// Call before ActorSorting.Raise, which sorts and scales whatever renderers exist.
        /// </summary>
        public static void GiveNpcBody(GameObject actor, string rig, Color tint)
        {
            RuntimeAnimatorController controller = RigController(rig);
            Sprite still = RigIdle(rig);
            if (!controller || !still)
            {
                Debug.LogError($"[Oathfire] Rig {rig} is missing its animator or idle sprite; {actor.name} would be invisible");
                return;
            }
            SpriteRenderer renderer = Require<SpriteRenderer>(actor);
            renderer.sprite = still;
            renderer.color = tint;
            Animator animator = Require<Animator>(actor);
            animator.runtimeAnimatorController = controller;
        }

        /// <summary>
        /// A person who can be spoken to: a drawn body, a talk radius, and conversations picked by story flags
        /// (see World.NpcDialogue). With <paramref name="requiresFlag"/> they are only there once the story has
        /// brought them; with <paramref name="blockedByFlag"/> they leave when it moves them on.
        /// </summary>
        public static GameObject BuildTalker(string name, string speakerKey, Vector3 position, string rig, Color tint, float scale,
            (string script, string requires, string consumed)[] conversationData, string requiresFlag = "", string blockedByFlag = "")
        {
            GameObject person = InstantiatePrefab(Prefabs + "NPC/TraderTemplete.prefab", position);
            if (!person)
                return null;
            person.name = name;
            foreach (var trader in person.GetComponentsInChildren<TraderComponent>())
                Object.DestroyImmediate(trader);

            var trigger = person.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            person.AddComponent<World.WorldInteractable>();
            GiveNpcBody(person, rig, tint);
            World.ActorSorting.Raise(person);
            person.transform.localScale *= scale;

            var serialized = new SerializedObject(person.AddComponent<World.NpcDialogue>());
            serialized.FindProperty("speakerKey").stringValue = speakerKey;
            SerializedProperty conversations = serialized.FindProperty("conversations");
            conversations.arraySize = conversationData.Length;
            for (int i = 0; i < conversationData.Length; i++)
            {
                SerializedProperty entry = conversations.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("scriptId").stringValue = conversationData[i].script;
                entry.FindPropertyRelative("requiresFlag").stringValue = conversationData[i].requires ?? "";
                entry.FindPropertyRelative("consumedFlag").stringValue = conversationData[i].consumed ?? "";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GiveOfferMarker(person);

            if (!string.IsNullOrEmpty(requiresFlag) || !string.IsNullOrEmpty(blockedByFlag))
                Gate(person, requiresFlag, blockedByFlag);
            return person;
        }

        /// <summary>
        /// Something to find: a prop with a glow over it, searchable while the quest that wants it is open.
        /// </summary>
        public static GameObject BuildSearchSpot(string name, Vector3 position, string spotId, string tile, string itemId,
            string foundFlag, string requiresFlag, GameObject[] guardians = null, int guardianCount = 0, string guardianBannerKey = "")
        {
            GameObject spot = BuildProp(name, tile, position, null, solid: false, strikeable: false);
            var trigger = Require<CircleCollider2D>(spot);
            trigger.isTrigger = true;
            trigger.radius = 1.1f;
            spot.AddComponent<World.WorldInteractable>();
            spot.AddComponent<World.QuestSpot>().Configure(spotId, foundFlag);
            var serialized = new SerializedObject(spot.AddComponent<World.SearchSpot>());
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("foundFlag").stringValue = foundFlag;
            if (guardians != null)
            {
                SerializedProperty list = serialized.FindProperty("guardians");
                list.arraySize = guardians.Length;
                for (int i = 0; i < guardians.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = guardians[i];
                serialized.FindProperty("guardianCount").intValue = guardianCount;
                serialized.FindProperty("guardianBannerKey").stringValue = guardianBannerKey;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // A slow glow over it, the same mark a visitor wears, so it can be found across the map.
            var glow = new GameObject("Glow", typeof(SpriteRenderer), typeof(World.EventMarker));
            glow.transform.SetParent(spot.transform, false);
            var marker = new SerializedObject(glow.GetComponent<World.EventMarker>());
            marker.ApplyModifiedPropertiesWithoutUndo();
            var glowRenderer = glow.GetComponent<SpriteRenderer>();
            glowRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");
            glowRenderer.color = new Color(1f, 0.8f, 0.4f, 0.7f);
            glowRenderer.sortingOrder = World.ActorSorting.ActorOrder + 1;
            glow.transform.localScale = Vector3.one * 0.6f;

            return Gate(spot, requiresFlag, foundFlag);
        }

        /// <summary>Puts a conversation at the front of a character's list (tried before their everyday lines).</summary>
        public static void PrependConversation(GameObject person, string script, string requires, string consumed, string requiresItem = "")
        {
            if (!person)
            {
                Debug.LogError($"[Oathfire] No character to give {script} to");
                return;
            }
            var serialized = new SerializedObject(person.GetComponent<World.NpcDialogue>());
            SerializedProperty list = serialized.FindProperty("conversations");
            list.InsertArrayElementAtIndex(0);
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("scriptId").stringValue = script;
            entry.FindPropertyRelative("requiresFlag").stringValue = requires ?? "";
            entry.FindPropertyRelative("consumedFlag").stringValue = consumed ?? "";
            entry.FindPropertyRelative("requiresItem").stringValue = requiresItem ?? "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Lets a character show the mark that says they are holding work for the Warden.</summary>
        public static void GiveOfferMarker(GameObject person)
        {
            var offers = new SerializedObject(Require<World.QuestOffers>(person));
            offers.FindProperty("markerSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Oathfire/Art/FX/glow.png");
            offers.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Wraps an object in a FlagGatedPresence, so it is only there while the story says so.</summary>
        public static GameObject Gate(GameObject body, string requiresFlag, string blockedByFlag = "")
        {
            var gate = new GameObject($"{body.name} (gate)", typeof(World.FlagGatedPresence));
            gate.transform.position = body.transform.position;
            body.transform.SetParent(gate.transform, true);
            var serialized = new SerializedObject(gate.GetComponent<World.FlagGatedPresence>());
            serialized.FindProperty("requiresFlag").stringValue = requiresFlag ?? "";
            serialized.FindProperty("blockedByFlag").stringValue = blockedByFlag ?? "";
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        /// <summary>A road out of a map: stand on it, tap Use, travel.</summary>
        public static GameObject BuildExit(Vector3 position, string destination, string promptKey, string flag, Vector3? arrival = null)
        {
            var exit = new GameObject($"Road to {destination}", typeof(CircleCollider2D), typeof(World.WorldInteractable), typeof(World.SceneExit));
            exit.transform.position = position;
            var serialized = new SerializedObject(exit.GetComponent<World.SceneExit>());
            serialized.FindProperty("destinationScene").stringValue = destination;
            serialized.FindProperty("promptKey").stringValue = promptKey;
            serialized.FindProperty("setsFlag").stringValue = flag ?? "";
            if (arrival.HasValue)
            {
                var marker = new GameObject("Arrival");
                marker.transform.SetParent(exit.transform, false);
                marker.transform.position = arrival.Value;
                serialized.FindProperty("arrivalPoint").objectReferenceValue = marker.transform;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return exit;
        }

        [System.Serializable] class SkillRow { public string id; public int level; public string ability; }
        [System.Serializable] class SkillTable { public SkillRow[] skills; }

        /// <summary>One way a spawned hostile looks; see World.ActorLook.</summary>
        public static World.ActorLook Look(string name, string rig, Color tint, float minScale, float maxScale, string killCounter = "") =>
            new World.ActorLook
            {
                name = name,
                rig = string.IsNullOrEmpty(rig) ? null : RigController(rig),
                tint = tint,
                minScale = minScale,
                maxScale = maxScale,
                killCounter = killCounter,
            };

        public static RuntimeAnimatorController RigController(string rig)
        {
            string folder = $"{Package}/Characters/{rig}/Animation Clips";
            string guid = AssetDatabase.FindAssets("t:AnimatorController", new[] { folder }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static Sprite RigIdle(string rig) =>
            AssetDatabase.LoadAllAssetsAtPath($"{Package}/Characters/{rig}/Idle.png").OfType<Sprite>().FirstOrDefault(s => s.name == "Idle_0_0");

        static void GiveThePlayerABody(GameObject player)
        {
            const string rig = Package + "/Characters/NPC3";

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                rig + "/Animation Clips/NPC3_20250926_184807_animator.controller");
            Animator animator = Require<Animator>(player);
            if (controller)
                animator.runtimeAnimatorController = controller;
            else
                Debug.LogError("[Oathfire] Player animator controller not found; the hero will not animate");

            var renderer = player.GetComponentInChildren<SpriteRenderer>();
            Sprite still = AssetDatabase.LoadAllAssetsAtPath(rig + "/Idle.png")
                .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Idle_0_0");  // first facing, first frame
            if (renderer && still)
                renderer.sprite = still;
            else
                Debug.LogError("[Oathfire] Player idle sprite not found; the hero will be invisible");
        }

        public static T Require<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component ? component : target.AddComponent<T>();
        }

        public static GameObject InstantiatePrefab(string path, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                Debug.LogWarning($"[Oathfire] Prefab missing: {path}");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return instance;
        }

        public static Sprite LoadSubSprite(string sheetPath, string spriteName) =>
            AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == spriteName);
    }
}
