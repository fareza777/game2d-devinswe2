"""Lays out Rennfall deliberately instead of cutting a rectangle out of someone else's town.

Cropping a region of the pack's example map chops buildings in half and throws away the reasoning behind
the layout, which is why it read as scattered props. This places whole buildings on a plan: a cobbled
square with the oathfire at its centre, two streets crossing it, houses set back along those streets with
their doors facing them, work yards behind, and forest closing the valley in.

Writes Docs/rennfall_map.json (read by the Unity scene builder) and a preview PNG.
"""
import json
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from tileset_preview import render  # noqa: E402
from doors import doors_for, hidden_by_roof, open_interiors, roof_cells  # noqa: E402
from walkability import clear_phantom_colliders  # noqa: E402
from terrain import check_tiles, drop_unknown_tiles, reachable, fence_void  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
STAMPS = json.loads((PROJECT / "Docs" / "tileset_stamps.json").read_text(encoding="utf-8"))["stamps"]

SIZE = 46
CENTRE = SIZE // 2
SQUARE = 3          # half-width of the cobbled square: 7 cells across, a village square
STREET = 1          # half-width of the streets: 3 cells, wide enough for a cart and no wider
VILLAGE = 14        # how far the settled ground reaches before the forest

# Tiles chosen off the palette the pack's own artist used, so nothing looks imported from elsewhere.
DIRT = "Ground A1_E"
GRASS = ["Ground A2_N", "Ground A2_E"]
# Variants are rotations of the same stone, not different stones. A darker tile mixed into the
# paving does not read as variety — it reads as potholes in the road.
COBBLE = ["Ground D1_E", "Ground D1_N"]
STREET_STONE = ["Ground H1_E", "Ground H1_N"]
SCRUB = ["Flora A5_W", "Flora A1_E", "Flora A6_S"]
TREES = ["Tree A1_S", "Tree A3_S", "Tree D1_E", "Tree D3_N", "Tree A2_W",
         "Tree A4_N", "Tree B1_E", "Tree B1_S", "Tree E4_N", "Tree E4_E"]

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# Building plots: which stamp, where its lower-left corner sits, and what the place is.
# Buildings stand back from the street edge so the cobbles stay walkable in front of every door.
# The building kit, chosen for variety of material rather than convenience: stone and red tile for the
# hall, half-timber and plaster for the inn, thatch for the cottages, slate for the barn, and one house
# left roofless. A village built from one roof type reads as a tile sprayed across a field.
PLOTS = [
    (5,  (CENTRE + 5,  CENTRE + 5),  "hall"),      # stone compound, red tile, gated — the Warden's hall
    (6,  (CENTRE - 13, CENTRE + 3),  "inn"),       # half-timbered, two storeys, red tile
    (10, (CENTRE - 16, CENTRE - 10), "workshop"),  # open timber-framed bay
    (14, (CENTRE + 5,  CENTRE - 9),  "barn"),      # slate and thatch
    (8,  (CENTRE - 13, CENTRE - 17), "ruin"),      # the house nobody rebuilt
    (25, (CENTRE + 3,  CENTRE - 15), "cottage"),
    (28, (CENTRE + 12, CENTRE - 9),  "cottage"),
    (31, (CENTRE - 10, CENTRE - 9),  "cottage"),
    (21, (CENTRE + 12, CENTRE + 12), "cottage"),
    (26, (CENTRE - 10, CENTRE + 12), "cottage"),
]


class Town:
    """The map under construction: one dictionary of cells per tile layer."""

    def __init__(self, seed: int = 20260916):
        self.layers: dict[str, dict[tuple[int, int], str]] = {}
        self.blocked: set[tuple[int, int]] = set()
        self.building_cells: dict[tuple[int, int], str] = {}
        self.random = random.Random(seed)

    def put(self, layer: str, cell: tuple[int, int], tile: str) -> None:
        if not (0 <= cell[0] < SIZE and 0 <= cell[1] < SIZE):
            return
        self.layers.setdefault(layer, {})[cell] = tile

    def block(self, cell: tuple[int, int]) -> None:
        self.blocked.add(cell)

    def pick(self, options: list[str]) -> str:
        return options[self.random.randrange(len(options))]


def is_square(x: int, y: int) -> bool:
    return abs(x - CENTRE) <= SQUARE and abs(y - CENTRE) <= SQUARE


def is_forecourt(x: int, y: int) -> bool:
    """The paved apron around the square, so the streets meet it without leaving bare corner pockets."""
    return max(abs(x - CENTRE), abs(y - CENTRE)) <= SQUARE + 1


def is_street(x: int, y: int) -> bool:
    """Two streets crossing at the square and running out to the valley."""
    return abs(x - CENTRE) <= STREET or abs(y - CENTRE) <= STREET


def is_village(x: int, y: int) -> bool:
    return abs(x - CENTRE) <= VILLAGE and abs(y - CENTRE) <= VILLAGE


def lay_ground(town: Town) -> None:
    """Paves the settlement in whole surfaces.

    Scattering tile variants cell by cell is what makes a map look like spilled assets: the eye reads the
    speckle, not the place. Each surface here is laid solid, and the few variants are kept rare enough to
    look like wear rather than noise.
    """
    for x in range(SIZE):
        for y in range(SIZE):
            town.put("Ground", (x, y), DIRT)
            if is_square(x, y):
                # The square is laid, not scattered: cobbles framed by a border of street stone, with the
                # two diagonals picked out, so it reads as one paved plaza from any side.
                on_frame = max(abs(x - CENTRE), abs(y - CENTRE)) == SQUARE
                on_diagonal = abs(x - CENTRE) == abs(y - CENTRE)
                town.put("Ground 2", (x, y), STREET_STONE[0] if on_frame else COBBLE[1] if on_diagonal else COBBLE[0])
            elif is_forecourt(x, y) or (is_street(x, y) and is_village(x, y)):
                town.put("Ground 2", (x, y), STREET_STONE[0])
            elif not is_village(x, y):
                town.put("Ground 2", (x, y), GRASS[0])

    # Yards are green. Bare earth is not a texture to sprinkle about — it is where feet have worn the
    # grass away, so it is painted later, as an apron around each building and along the paving.
    for x in range(SIZE):
        for y in range(SIZE):
            if is_village(x, y) and not is_street(x, y) and not is_forecourt(x, y):
                town.put("Ground 2", (x, y), GRASS[0])

    # A thin scatter of scrub, only out where the settled ground gives way.
    for x in range(SIZE):
        for y in range(SIZE):
            if not is_village(x, y) and town.random.random() < 0.08:
                town.put("Ground 3", (x, y), town.pick(SCRUB))


def ring_with_forest(town: Town) -> None:
    """Closes the valley in. The forest is the wall here, which is the whole point of the oathfire."""
    for x in range(SIZE):
        for y in range(SIZE):
            if is_village(x, y):
                continue
            edge = max(abs(x - CENTRE), abs(y - CENTRE))
            density = min(0.9, 0.25 + (edge - VILLAGE) * 0.12)
            if town.random.random() < density:
                town.put("Walls", (x, y), town.pick(TREES))
                town.block((x, y))


def open_the_road(town: Town) -> None:
    """Carries the south-east street out through the trees, so the valley has a way in and out."""
    for step in range(VILLAGE - 1, SIZE):
        for offset in range(-STREET, STREET + 1):
            for cell in ((CENTRE + step, CENTRE + offset), (CENTRE + offset, CENTRE - step)):
                if not (0 <= cell[0] < SIZE and 0 <= cell[1] < SIZE):
                    continue
                town.layers.get("Walls", {}).pop(cell, None)
                town.blocked.discard(cell)
                town.put("Ground 2", cell, STREET_STONE[0])


def place_buildings(town: Town) -> list[dict]:
    placed, yards = [], []
    for index, (stamp_index, (ox, oy), kind) in enumerate(PLOTS):
        stamp = STAMPS[stamp_index]
        for piece in stamp["pieces"]:
            cell = (piece["x"] + ox, piece["y"] + oy)
            town.put(piece["layer"], cell, piece["tile"])
            # Only the footprint blocks movement; roofs overhang and must stay walkable beneath.
            if piece["layer"] in ("Walls", "WallDetail1", "WallDetail2"):
                town.block(cell)
                town.building_cells[cell] = kind
        # Doorways in the pack are empty arches; hang a door in each so a house reads as a house.
        for x, y, tile in doors_for(stamp_index, stamp["pieces"]):
            cell = (x + ox, y + oy)
            layer = next(name for name in ("Objects", "WallDetail1", "WallDetail2")
                         if cell not in town.layers.get(name, {}))
            town.put(layer, cell, tile)
            town.block(cell)
        yards.append((ox, oy, ox + stamp["width"], oy + stamp["height"]))
        placed.append({"kind": kind, "x": ox + stamp["width"] // 2, "y": oy + stamp["height"] // 2})

    # No bare aprons. A stamp's bounding box is wider than the building inside it, so clearing the
    # ground under it leaves brown rectangles poking out from behind the walls — the exact patchwork look
    # this layout exists to avoid. Grass runs up to the walls instead.
    _ = yards
    return placed


def close_paving(town: Town) -> None:
    """Paves over any bare cell that paving already surrounds.

    Where a street meets the square, the rectangles miss a corner cell or two. Left bare, each one reads
    as a pothole in the road rather than as ground.
    """
    surface = town.layers.setdefault("Ground 2", {})
    paved = {cell for cell, tile in surface.items() if tile in COBBLE + STREET_STONE}
    for _ in range(2):
        for x in range(SIZE):
            for y in range(SIZE):
                if (x, y) in surface or (x, y) in town.blocked:
                    continue
                neighbours = sum(1 for d in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (x + d[0], y + d[1]) in paved)
                if neighbours >= 2:
                    surface[(x, y)] = STREET_STONE[0]
                    paved.add((x, y))


def mirrored(dx: int, dy: int) -> list[tuple[int, int]]:
    """The same offset from the square in all four directions, so the village reads as planned."""
    cells = {(CENTRE + sx * dx, CENTRE + sy * dy) for sx in (1, -1) for sy in (1, -1)}
    cells |= {(CENTRE + sy * dy, CENTRE + sx * dx) for sx in (1, -1) for sy in (1, -1)}
    return sorted(cells)


def dress_symmetrically(town: Town) -> None:
    """Places the village's ornament in matching sets rather than at random.

    Scattered barrels and stones read as clutter someone forgot to tidy. Here every piece has its twin on the
    other side of the street and the other side of the square: stone braziers at the four corners of the
    plaza, banners at every street mouth, hedges lining the streets between the lamps.
    """
    def place(cell, tile, solid=False):
        if cell in town.blocked or cell in town.building_cells or not is_village(*cell):
            return False
        if town.layers.get("Objects", {}).get(cell):
            return False
        town.put("Objects", cell, tile)
        if solid:
            town.block(cell)
        return True

    # A stone brazier on each corner of the forecourt: the square is lit from four sides at night.
    for cell in mirrored(SQUARE + 1, SQUARE + 1):
        place(cell, "Misc C8_E", solid=True)
    # A pair of banners at the mouth of each street, one either side.
    for cell in mirrored(SQUARE + 2, STREET + 1):
        place(cell, "Misc D1_E")
    # Hedges down both sides of every street, between the lamps, in step.
    for step in range(SQUARE + 5, VILLAGE - 1, 5):
        for cell in mirrored(step, STREET + 1):
            place(cell, "Flora B5_E")


def check_plots(town: Town) -> None:
    """Complains if a building covers the paving. Easier to read here than to spot in a screenshot."""
    paved = {cell: kind for cell, kind in town.building_cells.items()
             if is_street(*cell) or is_forecourt(*cell)}
    if paved:
        from collections import Counter
        who = Counter(paved.values())
        print(f"  WARNING: {len(paved)} building cells sit on the paving: {dict(who)}")
    else:
        print("  plots clear of the paving")


def dress_streets(town: Town) -> None:
    """The things that say people live here: lamps down the street, the well, stalls on the square."""
    for step in range(-VILLAGE + 3, VILLAGE - 2, 5):
        for cell in ((CENTRE + step, CENTRE + STREET + 1), (CENTRE + step, CENTRE - STREET - 1),
                     (CENTRE + STREET + 1, CENTRE + step), (CENTRE - STREET - 1, CENTRE + step)):
            if cell in town.blocked or is_square(*cell):
                continue
            # A torch is a pole: people walk past it, so it never blocks.
            town.put("Objects", cell, "Torch2")

    # A market is stalls with goods on them, a well people queue at, carts being unloaded and somewhere
    # to sit — not a row of empty canopies. Every prop here was identified off a labelled contact sheet of
    # the pack's own props, so each one is the thing it is meant to be.
    square_props = [
        # North side: the trading row, backs to the hall, goods facing the square.
        ((CENTRE - 2, CENTRE + 3), "Misc B17_E"),   # red canopy, wares on shelves
        ((CENTRE,     CENTRE + 3), "Misc B18_E"),   # tan canopy stall
        ((CENTRE + 2, CENTRE + 3), "Misc B19_E"),   # green canopy, cloth
        ((CENTRE - 3, CENTRE + 4), "Misc B3_E"),    # sacks stacked behind the stalls
        ((CENTRE + 3, CENTRE + 4), "Misc B2_E"),
        # East side: the clerk's table and the butcher.
        ((CENTRE + 3, CENTRE + 1), "Misc B38_E"),   # white cloth, ledgers
        ((CENTRE + 3, CENTRE - 1), "Misc B21_E"),   # butcher's frame
        ((CENTRE + 4, CENTRE - 2), "Misc B6_E"),    # cart loaded with pottery
        # South side: the well and somewhere to sit.
        ((CENTRE - 1, CENTRE - 3), "Misc B42_E"),   # the village well
        ((CENTRE + 1, CENTRE - 3), "Misc B26_E"),   # bench
        ((CENTRE - 3, CENTRE - 2), "Misc B20_E"),   # barrel of apples
        # West side: the produce table and a handcart waiting to be unloaded.
        ((CENTRE - 3, CENTRE + 1), "Misc B37_E"),   # red cloth table
        ((CENTRE - 4, CENTRE),     "Misc B11_E"),   # handcart
        ((CENTRE - 4, CENTRE + 2), "Misc B9_E"),    # wheelbarrow
    ]
    # Only the big things stand in the way: stalls, tables, the loaded cart and the well. Sacks, crates,
    # benches, barrows and banners are stepped round without thinking, and blocking them made the square a
    # maze the hero and the night's dead both snagged in.
    solid = {"Misc B17_E", "Misc B18_E", "Misc B19_E", "Misc B38_E", "Misc B21_E", "Misc B6_E", "Misc B42_E", "Misc B37_E"}
    for cell, tile in square_props:
        town.put("Objects", cell, tile)
        if tile in solid:
            town.block(cell)

    dress_symmetrically(town)


def walkable_cells(town: Town, interiors: set[tuple[int, int]]) -> set[tuple[int, int]]:
    """Open village ground outdoors. The example town's own collider tiles count as blocked, and house
    floors are left out: an anchor placed there put villagers inside their houses, drawn on top of the roof."""
    ground = set(town.layers.get("Ground", {}))
    colliders = set().union(*(set(cells) for name, cells in town.layers.items() if name.startswith("Collider")))
    return {cell for cell in ground
            if cell not in town.blocked and cell not in colliders and cell not in interiors and is_village(*cell)}


def anchors(town: Town, buildings: list[dict], walkable: set[tuple[int, int]], houses: list[dict]) -> dict:
    """Puts the gameplay objects where a village would put them: on the square, along its edges."""
    # Only ground connected to the square. Two woodpiles and a quarry once stood behind the forest wall,
    # walled off from the village, and the wood-gathering quest could not be finished.
    walkable = walkable & reachable(walkable, (CENTRE, CENTRE))
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    taken: list[tuple[int, int]] = []
    roofs = roof_cells(town.layers)

    def open_all_round(cell: tuple[int, int]) -> bool:
        return all((cell[0] + dx, cell[1] + dy) not in town.blocked for dx in (-1, 0, 1) for dy in (-1, 0, 1))

    def free_near(target: tuple[int, int], apart: float = 3.0, open_ground: bool = False) -> dict:
        for cell in sorted(walkable, key=lambda c: (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2):
            if any(((cell[0] - t[0]) ** 2 + (cell[1] - t[1]) ** 2) ** 0.5 < apart for t in taken):
                continue
            if open_ground and not open_all_round(cell):
                continue
            # Nobody stands where a roof in front of them would hide them.
            if hidden_by_roof(cell, roofs):
                continue
            taken.append(cell)
            return point(cell)
        return point(target)

    centre = (CENTRE, CENTRE)
    # Two steps out from the door, so talking to them and opening the door are not the same spot.
    doorsteps = [(2 * house["outside"]["x"] - house["door"]["x"], 2 * house["outside"]["y"] - house["door"]["y"])
                 for house in houses]
    doorsteps += [(b["x"], b["y"]) for b in buildings if b["kind"] == "cottage"]
    return {
        "hearth": point(centre),
        # Where the east street leaves the village: the road to the trade road.
        "roadOut": point((CENTRE + VILLAGE - 1, CENTRE)),
        # The hero starts with open ground on every side, never shoulder to shoulder with a wall.
        "player": free_near((CENTRE + 3, CENTRE - 3), open_ground=True),
        "board": free_near((CENTRE - 3, CENTRE + 3)),
        "brann": free_near((CENTRE + 3, CENTRE + 3)),
        "maren": free_near((CENTRE - 5, CENTRE - 5)),
        # Each resident waits on their own doorstep, where they can be seen and found.
        "villagers": [free_near(doorstep, apart=2.0) for doorstep in doorsteps[:6]],
        "plots": [free_near((CENTRE + dx, CENTRE + dy), apart=3.0)
                  for dx, dy in ((-9, 9), (9, 9), (-9, -9), (9, -9))],
        "timber": [free_near((CENTRE + dx, CENTRE + dy), apart=3.0)
                   for dx, dy in ((-14, 12), (13, -14), (14, 13))],
        "stone": [free_near((CENTRE + dx, CENTRE + dy), apart=3.0) for dx, dy in ((-14, -13), (12, 6))],
        # Where the north street leaves the village between the trees: the lane to the Windrest.
        "trail": point((CENTRE - 1, CENTRE - VILLAGE)),
        # Where Crane's journal lies: by the ruined record house, the building nobody rebuilt.
        "search": free_near(next(((b["x"], b["y"]) for b in buildings if b["kind"] == "ruin"), (CENTRE - 10, CENTRE - 13)),
                            apart=2.0),
        "arrivals": [free_near((CENTRE + dx, CENTRE + dy), apart=4.0)
                     for dx, dy in ((0, VILLAGE - 2), (VILLAGE - 2, 0), (-VILLAGE + 2, 0))],
    }


def main() -> None:
    town = Town()
    lay_ground(town)
    ring_with_forest(town)
    buildings = place_buildings(town)
    open_the_road(town)
    check_plots(town)
    close_paving(town)
    dress_streets(town)

    print(f"  dropped {drop_unknown_tiles(town.layers)} cells the example scene left as unresolved tiles")
    missing = check_tiles(town.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    houses = open_interiors(town.layers, town.blocked)
    print(f"  cleared {clear_phantom_colliders(town.layers, town.blocked)} colliders standing on open ground")
    interiors = {(c["x"], c["y"]) for house in houses for c in house["interior"]}
    walkable = walkable_cells(town, interiors)
    # Every blocked cell gets an invisible collider tile, the way the pack's own scene does it.
    for cell in town.blocked:
        town.put("Colliders", cell, "Shadow5_E")

    fenced = fence_void(town.layers, walkable)
    print(f"  fenced {fenced} void edges")

    payload = {
        "region": [0, SIZE, 0, SIZE],
        "plaza": [CENTRE - SQUARE, CENTRE - SQUARE],
        "anchors": anchors(town, buildings, walkable, houses),
        "houses": houses,
        "walkable": [[x, y] for x, y in sorted(walkable)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in town.layers.items()],
    }
    out = PROJECT / "Docs" / "rennfall_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Rennfall: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(buildings)} buildings, {len(houses)} enterable houses, {len(walkable)} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Colliders") for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.5).save(scratch / "rennfall_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
