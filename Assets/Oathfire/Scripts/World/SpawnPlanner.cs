using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oathfire.World
{
    /// <summary>
    /// Finds where an enemy can appear and still be fought: on painted ground, clear of walls, trees and
    /// buildings, with a straight open approach to where it is heading. A ring drawn around the hearth ignores
    /// all of that, and the night's dead used to appear inside houses and behind tree lines, stuck and
    /// unreachable.
    /// </summary>
    public static class SpawnPlanner
    {
        const float BodyRadius = 0.45f;
        const int Tries = 48;

        static Tilemap ground;

        static int WorldMask => LayerMask.GetMask("World");

        public static bool IsOpen(Vector2 point) => IsOpen(point, BodyRadius);

        /// <summary>Open ground for a body of the given radius (a crowd packs tighter than a lone spawn needs).</summary>
        public static bool IsOpen(Vector2 point, float radius)
        {
            if (!ground)
            {
                GameObject found = GameObject.Find("Ground");
                ground = found ? found.GetComponent<Tilemap>() : null;
            }
            if (ground && !ground.HasTile(ground.WorldToCell(point)))
                return false;
            return !Physics2D.OverlapCircle(point, radius, WorldMask);
        }

        /// <summary>
        /// True when a body can walk in a straight line from one point to within <paramref name="stopShort"/> of the
        /// other. Aiming at the edge of a place rather than its centre matters: the hearth stands in a market
        /// square, and a line to the fire itself always crosses a stall even though the square is open.
        /// </summary>
        public static bool HasClearApproach(Vector2 from, Vector2 to, float stopShort = 1f)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude - stopShort;
            if (distance <= 0f)
                return true;
            return !Physics2D.CircleCast(from, BodyRadius * 0.7f, delta.normalized, distance, WorldMask);
        }

        /// <summary>
        /// A point on the flattened isometric ring between the two radii around <paramref name="centre"/> that is open
        /// and has a clear approach to <paramref name="goal"/>. Tries the preferred angle first, then anywhere.
        /// Falls back to the nearest open point, then to the centre itself.
        /// </summary>
        public static Vector3 FindOpenPoint(Vector2 centre, Vector2 goal, float minRadius, float maxRadius, float preferredAngle = float.NaN,
            float stopShort = 1f)
        {
            Vector3 fallback = centre;
            bool haveFallback = false;
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                float angle = !float.IsNaN(preferredAngle) && attempt < Tries / 2
                    ? preferredAngle + Random.Range(-0.7f, 0.7f)
                    : Random.Range(0f, Mathf.PI * 2f);
                // Later attempts come closer in, where the ground is usually more open.
                float shrink = Mathf.Lerp(1f, 0.55f, attempt / (float)Tries);
                float radius = Random.Range(minRadius, maxRadius) * shrink;
                var point = centre + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.5f);
                if (!IsOpen(point))
                    continue;
                if (HasClearApproach(point, goal, stopShort))
                    return point;
                if (!haveFallback)
                {
                    fallback = point;
                    haveFallback = true;
                }
            }
            return fallback;
        }

        /// <summary>
        /// An open point near <paramref name="centre"/> that keeps its distance from bodies already placed. The ring
        /// search above suits squares and roads; in a narrow ravine nearly every ring point is inside a wall, it
        /// falls back to the centre, and a whole group appears stacked on one spot. This walks a grid instead.
        /// </summary>
        public static Vector3 FindOpenPointApart(Vector2 centre, float maxRadius, IReadOnlyList<Vector3> taken, float apart = 0.7f)
        {
            // A snug fit first; a narrow pass may only allow a tighter one.
            foreach ((float body, float gap) in new[] { (BodyRadius, apart), (CrowdRadius, 0.55f) })
            {
                Vector2? found = SearchGrid(centre, maxRadius, taken, body, gap);
                if (found.HasValue)
                    return found.Value;
            }
            return centre;
        }

        const float CrowdRadius = 0.28f;

        static Vector2? SearchGrid(Vector2 centre, float maxRadius, IReadOnlyList<Vector3> taken, float body, float apart)
        {
            const float Step = 0.3f;
            var candidates = new List<Vector2>();
            for (float x = -maxRadius; x <= maxRadius; x += Step)
            {
                for (float y = -maxRadius * 0.5f; y <= maxRadius * 0.5f; y += Step * 0.5f)
                {
                    var point = centre + new Vector2(x, y);
                    if (!IsOpen(point, body))
                        continue;
                    bool crowded = false;
                    foreach (Vector3 other in taken)
                        crowded |= Vector2.Distance(point, other) < apart;
                    if (!crowded && HasClearApproach(point, centre, 0.2f))
                        candidates.Add(point);
                }
            }
            if (candidates.Count == 0)
                return null;
            // Nearest few first, so the group stays a group rather than scattering to the far end of the map.
            candidates.Sort((a, b) => (a - centre).sqrMagnitude.CompareTo((b - centre).sqrMagnitude));
            return candidates[Random.Range(0, Mathf.Min(4, candidates.Count))];
        }
    }
}
