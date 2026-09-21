"""The Chalkpit: an old chalk quarry reopened under a dead writ, composed from scratch.

A pale bite taken out of the valley's north rise: the pit floor is bare cracked chalk under the
cut faces, a plank derrick platform overhangs the deep end, and the foreman's camp sits on the
rim where the cart lane crests. Squatters — stragglers selling cut stone to a buyer no one names —
hold the benches. Smaller than the pans: a wound in the ground, not a court.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable  # noqa: E402

SIZE_X, SIZE_Y = 30, 22
DIRT = "Ground A1_E"
CHALK = "Ground H4_E"      # the cut face: flat pale stone, worked flat
YARD = "Ground D1_E"       # grey cobble: the lane and the worked rim
GRIT = "Ground G10_E"      # gravel seams where the digging is freshest
PLANK = "Ground E1_E"      # the derrick platform

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The excavation itself: a rectangle bitten into the rise, deeper at the north end.
PIT = (4, 2, 24, 10)
# The lane in: a ramp from the south edge over the rim gap at x 14..16.
LANE = [(13, 12, 15, 21)]


class Pit:
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


def lay_ground(pit: Pit) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            pit.put("Ground", (x, y), DIRT)
    # The cart lane in, packed grey by the wagons' wheels.
    for x0, y0, x1, y1 in LANE:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                pit.put("Ground 2", (x, y), YARD)
    # The rim walk round the pit's south and east lip — cobble where the counting happens.
    for x in range(4, 25):
        pit.put("Ground 2", (x, 12), YARD)
    for y in range(11, 18):
        pit.put("Ground 2", (24, y), YARD)
        pit.put("Ground 2", (25, y), YARD)
    # The pit floor: bare chalk, crazed white where the picks have been.
    for x in range(PIT[0], PIT[2] + 1):
        for y in range(PIT[1], PIT[3] + 1):
            pit.put("Ground 2", (x, y), CHALK if (x * 5 + y * 3) % 13 else GRIT)
    # The fresh bench: a gravel band along the pit's east face where the digging is live.
    for x in range(17, 23):
        for y in range(6, 9):
            if (x + y) % 3:
                pit.put("Ground 3", (x, y), GRIT)
    # The derrick platform: planks over the pit's deep end, where the hoist drops.
    for x in range(21, 24):
        for y in range(2, 4):
            pit.put("Ground 3", (x, y), PLANK)


def dress_pit(pit: Pit) -> None:
    p = pit.prop
    # The cut faces: rock stumps along the pit's rim so the excavation reads as a wound in the
    # ground. The lane gap at x 14..16 is where the carts crest; the north face stands sheer.
    for x in range(4, 25):
        if x not in (14, 15, 16):
            p((x, 11), "Wall A11_E")
    for x in range(4, 25):
        if x not in (21, 22, 23):          # the derrick platform holds the north face open
            p((x, 1), "Wall A10_E")
    for y in range(2, 11):
        p((3, y), "Wall A11_E")
    for y in range(2, 11):
        if y not in (11, 12):              # east face closed except the camp walk
            p((25, y), "Wall A10_E")

    # The derrick: scaffold frame and its ladder at the platform's lip, hoist over the deep end.
    p((23, 1), "Misc B60_N")
    pit.put("Objects", (24, 3), "Misc E9_N")
    # Cut work: slab tables and pillar stubs mid-dressing, spoil heaps where the waste went.
    for cell, tile in {(6, 5): "Misc C9_N", (10, 7): "Misc C9_N", (15, 8): "Misc C9_N",
                       (7, 8): "Misc C10_N", (18, 5): "Misc C10_N",
                       (5, 9): "BrokenStone1", (12, 9): "BrokenStone small1", (19, 7): "BrokenStone1",
                       (9, 3): "BrokenStone small1", (20, 9): "BrokenStone1"}.items():
        p(cell, tile)
    # A block stack ready for the hoist: cut stone waiting by the platform.
    p((20, 3), "Misc C10_N")
    p((19, 4), "Misc C9_N")

    # The squatters' squat: a torn tent and a cold fire where the claim-jumpers sleep in the pit.
    p((8, 3), "Misc B43_N")
    pit.put("Objects", (9, 4), "Misc C1_E")
    p((7, 4), "Misc B45_N")

    # The foreman's camp on the east rim: his tent, the count's fire, the cart that waits loaded,
    # barrels of pitch and lamp oil against the night shifts.
    p((26, 12), "Misc B43_S")
    pit.put("Objects", (25, 14), "Misc C1_E")
    p((27, 15), "Misc B6_E")
    p((28, 12), "Barrel A1")
    p((28, 14), "Barrel A1")
    p((13, 12), "Misc B52_E", solid=False)
    pit.put("Objects", (26, 14), "Misc B45_N")

    # Torches burn at the rim gap and the camp — the pit is worked past dark when the books want it.
    for cell in ((14, 12), (17, 12), (22, 11), (24, 16)):
        pit.put("Objects", cell, "Torch2")

    # The rise around the dig: scrub trees on the outer slope, nothing green inside the chalk.
    for cell, tile in {(0, 3): "Tree A2_S", (1, 0): "Tree A3_S", (7, 0): "Tree A1_S",
                       (12, 0): "Tree A2_S", (17, 0): "Tree A3_S", (27, 0): "Tree A2_S",
                       (29, 5): "Tree A1_S", (29, 10): "Tree A2_S", (29, 17): "Tree A3_S",
                       (26, 20): "Tree A1_S", (20, 21): "Tree A2_S", (9, 21): "Tree A3_S",
                       (3, 18): "Tree A2_S", (0, 13): "Tree A1_S", (0, 8): "Tree A3_S"}.items():
        pit.prop(cell, tile)
    # Sparse flora on the rim — the chalk kills what tries.
    for cell in ((11, 12), (18, 12), (5, 12), (1, 15), (27, 9)):
        pit.put("Objects", cell, "Flora B3_N")


def anchors(pit: Pit) -> dict:
    ground = set(pit.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in pit.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 14) ** 2 + (c[1] - 19) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[chalkpit] {len(reach)} of {len(walkable)} cells reachable from the lane")
    return {
        "player": point(near((14, 18))),
        "roadOut": point(near((14, 21))),
        "hearth": point(near((25, 14))),
        # villagers: foreman Brack at the camp, tally-clerk Ines on the rim, Old Simm by the cart,
        # drudge Ket down in the cut, yard-warden Rill at the platform's foot.
        "villagers": [point(near((24, 13))), point(near((12, 14))), point(near((26, 16))),
                      point(near((7, 6))), point(near((20, 10)))],
        # arrivals: the squatters' rush off the benches; the pit's middle; the deep end.
        "arrivals": [point(near((10, 6))), point(near((15, 8))), point(near((21, 8)))],
        # timber (search spots): three tally-sticks dropped on the benches, the foreman's seal on
        # the derrick platform, four lime sacks split around the pit floor.
        "timber": [point(near((10, 5))), point(near((18, 9))), point(near((6, 9))), point(near((22, 3))),
                   point(near((11, 10))), point(near((5, 6))), point(near((16, 10))), point(near((8, 8)))],
        # stone (porters' wander homes): the cart lane where the loads wait.
        "stone": [point(near((24, 16))), point(near((26, 17)))],
        "plots": [],
    }


def main() -> None:
    pit = Pit()
    lay_ground(pit)
    dress_pit(pit)

    print(f"  dropped {drop_unknown_tiles(pit.layers)} cells the pack does not have")
    missing = check_tiles(pit.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in pit.blocked:
        pit.put("Colliders", cell, "Shadow5_E")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [14, 14],
        "anchors": anchors(pit),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(pit.layers.get("Ground", {})) - pit.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in pit.layers.items()],
    }
    out = PROJECT / "Docs" / "chalkpit_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Chalkpit: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if layer["name"] != "Colliders" for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "chalkpit_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
