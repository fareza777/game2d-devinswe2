using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oathfire.World
{
    /// <summary>
    /// A house door the hero can open and walk through. Opening lifts the door leaf off its arch and takes away
    /// the collider tile in the doorway; closing puts both back. The floor behind was opened when the map was
    /// composed (Tools/doors.py), and RoofFader lifts the roof while the hero is inside.
    /// </summary>
    [RequireComponent(typeof(WorldInteractable))]
    public class HouseDoor : MonoBehaviour
    {
        [SerializeField] Vector3Int cell;
        [SerializeField] Vector3Int outside;

        readonly List<(Tilemap map, TileBase tile)> removed = new List<(Tilemap, TileBase)>();
        WorldInteractable interactable;

        public bool IsOpen { get; private set; }
        public Vector3Int Cell => cell;
        public Vector3Int Outside => outside;
        /// <summary>The first floor cell through the door.</summary>
        public Vector3Int Inside => cell + (cell - outside);

        public void Configure(Vector3Int doorCell, Vector3Int outsideCell)
        {
            cell = doorCell;
            outside = outsideCell;
        }

        void Awake()
        {
            interactable = GetComponent<WorldInteractable>();
            interactable.SetPrompt("prompt.door.open");
            interactable.SetRadius(0.9f);
            interactable.Used += Toggle;
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        void Open()
        {
            removed.Clear();
            Grid grid = GetComponentInParent<Grid>();
            foreach (Tilemap map in grid ? grid.GetComponentsInChildren<Tilemap>() : new Tilemap[0])
            {
                TileBase tile = map.GetTile(cell);
                // The door leaf and whatever blocks the doorway; the arch above the opening stays.
                if (!tile || !(tile.name.StartsWith("Door") || map.name.StartsWith("Collider")))
                    continue;
                removed.Add((map, tile));
                map.SetTile(cell, null);
            }
            IsOpen = true;
            interactable.SetPrompt("prompt.door.close");
            Core.GameServices.Audio.PlaySfx("travel", 0.6f, 0.05f);
        }

        void Close()
        {
            foreach ((Tilemap map, TileBase tile) in removed)
                if (map)
                    map.SetTile(cell, tile);
            removed.Clear();
            IsOpen = false;
            interactable.SetPrompt("prompt.door.open");
            Core.GameServices.Audio.PlaySfx("ui_close", 0.8f, 0.05f);
        }
    }
}
