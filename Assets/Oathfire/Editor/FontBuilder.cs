using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Turns the downloaded TTFs into TMP font assets and makes the body face the project default, so no
    /// screen falls back to Unity's stock sans and looks like a prototype.
    /// </summary>
    public static class FontBuilder
    {
        const string FontSource = "Assets/Oathfire/Art/Fonts";
        const string FontOutput = "Assets/Oathfire/Resources/Fonts";
        const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Dynamic atlases grow as glyphs are used, so Indonesian and English both render without pre-baking.
        const int SamplingPointSize = 90;
        const int AtlasPadding = 9;
        const int AtlasSize = 2048; // one atlas holds the whole character set, so no multi-atlas juggling

        /// <summary>Every glyph the game can show: ASCII plus the punctuation the writing actually uses.</summary>
        static readonly string CharacterSet =
            string.Concat(System.Linq.Enumerable.Range(32, 95).Select(code => (char)code)) +
            "·—–‘’“”…×àáâäèéêëìíîïòóôöùúûüñçÀÁÂÄÈÉÊËÌÍÎÏÒÓÔÖÙÚÛÜÑÇ" +
            // Marks the UI uses: continue arrow, bullets, ticks. Missing glyphs draw as blank spaces.
            "▼▲►◄•◆✓›‹";

        [MenuItem("Oathfire/Build Fonts")]
        public static void Build()
        {
            Directory.CreateDirectory(FontOutput);
            TMP_FontAsset body = null;

            foreach (string path in Directory.GetFiles(FontSource, "*.ttf").Select(p => p.Replace('\\', '/')))
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (!font)
                {
                    Debug.LogError($"[Oathfire] Could not load font {path}");
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                string output = $"{FontOutput}/{name}.asset";

                // Always rebuild from scratch: baking glyphs into an existing asset can leave the atlas
                // texture outside the asset file, which ships as a broken glyph table and garbled text.
                AssetDatabase.DeleteAsset(output);

                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
                    GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
                asset.name = name;
                AssetDatabase.CreateAsset(asset, output);

                // Bake every glyph while the asset is still dynamic, save the atlas into the asset file, and
                // only then freeze it. Freezing first leaves an empty table and the text renders as nonsense.
                if (!asset.TryAddCharacters(CharacterSet, out string missing))
                    Debug.LogWarning($"[Oathfire] {name} could not add some glyphs: {missing}");

                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (Texture2D atlas in asset.atlasTextures)
                    if (atlas && !AssetDatabase.Contains(atlas))
                        AssetDatabase.AddObjectToAsset(atlas, asset);

                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(asset);
                Debug.Log($"[Oathfire] {name}: {asset.characterTable.Count} glyphs, {asset.atlasTextures.Length} atlas texture(s)");
                if (name == "EBGaramond-Regular")
                    body = asset;
                Debug.Log($"[Oathfire] font asset ready: {name}");
            }

            if (body)
                SetDefaultFont(body);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SetDefaultFont(TMP_FontAsset body)
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettings);
            if (!settings)
            {
                Debug.LogError($"[Oathfire] TMP settings not found at {TmpSettings}");
                return;
            }
            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty("m_defaultFontAsset");
            if (property == null)
            {
                Debug.LogError("[Oathfire] TMP settings has no m_defaultFontAsset property");
                return;
            }
            property.objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log("[Oathfire] default TMP font set to EB Garamond");
        }
    }
}
