using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oathfire.EditorTools
{
    /// <summary>
    /// Opens each playable map and measures, from the player's spawn, whether the body starts inside a wall and
    /// how far it can travel in eight directions. A spawn that reads zero everywhere is a hero who cannot walk.
    /// </summary>
    public static class MovementDiagnostics
    {
        static readonly string[] Maps = { "Rennfall", "TradeRoad", "Greymarch" };

        [MenuItem("Oathfire/Diagnostics/Player Spawn Clearance")]
        public static void Run()
        {
            var report = new StringBuilder();
            int worldMask = LayerMask.GetMask("World");
            foreach (string map in Maps)
            {
                EditorSceneManager.OpenScene($"Assets/Oathfire/Scenes/{map}.unity", OpenSceneMode.Single);
                Physics2D.SyncTransforms();
                GameObject player = GameObject.FindWithTag("Player");
                var circle = player ? player.GetComponent<CircleCollider2D>() : null;
                if (!circle)
                {
                    report.AppendLine($"{map}: no player circle collider");
                    continue;
                }

                foreach (CompositeCollider2D wall in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
                {
                    report.AppendLine($"  walls {wall.name}: {wall.pathCount} paths, {wall.shapeCount} shapes, bounds {wall.bounds.size}");
                    wall.GenerateGeometry();
                    report.AppendLine($"  walls {wall.name} regenerated: {wall.pathCount} paths, {wall.shapeCount} shapes");
                }
                Physics2D.SyncTransforms();
                foreach (TilemapCollider2D tiles in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
                    report.AppendLine($"  tiles {tiles.name}: {tiles.shapeCount} shapes, merge {tiles.compositeOperation}");

                Vector2 centre = circle.bounds.center;
                float radius = circle.bounds.extents.x;
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(centre, radius, worldMask);
                report.AppendLine($"{map}: spawn {centre} radius {radius:0.00} overlaps {overlaps.Length}");
                foreach (Collider2D overlap in overlaps)
                    report.AppendLine($"    inside {overlap.name}");

                for (int step = 0; step < 8; step++)
                {
                    float angle = step * 45f * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    RaycastHit2D hit = Physics2D.CircleCast(centre, radius, direction, 3f, worldMask);
                    report.AppendLine($"    {step * 45,3} deg free {(hit ? hit.distance : 3f):0.00}");
                }
            }
            Debug.Log("[Oathfire] spawn clearance\n" + report);
        }
    }
}
