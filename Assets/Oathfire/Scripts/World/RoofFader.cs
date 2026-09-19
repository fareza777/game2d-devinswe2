using System.Collections.Generic;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oathfire.World
{
    /// <summary>
    /// Keeps the hero visible around buildings. Characters sort under roofs, as the tileset's artist intended, so
    /// a roof is lifted almost away while the hero stands on the floor beneath it, and thinned while the hero
    /// walks behind the building it belongs to. Each building's roof is faded on its own, never every roof.
    /// </summary>
    public class RoofFader : MonoBehaviour
    {
        const float InsideAlpha = 0.12f;
        const float BehindAlpha = 0.45f;
        const float FadeSpeed = 4f;
        const float CheckSeconds = 0.12f;

        class Roof
        {
            public readonly List<Vector3Int> cells = new List<Vector3Int>();
            public float alpha = 1f;
            public float target = 1f;
        }

        [System.Serializable]
        public class HouseArea
        {
            public Vector3Int[] interior;
            public Vector3Int[] roof;
        }

        [SerializeField] List<HouseArea> houses = new List<HouseArea>();

        readonly Dictionary<Vector3Int, HouseArea> houseAt = new Dictionary<Vector3Int, HouseArea>();
        readonly List<Roof> roofs = new List<Roof>();
        readonly Dictionary<Vector3Int, Roof> roofAt = new Dictionary<Vector3Int, Roof>();
        Tilemap[] roofMaps;
        Tilemap ground;
        float nextCheck;

        public static RoofFader Instance { get; private set; }

        /// <summary>How faded the roof over or in front of the hero currently is (1 = solid); read by the smoke test.</summary>
        public float LowestAlpha { get; private set; } = 1f;

        void Awake() => Instance = this;

        /// <summary>Set when the scene is built: each enterable house's floor and the roof cells over it.</summary>
        public void AddHouse(Vector3Int[] interior, Vector3Int[] roof) =>
            houses.Add(new HouseArea { interior = interior, roof = roof });

        void Start()
        {
            var maps = new List<Tilemap>();
            foreach (Tilemap map in GetComponentsInChildren<Tilemap>())
            {
                if (map.name.StartsWith("Roof"))
                    maps.Add(map);
                else if (map.name == "Ground")
                    ground = map;
            }
            roofMaps = maps.ToArray();
            GroupRoofs();
            foreach (HouseArea house in houses)
                foreach (Vector3Int cell in house.interior)
                    houseAt[cell] = house;
        }

        /// <summary>Touching roof cells, across all roof layers, form one building's roof.</summary>
        void GroupRoofs()
        {
            var all = new HashSet<Vector3Int>();
            foreach (Tilemap map in roofMaps)
            {
                map.CompressBounds();
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                    if (map.HasTile(cell))
                    {
                        all.Add(new Vector3Int(cell.x, cell.y, 0));
                        map.SetTileFlags(cell, TileFlags.None);
                    }
            }

            foreach (Vector3Int start in all)
            {
                if (roofAt.ContainsKey(start))
                    continue;
                var roof = new Roof();
                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                roofAt[start] = roof;
                while (stack.Count > 0)
                {
                    Vector3Int cell = stack.Pop();
                    roof.cells.Add(cell);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var next = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                            if (all.Contains(next) && !roofAt.ContainsKey(next))
                            {
                                roofAt[next] = roof;
                                stack.Push(next);
                            }
                        }
                }
                roofs.Add(roof);
            }
        }

        void Update()
        {
            if (Time.time >= nextCheck && ground && PlayerHealth.Instance)
            {
                nextCheck = Time.time + CheckSeconds;
                ChooseTargets(ground.WorldToCell(FeetOf(PlayerHealth.Instance)));
            }

            float lowest = 1f;
            foreach (Roof roof in roofs)
            {
                if (!Mathf.Approximately(roof.alpha, roof.target))
                {
                    roof.alpha = Mathf.MoveTowards(roof.alpha, roof.target, Time.deltaTime * FadeSpeed);
                    Paint(roof);
                }
                lowest = Mathf.Min(lowest, roof.alpha);
            }
            LowestAlpha = lowest;
        }

        static Vector3 FeetOf(PlayerHealth hero)
        {
            var feet = hero.GetComponent<CircleCollider2D>();
            return feet ? feet.bounds.center : hero.transform.position;
        }

        /// <summary>
        /// A roof tile is painted upward from its cell, so it covers whoever stands a few rows behind it (larger
        /// x + y is further up the screen) in roughly the same screen column (x - y). Standing right beside or
        /// under the roof's own cells means being inside.
        /// </summary>
        void ChooseTargets(Vector3Int hero)
        {
            foreach (Roof roof in roofs)
                roof.target = 1f;

            // Inside a house: its own roof lifts, however tall the building and wherever its roof tiles sit.
            if (houseAt.TryGetValue(hero, out HouseArea inside))
                foreach (Vector3Int cell in inside.roof)
                    if (roofAt.TryGetValue(cell, out Roof over))
                        over.target = InsideAlpha;

            int depth = hero.x + hero.y;
            int column = hero.x - hero.y;
            for (int back = 0; back <= 6; back++)
                for (int side = -2; side <= 2; side++)
                {
                    // Cells in front of the hero on screen: smaller depth, nearby column.
                    int d = depth - back;
                    int c = column + side;
                    if ((d + c) % 2 != 0)
                        continue;
                    var cell = new Vector3Int((d + c) / 2, (d - c) / 2, 0);
                    if (!roofAt.TryGetValue(cell, out Roof roof))
                        continue;
                    float alpha = back <= 1 ? InsideAlpha : BehindAlpha;
                    roof.target = Mathf.Min(roof.target, alpha);
                }
        }

        void Paint(Roof roof)
        {
            var colour = new Color(1f, 1f, 1f, roof.alpha);
            foreach (Tilemap map in roofMaps)
                foreach (Vector3Int cell in roof.cells)
                    if (map.HasTile(cell))
                        map.SetColor(cell, colour);
        }
    }
}
