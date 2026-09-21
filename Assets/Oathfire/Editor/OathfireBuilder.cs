using System.IO;
using System.Linq;
using Oathfire.Cinematic;
using Oathfire.Core;
using Oathfire.Dialogue;
using Oathfire.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Builds Oathfire's scenes from code so the game's structure lives in version control as C#, not as
    /// binary scene files, and can be regenerated after any change.
    /// </summary>
    public static class OathfireBuilder
    {
        const string SceneFolder = "Assets/Oathfire/Scenes";
        const string FxFolder = "Assets/Oathfire/Art/FX";
        const string UiSheet = "Assets/SmallScaleInt/Fantasy kingdom Tileset/Example scene/UI/UISprites.png";
        const string ApkPath = "Builds/Android/Oathfire.apk";
        const string PackageId = "com.fajar.oathfire";

        [MenuItem("Oathfire/Build Scenes")]
        public static void BuildScenes()
        {
            Directory.CreateDirectory(SceneFolder);
            ReimportGameArt();
            BuildBootScene();
            BuildTitleScene();
            BuildOpeningScene();
            BuildPrologueScene();

            // The core scenes lead the list; the maps their own builders registered stay after them. Replacing the
            // whole list here used to drop Rennfall, the trade road and Greymarch from every build that followed.
            EditorBuildSettingsScene[] core = new[] { "Boot", "Title", "Opening", "Prologue" }
                .Select(name => new EditorBuildSettingsScene($"{SceneFolder}/{name}.unity", true))
                .ToArray();
            EditorBuildSettings.scenes = core
                .Concat(EditorBuildSettings.scenes.Where(scene => core.All(entry => entry.path != scene.path)))
                .ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Oathfire] Scenes rebuilt");
        }

        [MenuItem("Oathfire/Build Android APK")]
        public static void BuildAndroid()
        {
            AndroidTextureOptimizer.Optimize();

            PlayerSettings.productName = "Oathfire";
            PlayerSettings.companyName = "Fajar";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SplashScreen.show = false; // the game has its own ember splash
            ApplyAppIcon();
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[Oathfire] Android build: {report.summary.result}, errors={report.summary.totalErrors}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Windows build used for automated play-throughs on this machine: same scenes and content as the
        /// APK, in a phone-shaped window so the portrait layout is what gets tested.
        /// </summary>
        [MenuItem("Oathfire/Build Windows Test Player")]
        public static void BuildWindows()
        {
            PlayerSettings.productName = "Oathfire";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SplashScreen.show = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = "Builds/Windows/Oathfire.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[Oathfire] Windows build: {report.summary.result}, errors={report.summary.totalErrors}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        public static void BuildAll()
        {
            BuildScenes();
            BuildAndroid();
        }

        /// <summary>Rebuilds every map scene in one batch: the shared UI and systems live in MapScene,
        /// so a change to any of them means all the maps need it.</summary>
        public static void BuildAllMaps()
        {
            BuildScenes();
            RennfallSceneBuilder.Build();
            TradeRoadSceneBuilder.Build();
            GreymarchSceneBuilder.Build();
            HollowSceneBuilder.Build();
            KeepSceneBuilder.Build();
            SaltpansSceneBuilder.Build();
            BurnpitsSceneBuilder.Build();
            WindrestSceneBuilder.Build();
            ChalkpitSceneBuilder.Build();
        }

        /// <summary>
        /// Art generated by the tool pipeline is written straight into the project, so Unity may hold an
        /// import from before the postprocessor existed (textures imported as Default, not Sprite).
        /// Force-reimport them so every generated PNG is a usable sprite.
        /// </summary>
        static void ReimportGameArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Oathfire" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)
                    continue;
                // Set it on the importer itself rather than trusting import-time defaults, which have
                // repeatedly come back as Sprite/Multiple for the generated cinematic panels.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();

                var check = (TextureImporter)AssetImporter.GetAtPath(path);
                bool loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Debug.Log($"[Oathfire] reimported {path}: type={check.textureType} mode={check.spriteImportMode} loadsAsSprite={loaded}");
            }
            AssetDatabase.Refresh();
        }

        static Scene NewScene(string name)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.063f, 0.051f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.name = "EventSystem";
            return scene;
        }

        static void SaveScene(Scene scene, string name)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{name}.unity");
        }

        static void BuildBootScene()
        {
            Scene scene = NewScene("Boot");
            var bootstrap = new GameObject("GameBootstrap", typeof(GameBootstrap));

            var splashGo = new GameObject("SplashScreen", typeof(SplashScreen));
            var splash = new SerializedObject(splashGo.GetComponent<SplashScreen>());
            splash.FindProperty("glowSprite").objectReferenceValue = LoadSprite($"{FxFolder}/glow.png");
            splash.FindProperty("sigilSprite").objectReferenceValue = LoadSprite("Assets/Oathfire/Art/Brand/splash_sigil.png");
            splash.FindProperty("logoSprite").objectReferenceValue = LoadSprite("Assets/Oathfire/Resources/UI/logo_oathfire.png");
            splash.ApplyModifiedPropertiesWithoutUndo();
            _ = bootstrap;
            SaveScene(scene, "Boot");
        }

        static void BuildTitleScene()
        {
            Scene scene = NewScene("Title");
            var titleGo = new GameObject("TitleScreen", typeof(TitleScreen));
            var title = new SerializedObject(titleGo.GetComponent<TitleScreen>());
            title.FindProperty("keyArt").objectReferenceValue = LoadSprite("Assets/Oathfire/Resources/Cinematic/p1_valley.png");
            title.FindProperty("glowSprite").objectReferenceValue = LoadSprite($"{FxFolder}/glow.png");
            title.FindProperty("logo").objectReferenceValue = LoadSprite("Assets/Oathfire/Resources/UI/logo_oathfire.png");
            title.FindProperty("buttonPlate").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_16");
            title.FindProperty("menuMusic").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Oathfire/Resources/Music/menu_theme.mp3");
            title.ApplyModifiedPropertiesWithoutUndo();
            SaveScene(scene, "Title");
        }

        static void BuildOpeningScene()
        {
            Scene scene = NewScene("Opening");
            var playerGo = new GameObject("CinematicPlayer", typeof(CinematicPlayer));
            var player = new SerializedObject(playerGo.GetComponent<CinematicPlayer>());
            player.FindProperty("cinematicId").stringValue = "opening";
            player.FindProperty("glowSprite").objectReferenceValue = LoadSprite($"{FxFolder}/glow.png");
            player.FindProperty("watcherSprite").objectReferenceValue = LoadSprite($"{FxFolder}/watcher.png");
            player.FindProperty("emberSprite").objectReferenceValue = LoadSprite($"{FxFolder}/ember.png");
            player.ApplyModifiedPropertiesWithoutUndo();
            SaveScene(scene, "Opening");
        }

        /// <summary>Placeholder chapter scene: proves dialogue, save and flow work end to end until Milestone 2.</summary>
        static void BuildPrologueScene()
        {
            Scene scene = NewScene("Prologue");
            var dialogueGo = new GameObject("Dialogue", typeof(DialogueRunner), typeof(DialoguePanel));
            var panel = new SerializedObject(dialogueGo.GetComponent<DialoguePanel>());
            panel.FindProperty("boxSprite").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_0");
            panel.FindProperty("portraitFrame").objectReferenceValue = LoadSubSprite(UiSheet, "UISprites_101");
            panel.ApplyModifiedPropertiesWithoutUndo();

            var runner = new SerializedObject(dialogueGo.GetComponent<DialogueRunner>());
            runner.FindProperty("panel").objectReferenceValue = dialogueGo.GetComponent<DialoguePanel>();
            runner.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("ProloguePlaceholder", typeof(ProloguePlaceholder));
            SaveScene(scene, "Prologue");
        }

        /// <summary>
        /// The hooded warden cupping the oathfire (Art/Brand, from Tools/make_brand_art.py). Legacy and round icons
        /// use the full picture; the adaptive icon's background sets it a little smaller, because launchers crop
        /// adaptive icons to roughly their middle two thirds.
        /// </summary>
        [MenuItem("Oathfire/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            const string Brand = "Assets/Oathfire/Art/Brand/";
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Brand + "icon_1024.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Brand + "icon_adaptive_background.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Brand + "icon_adaptive_foreground.png");
            if (!full || !background || !foreground)
            {
                Debug.LogError("[Oathfire] App icon textures are missing in Art/Brand");
                return;
            }

            NamedBuildTarget android = NamedBuildTarget.Android;
            foreach (PlatformIconKind kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (PlatformIcon icon in icons)
                    icon.SetTexture(full);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in adaptive)
            {
                icon.SetTexture(background, 0);
                icon.SetTexture(foreground, 1);
            }
            PlayerSettings.SetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Oathfire] app icon applied ({adaptive.Length} adaptive, legacy and round sizes)");
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite)
                Debug.LogError($"[Oathfire] Sprite not found: {path}");
            return sprite;
        }

        static Sprite LoadSubSprite(string sheetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
            if (!sprite)
                Debug.LogError($"[Oathfire] Sub-sprite {spriteName} not found in {sheetPath}");
            return sprite;
        }
    }
}
