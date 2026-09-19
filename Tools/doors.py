"""Hangs doors in the pack's buildings and opens the houses behind them.

Most buildings in the tileset's example town have doorways but no doors: an arch tile (Wall C6 in timber,
Wall D6 in stone) spans an empty opening, which reads as a hole in the wall. The pack does have door leaves,
Door A1_<side>, drawn for exactly that opening, so every lone arch gets the leaf facing the same way.

A building with no arch at all can be given a door by hand in EXTRA_DOORS, keyed by stamp index, at a cell
checked in a preview render (the wall must face the viewer, or the door hides behind the building).

The example town's artist also filled every interior with collider tiles, so a house could never be entered.
open_interiors() clears the floor behind each door (under its roof, bounded by walls and furniture) and
records what the game needs: the door cell, the interior cells, and the roof cells that fade out while the
hero is inside.
"""

ARCHES = ("Wall C6_", "Wall D6_")
DOOR_LAYER = "Objects"
WALL_LAYERS = ("Walls", "WallDetail1", "WallDetail2")
ROOF_LAYERS = ("Roof1", "Roof2", "Roof3")
MAX_INTERIOR = 90

# stamp index -> [(x, y, tile)] in stamp-local cells.
EXTRA_DOORS = {
    6: [(2, 4, "Door A1_E")],  # the inn: ground floor of the front right wall
}

Cell = tuple[int, int]


def doors_for(stamp_index: int, pieces: list[dict]) -> list[tuple[int, int, str]]:
    """Door tiles to add to one stamp, in its own cell coordinates. Arches that already hold a door are skipped."""
    occupied = {(p["x"], p["y"]) for p in pieces if p["tile"].startswith("Door")}
    arches = {(p["x"], p["y"]): p["tile"] for p in pieces if p["tile"].startswith(ARCHES)}
    doors = []
    for piece in pieces:
        cell = (piece["x"], piece["y"])
        # A run of identical arches is an arcade or a covered walk, which people pass through. A doorway is a
        # single arch in its wall.
        in_arcade = any(arches.get((cell[0] + dx, cell[1] + dy)) == piece["tile"]
                        for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx, dy) != (0, 0))
        if piece["tile"].startswith(ARCHES) and cell not in occupied and not in_arcade:
            doors.append((cell[0], cell[1], f"Door A1_{piece['tile'][-1]}"))
            occupied.add(cell)
    doors += [door for door in EXTRA_DOORS.get(stamp_index, []) if (door[0], door[1]) not in occupied]
    return doors


def roof_cells(layers: dict[str, dict[Cell, str]]) -> set[Cell]:
    return set().union(*(set(layers.get(name, {})) for name in ROOF_LAYERS))


def hidden_by_roof(cell: Cell, roofs: set[Cell]) -> bool:
    """True when a roof drawn in front would cover someone standing here.

    A roof tile is painted upward from its cell, so it covers what stands a few rows behind it (larger x + y is
    further up the screen) and roughly in the same screen column (x - y).
    """
    depth, column = cell[0] + cell[1], cell[0] - cell[1]
    return any((rx + ry) < depth <= (rx + ry) + 6 and abs((rx - ry) - column) <= 2 for rx, ry in roofs)


def _near(cell: Cell, cells: set[Cell], reach: int) -> int:
    return sum((cell[0] + dx, cell[1] + dy) in cells for dx in range(-reach, reach + 1) for dy in range(-reach, reach + 1))


def _roof_groups(roofs: set[Cell]) -> list[set[Cell]]:
    """Roof cells joined into one group per building (touching cells, diagonals included)."""
    groups, seen = [], set()
    for start in roofs:
        if start in seen:
            continue
        group, stack = set(), [start]
        seen.add(start)
        while stack:
            x, y = stack.pop()
            group.add((x, y))
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    other = (x + dx, y + dy)
                    if other in roofs and other not in seen:
                        seen.add(other)
                        stack.append(other)
        groups.append(group)
    return groups


def open_interiors(layers: dict[str, dict[Cell, str]], blocked: set[Cell]) -> list[dict]:
    """Opens the floor behind every door and returns one record per enterable house.

    Mutates `layers` (collider tiles removed inside) and `blocked` (interior cells freed). A door whose inside
    cannot be told from its outside, or whose floor is too cramped to stand in, stays a closed prop.
    """
    walls = set().union(*(set(layers.get(name, {})) for name in WALL_LAYERS))
    furniture = set(layers.get("Objects", {}))
    ground = set().union(*(set(cells) for name, cells in layers.items() if name.startswith("Ground")))
    roofs = set().union(*(set(layers.get(name, {})) for name in ROOF_LAYERS))
    groups = _roof_groups(roofs)
    doors = [(cell, tile) for name in (DOOR_LAYER, "WallDetail1", "WallDetail2")
             for cell, tile in layers.get(name, {}).items() if tile.startswith("Door A1_")]

    houses = []
    for door, tile in sorted(doors):
        side = tile[-1]
        # The leaf lies in its wall; the two cells across the wall are inside and outside. Inside is under roof.
        across = [(door[0], door[1] + 1), (door[0], door[1] - 1)] if side in "EW" else [(door[0] + 1, door[1]), (door[0] - 1, door[1])]
        scores = [_near(cell, roofs, 1) for cell in across]
        if scores[0] == scores[1]:
            continue
        inside, outside = (across[0], across[1]) if scores[0] > scores[1] else (across[1], across[0])

        interior, stack = set(), [inside]
        while stack and len(interior) < MAX_INTERIOR:
            cell = stack.pop()
            if cell in interior or cell in walls or cell in furniture or cell not in ground or _near(cell, roofs, 1) == 0:
                continue
            interior.add(cell)
            stack.extend((cell[0] + dx, cell[1] + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        # Too cramped to stand in, or so large the fill has leaked out through a gap into the street: not a house.
        if len(interior) < 3 or len(interior) >= MAX_INTERIOR:
            continue
        # Two leaves of one doorway lead into the same room; the first one owns it.
        if any(interior & {(c["x"], c["y"]) for c in house["interior"]} for house in houses):
            continue

        for cell in interior:
            blocked.discard(cell)
            for name in list(layers):
                if name.startswith("Collider"):
                    layers[name].pop(cell, None)

        roof = set().union(*(group for group in groups
                             if any(_near(cell, group, 3) for cell in interior)), set())
        houses.append({
            "door": {"x": door[0], "y": door[1]},
            "tile": tile,
            "outside": {"x": outside[0], "y": outside[1]},
            "interior": [{"x": x, "y": y} for x, y in sorted(interior)],
            "roof": [{"x": x, "y": y} for x, y in sorted(roof)],
        })
    return houses
