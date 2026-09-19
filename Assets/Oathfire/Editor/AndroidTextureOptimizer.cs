using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// The tileset ships every texture uncompressed (fine on PC). On Android that is ~3.3 GB of texture RAM and the
    /// low-memory killer terminates the app during scene load. This adds Android-only ASTC overrides:
    /// big character/animation sheets use ASTC 5x5, tiles/UI/atlases keep sharper ASTC 4x4. Standalone is untouched.
    /// </summary>
    public static class AndroidTextureOptimizer
    {
        const string AndroidPlatform = "Android";
        const string PackageRoot = "Assets";
        const int LargeSheetMinSize = 1024;

        static readonly string[] SheetFolders = { "/Characters/", "/Animations/" };

        [MenuItem("Oathfire/Optimize Textures For Android")]
        public static void Optimize()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PackageRoot });
            var changed = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;
                    if (ApplyAndroidOverride(importer, path))
                    {
                        importer.SaveAndReimport();
                        changed.Add(path);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int atlases = OptimizeSpriteAtlases();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Oathfire] Android texture overrides applied to {changed.Count} of {guids.Length} textures, {atlases} sprite atlases.");
        }

        static bool ApplyAndroidOverride(TextureImporter importer, string path)
        {
            TextureImporterFormat format = ChooseFormat(importer, path);
            var settings = importer.GetPlatformTextureSettings(AndroidPlatform);
            if (settings.overridden && settings.format == format)
                return false;

            settings.overridden = true;
            settings.format = format;
            settings.maxTextureSize = importer.maxTextureSize;
            settings.compressionQuality = (int)TextureCompressionQuality.Normal;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        static TextureImporterFormat ChooseFormat(TextureImporter importer, string path)
        {
            bool isSheetFolder = false;
            foreach (string folder in SheetFolders)
                isSheetFolder |= path.Contains(folder);

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            bool isLargeSheet = isSheetFolder && Mathf.Max(width, height) >= LargeSheetMinSize;
            return isLargeSheet ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_4x4;
        }

        static int OptimizeSpriteAtlases()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { PackageRoot }))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
                if (!atlas)
                    continue;
                var settings = atlas.GetPlatformSettings(AndroidPlatform);
                settings.overridden = true;
                settings.format = TextureImporterFormat.ASTC_4x4;
                atlas.SetPlatformSettings(settings);
                EditorUtility.SetDirty(atlas);
                count++;
            }
            return count;
        }
    }
}
