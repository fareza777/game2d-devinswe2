using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Oathfire.EditorTools
{
    /// <summary>Prints what Unity actually thinks the generated art is, when an asset refuses to load.</summary>
    public static class ArtDiagnostics
    {
        [MenuItem("Oathfire/Diagnose Art Import")]
        public static void Run()
        {
            foreach (string path in new[]
                     {
                         "Assets/Oathfire/Resources/Cinematic/p1_valley.png",
                         "Assets/Oathfire/Art/FX/glow.png",
                         "Assets/Oathfire/Resources/Portraits/brann.png",
                     })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Debug.Log($"[Diag] {path}\n  importer={(importer ? $"type={importer.textureType} mode={importer.spriteImportMode} npot={importer.npotScale} max={importer.maxTextureSize}" : "NONE")}" +
                          $"\n  objects=[{string.Join(", ", all.Select(o => o ? $"{o.GetType().Name}:{o.name}" : "null"))}]" +
                          $"\n  loadAsSprite={(sprite ? sprite.name : "null")}");
            }
        }
    }
}
