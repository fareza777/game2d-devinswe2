"""Clears the collider tiles in the example town that block nothing you can see.

The tileset's example scene has hundreds of collider tiles on open ground: plain grass, dirt, straw, rubble,
plank floors and paving, with no wall, prop, tree or slope on them. In Greymarch 592 of them sat on the streets,
which is what made the city snag at every corner. A collider stays when there is a reason to see for it:
anything standing in the cell, a slope or cliff edge, or the edge of the map (nothing to walk onto past it).
"""

Cell = tuple[int, int]

SOLID_LAYERS = ("Walls", "WallDetail1", "WallDetail2", "Objects", "BrokenObjects")
# Families of ground tile that are flat and walkable in this tileset (checked in a preview render).
WALKABLE_GROUND = ("Ground A", "Ground B", "Ground D", "Ground E", "Ground F", "Ground H", "Ground I", "Ground J")
# Anything named like this in any layer is a reason for the cell to block.
BLOCKING_NAMES = ("Tree", "Stone", "Misc", "Wall", "Door", "Flora B", "Water", "Ground G")


def clear_phantom_colliders(layers: dict[str, dict[Cell, str]], blocked: set[Cell]) -> int:
    """Removes collider tiles (and blocked marks) that stand on open, walkable ground. Returns how many."""
    solid = set().union(*(set(layers.get(name, {})) for name in SOLID_LAYERS))
    ground_layers = [cells for name, cells in layers.items() if name.startswith("Ground")]
    ground = set().union(*(set(cells) for cells in ground_layers))
    collider_layers = [cells for name, cells in layers.items() if name.startswith("Collider")]

    removed = 0
    for colliders in collider_layers:
        for cell in list(colliders):
            if cell in solid:
                continue
            tiles = [cells[cell] for name, cells in layers.items() if not name.startswith("Collider") and cell in cells]
            if not tiles or any(tile.startswith(BLOCKING_NAMES) for tile in tiles):
                continue
            if not all(tile.startswith(WALKABLE_GROUND) or tile.startswith("Shadow") for tile in tiles):
                continue
            # The edge of the painted ground is a boundary, not a snag.
            if not all((cell[0] + dx, cell[1] + dy) in ground for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                continue
            del colliders[cell]
            blocked.discard(cell)
            removed += 1
    return removed
