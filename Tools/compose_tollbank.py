"""The Tollbank: the League's ruined toll fort north of the valley road, composed from scratch.

Not a crop of the example map — a broken square of ramparts laid cell by cell: gravel court,
breached wall stubs, a plank tollhouse floor with its counter and strongboxes, the cutter's
bench where false dies were struck, a cage pen for the tollmen who asked questions, and the
fire ring the washing crew works by. This is Act III ground — the coin's last address.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable, fence_void  # noqa: E402

SIZE_X, SIZE_Y = 32, 22
DIRT = "Ground A1_E"
GRAVEL = "Ground G10_E"     # fort-yard gravel
FLOOR = "Ground E1_E"       # tollhouse planks
PATH = "Ground D1_E"        # the old toll road spur

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The rampart ring (wall stubs) with a south breach for the lane and a collapsed stretch north.
FORT = (6, 3, 28, 17)
BREACH_S = (15, 16)   # x, y range of the south gap in the wall line
BREACH_N = (18, 19)
# The tollhouse: plank floor and counter at the east end of the court.
HOUSE = (21, 5, 27, 9)


class Fort:
    def __init__(self) -> None:
        self.layers: dict[str, dict] = {}
        self.blocked: set[tuple[int, int]] = set()

    def put(self, layer: str, cell: tuple[int, int], tile: str) -> None:
        self.layers.setdefault(layer, {})[cell] = tile

    def block(self, cell: tuple[int, int]) -> None:
        self.blocked.add(cell)

    def prop(self, cell: tuple[int, int], tile: str, solid: bool = True) -> None:
        self.put("Objects", cell, tile)
        if solid:
            self.block(cell)


def lay_ground(fort: Fort) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            fort.put("Ground", (x, y), DIRT)
    # The old toll road spur: packed pale lane in from the south edge up to the breach.
    for y in range(17, SIZE_Y):
        fort.put("Ground 2", (15, y), PATH)
        fort.put("Ground 2", (16, y), PATH)
    # The court inside the walls is gravel.
    x0, y0, x1, y1 = FORT
    for x in range(x0 + 1, x1):
        for y in range(y0 + 1, y1):
            fort.put("Ground 2", (x, y), GRAVEL)
    # The tollhouse floor.
    hx0, hy0, hx1, hy1 = HOUSE
    for x in range(hx0, hx1 + 1):
        for y in range(hy0, hy1 + 1):
            fort.put("Ground 2", (x, y), FLOOR)
            fort.block((x, y))  # the floor is a raised deck the court works around
    # Entry step onto the deck: one plank footing the crew still uses.
    fort.put("Ground 2", (20, 7), FLOOR)
    fort.blocked.discard((20, 7))


def lay_ramparts(fort: Fort) -> None:
    x0, y0, x1, y1 = FORT
    # Wall stubs around the ring, gaps at both breaches and a few crumbled cells north-east.
    rubble = {(9, 3), (24, 3), (7, 10), (28, 12), (6, 14), (27, 17)}
    for x in range(x0, x1 + 1):
        for y in (y0, y1):
            if (x in BREACH_S and y == y1) or (x in BREACH_N and y == y0) or (x, y) in rubble:
                continue
            fort.prop((x, y), "Wall A10_E" if (x + y) % 2 else "Wall A11_E")
    for y in range(y0, y1 + 1):
        for x in (x0, x1):
            if (x, y) in rubble:
                continue
            fort.prop((x, y), "Wall A10_E" if (x + y) % 2 else "Wall A11_E")
    # Rubble heaps where the wall went down.
    for cell in rubble:
        fort.put("Objects", cell, "Misc E9_N")


def dress_fort(fort: Fort) -> None:
    p = fort.prop
    # The tollhouse deck: counter slab, the strongbox row, the ledger shelf.
    p((22, 5), "Misc C9_N", solid=False)     # counter slab (on the deck edge; the deck itself blocks)
    p((24, 5), "Chest A2_E", solid=False)
    p((25, 5), "Chest A2_E", solid=False)
    p((26, 5), "Chest A2_E", solid=False)
    p((27, 8), "Misc C12_E", solid=False)    # ledger shelf
    p((22, 9), "Misc B54_E", solid=False)    # coin crates under the deck lip
    # The cutter's bench mid-court: slab, pillar stubs, scaffold beam overhead.
    p((14, 9), "Misc C9_N")
    p((13, 8), "Misc C10_N")
    p((15, 8), "Misc C10_N")
    p((14, 7), "Misc B60_N", solid=False)
    p((13, 10), "Misc A2_E")                 # die crate
    p((15, 10), "Misc A1_E")
    # The cage pen west of the bench: crate walls, a left-open door — where tollmen who asked
    # questions waited their turn.
    for c in ((8, 8), (8, 9), (8, 10), (9, 7), (10, 7)):
        p(c, "Misc A1_E")
    p((10, 9), "Misc B26_N", solid=False)    # the pen's tally stone
    p((9, 9), "Misc B45_N", solid=False)     # bedding straw
    # The fire ring the washing crew works by, east of the breach.
    p((19, 13), "Misc C1_E")
    p((18, 14), "Misc B58_E")                # cordwood
    p((20, 14), "Misc E6_N", solid=False)    # ash
    # Red washing banners on the court corners, toll posts at the breach.
    p((7, 4), "Misc B43_N")
    p((27, 4), "Misc B43_E")
    p((15, 16), "Misc B52_E", solid=False)
    p((17, 16), "Misc B53_E", solid=False)
    # Torches along the court so the night shift reads.
    for cell in ((8, 5), (14, 12), (22, 12), (26, 16)):
        p(cell, "Torch2", solid=False)
    # Spoil and work-leavings around the court.
    for cell, tile in {(10, 5): "Misc E8_N", (11, 13): "Misc E7_E", (18, 6): "Misc E10_N",
                       (23, 13): "Misc E6_N", (25, 14): "Misc E7_W", (9, 13): "Misc E11_N",
                       (12, 6): "Misc B45_N", (16, 5): "Misc B40_E"}.items():
        p(cell, tile)
    # Outside the walls: dead scrub, a wrecked toll cart, wind-bent trees at the map edge.
    for cell, tile in {(2, 4): "Tree B4_W", (3, 18): "Tree E4_S", (29, 2): "Tree B4_W",
                       (30, 19): "Tree E4_S", (1, 10): "Tree B4_W"}.items():
        p(cell, tile)
    for cell, tile in {(3, 8): "Misc B6_E", (4, 15): "Misc E9_N", (29, 8): "Misc E8_N",
                       (24, 20): "Misc E10_N", (9, 20): "Misc E8_N", (20, 19): "Misc E9_N",
                       (4, 6): "Misc E11_N", (30, 12): "Misc B45_N", (11, 20): "Misc B58_E"}.items():
        p(cell, tile)
    for cell in ((1, 5), (4, 11), (29, 14), (31, 7), (7, 20), (25, 19)):
        fort.put("Objects", cell, "Flora B3_N")


def anchors(fort: Fort) -> dict:
    ground = set(fort.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in fort.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 15) ** 2 + (c[1] - 20) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[tollbank] {len(reach)} of {len(walkable)} cells reachable from the spur")
    return {
        "player": point(near((15, 20))),
        "roadOut": point(near((15, 21))),
        "roadOutN": point(near((18, 4))),      # the north breach the coin walked out of
        "hearth": point(near((19, 13))),          # the washing crew's fire
        # villagers: the freed tollkeepers who come back once the yard is clear —
        # Widow Annet by the pen, Tollman Joss at the counter, Boy Pike at the breach,
        # Clerk Effa by the shelf.
        "villagers": [point(near((9, 10))), point(near((21, 8))), point(near((16, 15))),
                      point(near((24, 10)))],
        # arrivals: the yard mouth fight, the court centre, the tollhouse dais for the boss.
        "arrivals": [point(near((15, 16))), point(near((13, 12))), point(near((21, 10)))],
        # timber (search spots): three toll tallies, the banner pole, four strongboxes.
        "timber": [point(near((11, 5))), point(near((17, 8))), point(near((23, 12))),
                   point(near((12, 14))),
                   point(near((20, 9))), point(near((26, 10))), point(near((18, 12))),
                   point(near((10, 12)))],
        # stone: porters' wander homes in the court.
        "stone": [point(near((12, 11))), point(near((19, 9)))],
        "plots": [],
    }


def main() -> None:
    fort = Fort()
    lay_ground(fort)
    lay_ramparts(fort)
    dress_fort(fort)

    print(f"  dropped {drop_unknown_tiles(fort.layers)} cells the pack does not have")
    missing = check_tiles(fort.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in fort.blocked:
        fort.put("Colliders", cell, "Shadow5_E")

    fenced = fence_void(fort.layers, set(fort.layers.get("Ground", {})) - fort.blocked)
    print(f"  fenced {fenced} void edges")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [15, 13],
        "anchors": anchors(fort),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(fort.layers.get("Ground", {})) - fort.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in fort.layers.items()],
    }
    out = PROJECT / "Docs" / "tollbank_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Tollbank: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Colliders") for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "tollbank_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
