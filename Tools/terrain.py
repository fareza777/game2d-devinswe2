"""Ground work shared by the map composers: reachability, opening a blocked route, and softening edges.

Two problems kept coming back. Places were laid out as rectangles, so grass met paving along a dead straight
line and the ground read as coloured squares with borders. And anchors (a quarry, a woodpile) were chosen by
distance alone, so some stood behind a wall where nobody could ever reach them.
"""
import random
from collections import deque
from pathlib import Path

ENVIRONMENT = Path(__file__).resolve().parent.parent / "Assets" / "SmallScaleInt" / "Fantasy kingdom Tileset" / "Environment"
TILES = ENVIRONMENT / "Tiles"
ANIMATED = ENVIRONMENT / "Animated tiles"

# Overlay pieces, not surfaces: each one is a scatter of tufts or pebbles on transparent ground, so they
# break a hard edge without covering what is underneath.
GRASS_DECALS = ["Ground A3", "Ground A5", "Ground A7", "Ground A9", "Ground A20", "Ground A21"]
STONE_DECALS = ["Ground E9", "Ground E11", "Ground E13", "Ground E14"]
DIRT_DECALS = ["Ground E1", "Ground E2", "Ground E5"]
ROTATIONS = ["_N", "_E", "_S", "_W"]


def reachable(walkable: set[tuple[int, int]], start: tuple[int, int]) -> set[tuple[int, int]]:
    """Every cell a body can actually walk to from <start>, four ways, never diagonally through a corner."""
    if start not in walkable:
        start = min(walkable, key=lambda c: (c[0] - start[0]) ** 2 + (c[1] - start[1]) ** 2)
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        for step in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            cell = (x + step[0], y + step[1])
            if cell in walkable and cell not in seen:
                seen.add(cell)
                queue.append(cell)
    return seen


def structural(layers: dict[str, dict[tuple[int, int], str]]) -> set[tuple[int, int]]:
    """Cells held by a building: walls, roofs, doorways. Everything else that blocks is a prop."""
    held = set()
    for name, cells in layers.items():
        for cell, tile in cells.items():
            if tile.startswith(("Wall", "Roof", "Door")) or name.startswith(("Roof", "Wall")):
                held.add(cell)
    return held


def open_route(layers: dict[str, dict[tuple[int, int], str]], blocked: set[tuple[int, int]],
               start: tuple[int, int], goal: tuple[int, int], width: int = 1) -> int:
    """Clears the props standing in the way of the main road through a place.

    A cropped or stamped town keeps the example map's own gate decorations, and those have colliders: the
    front gate of a city can be sealed by two barrels and a crate while the only way in is a detour nobody
    finds. Walls are never touched — only the loose things standing on the road.
    """
    ground = set(layers.get("Ground", {}))
    held = structural(layers)
    passable = ground - held
    if start not in passable or goal not in passable:
        return 0

    previous = {start: None}
    queue = deque([start])
    while queue:
        cell = queue.popleft()
        if cell == goal:
            break
        for step in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (cell[0] + step[0], cell[1] + step[1])
            if nxt in passable and nxt not in previous:
                previous[nxt] = cell
                queue.append(nxt)
    if goal not in previous:
        return 0

    path = []
    cell = goal
    while cell is not None:
        path.append(cell)
        cell = previous[cell]

    corridor = {(c[0] + dx, c[1] + dy) for c in path
                for dx in range(-width, width + 1) for dy in range(-width, width + 1)}
    cleared = 0
    for cell in corridor:
        if cell in held or cell not in ground:
            continue
        if cell in blocked:
            blocked.discard(cell)
            cleared += 1
        # The prop itself goes too: walking through a crate looks worse than the crate not being there.
        for name in ("Objects", "BrokenObjects", "Ground 3"):
            tiles = layers.get(name)
            if tiles and cell in tiles and not tiles[cell].startswith(("Wall", "Roof", "Door")):
                del tiles[cell]
    return cleared


def clear_lane(layers: dict[str, dict[tuple[int, int], str]], blocked: set[tuple[int, int]],
               lane: int, from_x: int, to_x: int, half: int = 1) -> int:
    """Keeps the main street of a place actually walkable end to end.

    Following a path around the obstacles is not enough: the road the player is looking at has to be the road
    that works, or they stand at a gate that is visibly a gate and cannot go through it. Walls are untouched;
    stalls and crates standing in the roadway are moved aside.
    """
    ground = set(layers.get("Ground", {}))
    held = structural(layers)
    cleared = 0
    for x in range(min(from_x, to_x), max(from_x, to_x) + 1):
        for y in range(lane - half, lane + half + 1):
            cell = (x, y)
            if cell in held or cell not in ground:
                continue
            if cell in blocked:
                blocked.discard(cell)
                cleared += 1
            for name in ("Objects", "BrokenObjects", "Ground 3"):
                tiles = layers.get(name)
                if tiles and cell in tiles and not tiles[cell].startswith(("Wall", "Roof", "Door")):
                    del tiles[cell]
    return cleared


def blend_edges(layers: dict[str, dict[tuple[int, int], str]], rng: random.Random,
                surface_layer: str = "Ground 2", decal_layer: str = "Ground 3",
                grass_prefixes: tuple[str, ...] = ("Ground A2",), paved_prefixes: tuple[str, ...] = (),
                blocked: set[tuple[int, int]] | None = None) -> int:
    """Scatters tufts and pebbles where one surface meets another, and thinly over the fields.

    A hard boundary between two flat surfaces is what makes a laid-out map look like coloured squares. The
    pack ships overlay pieces for exactly this; used along the seams they read as grass creeping over the
    kerb and grit worn off the road.
    """
    surface = layers.get(surface_layer, {})
    decals = layers.setdefault(decal_layer, {})
    blocked = blocked or set()

    def kind(cell):
        tile = surface.get(cell)
        if tile is None:
            return None
        if tile.startswith(grass_prefixes):
            return "grass"
        if paved_prefixes and tile.startswith(paved_prefixes):
            return "paved"
        return "other"

    placed = 0
    for cell in list(surface):
        if cell in decals or cell in blocked:
            continue
        here = kind(cell)
        if here is None:
            continue
        neighbours = {kind((cell[0] + dx, cell[1] + dy)) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))}
        seam = any(other is not None and other != here for other in neighbours)
        chance = 0.5 if seam else (0.1 if here == "grass" else 0.04)
        if rng.random() > chance:
            continue
        family = (GRASS_DECALS if here == "grass" else STONE_DECALS if here == "paved" else DIRT_DECALS)
        decals[cell] = rng.choice(family) + rng.choice(ROTATIONS)
        placed += 1
    return placed


def drop_unknown_tiles(layers: dict[str, dict[tuple[int, int], str]]) -> int:
    """Removes cells whose tile came out of the example scene as an unresolved GUID (named "?abc123").

    Unity cannot paint them either, so they are holes in the map rather than decoration, and a hole in the
    ground layer is ground the spawner will not stand anything on.
    """
    dropped = 0
    for cells in layers.values():
        for cell in [cell for cell, tile in cells.items() if tile.startswith("?")]:
            del cells[cell]
            dropped += 1
    return dropped


def check_tiles(layers: dict[str, dict[tuple[int, int], str]]) -> list[str]:
    """Names every tile the map asks for that the pack does not have.

    A misspelt rotation (the pack has Ground F4_S but no Ground F4_W) is silent everywhere else: Unity paints
    nothing, the map gets a hole, and the hole reads as ground that cannot be stood on.
    """
    missing: dict[str, int] = {}
    for cells in layers.values():
        for tile in cells.values():
            if tile in missing:
                missing[tile] += 1
            elif not (TILES / f"{tile}.asset").exists() and not (ANIMATED / f"{tile}.asset").exists():
                missing[tile] = 1
    return [f"{tile} x{count}" for tile, count in sorted(missing.items())]


def fence_void(layers: dict[str, dict[tuple[int, int], str]], walkable: set[tuple[int, int]]) -> int:
    """Puts an invisible edge collider on every completely empty cell that touches walkable ground.

    Without it a hero who reaches an unwalled edge steps onto the void and the screen goes black.
    Anything with content — ground, walls, props, deco — counts as occupied; only true emptiness is fenced.
    """
    occupied = {cell for cells in layers.values() for cell in cells}
    fence: dict[tuple[int, int], str] = {}
    for x, y in walkable:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                cell = (x + dx, y + dy)
                if cell not in occupied and cell not in fence:
                    fence[cell] = "Shadow5_E"
    if fence:
        layers.setdefault("Colliders(edge)", {}).update(fence)
    return len(fence)
