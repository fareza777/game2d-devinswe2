using UnityEditor;
using UnityEngine;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Import rules for the game's own art and audio, so generated files are usable straight from the
    /// pipeline without anyone clicking through the inspector.
    /// </summary>
    public class OathfireAssetPostprocessor : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Oathfire/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Cinematic prints and portraits are shown large on screen: keep them sharp.
            importer.maxTextureSize = assetPath.Contains("/Cinematic/") ? 2048 : 1024;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ArtRoot))
                return;

            var importer = (AudioImporter)assetImporter;
            bool isMusic = assetPath.Contains("/Music/");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // Music streams from disk; short voice lines decompress into memory for instant playback.
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.6f : 0.75f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !isMusic;
        }
    }
}
