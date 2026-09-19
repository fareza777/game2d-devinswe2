"""The Saltpans: the League's brine-works at the valley's edge, composed from scratch.

Not a crop of the example map — an open-air camp laid out cell by cell the way the village was:
a dirt yard, three dry pans under pale crust, a boiling row of cauldrons, canvas tents, drying
racks, and the wagon lane. Small on purpose: this is side-quest ground, a working camp, not a keep.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable  # noqa: E402

SIZE_X, SIZE_Y = 34, 26
DIRT = "Ground A1_E"
CRUST = "Ground H1_E"       # bleached salt crust
CRUST2 = "Ground H3_E"
PATH = "Ground D1_E"        # packed pale wagon lane

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The three dry pans the camp exists for: rectangles of crust, kept unwalkable — the League counts
# every footprint in a pan.
PANS = [(4, 8, 9, 11), (11, 8, 16, 11), (6, 4, 12, 7), (16, 4, 22, 7)]


class Camp:
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


def lay_ground(camp: Camp) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            camp.put("Ground", (x, y), DIRT)
    # The wagon lane, packed pale, in from the west edge and round the pans to the boiling row;
    # its east fork is the burners' road, the one the charcoal carts come down.
    for x in range(0, 34):
        camp.put("Ground 2", (x, 13), PATH)
        camp.put("Ground 2", (x, 14), PATH)
    for y in range(14, 21):
        camp.put("Ground 2", (22, y), PATH)
        camp.put("Ground 2", (23, y), PATH)
    # Pan crust — three rectangles the salt dries in, rimmed with loose pebbles so they read as
    # built things and not pale accidents.
    for x0, y0, x1, y1 in PANS:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                camp.put("Ground 2", (x, y), CRUST2 if (x + y) % 4 == 0 else CRUST)
                camp.block((x, y))  # nobody walks a pan; the crust carries the count
        # a single footing of plank edging on the lane side, where the pans are worked
        for x in range(x0, x1 + 1):
            camp.put("Objects", (x, y1 + 1), "Misc B58_E",)
            camp.block((x, y1 + 1))


def dress_camp(camp: Camp) -> None:
    p = camp.prop
    # The boiling row at the lane's end: three cauldrons on the fire line, the woodpile beside them,
    # and the covered store-rack the dry salt sleeps under.
    p((24, 19), "Misc C1_E")
    p((26, 19), "Misc C1_E")
    p((28, 19), "Misc C1_E")
    p((30, 19), "Misc B58_E")          # cordwood feeding the fires
    p((31, 20), "Misc B59_E")
    p((24, 17), "Misc C12_E")          # covered rack — the night's salt goes here
    p((26, 17), "Misc C13_E")
    p((28, 17), "Misc B54_E")          # crates of wrapped salt
    p((30, 17), "Misc B45_N")          # sacks for the wagon
    # The factor's pitch: one red tent apart from the workers, his table out front.
    p((23, 23), "Misc B46_N")
    p((24, 21), "Misc B40_E")
    # The gleaners' corner: two canvas tents past the pans, off the lane, where the League tolerates them.
    p((27, 6), "Misc B43_E")
    p((30, 7), "Misc B43_S")
    p((28, 5), "Misc B3_N", solid=False)
    p((26, 7), "Misc C8_S", solid=False)
    p((29, 5), "Misc B45_N")
    # Drying rack mid-lane and the yard well.
    p((19, 12), "Misc B55_E")
    p((20, 15), "Misc B42_E")
    # Salt heaps where the pans are worked and spilled spoil where they leak.
    for cell, tile in {(3, 7): "Misc E7_E", (23, 8): "Misc E8_N", (5, 12): "Misc E6_N",
                       (15, 12): "Misc E7_W", (12, 3): "Misc E8_E", (24, 10): "Misc E6_N"}.items():
        p(cell, tile)
    # Tally posts and torch baskets along the lane so the place reads as run, not abandoned.
    p((2, 12), "Misc B52_E", solid=False)
    p((22, 12), "Misc B53_E", solid=False)
    p((31, 12), "Misc B52_E", solid=False)   # the marker where the burners' road forks east
    p((7, 15), "Torch2", solid=False)
    p((21, 15), "Torch2", solid=False)
    p((28, 14), "Torch2", solid=False)
    # The west and north edges: scrub and a few wind-bent trees — the flat's edge, not a forest.
    for cell, tile in {(1, 4): "Tree E4_S", (3, 20): "Tree B4_W", (31, 23): "Tree E4_S",
                       (1, 10): "Tree B4_W", (32, 3): "Tree B4_W"}.items():
        p(cell, tile)
    for cell in ((0, 6), (2, 8), (31, 11), (32, 16), (5, 23), (12, 22), (18, 24), (25, 2)):
        camp.put("Objects", cell, "Flora B3_N")


def anchors(camp: Camp) -> dict:
    ground = set(camp.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in camp.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 2) ** 2 + (c[1] - 13) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[saltpans] {len(reach)} of {len(walkable)} cells reachable from the lane")
    return {
        "player": point(near((2, 13))),
        "roadOut": point(near((0, 13))),
        "trail": point(near((33, 13))),
        "hearth": point(near((18, 14))),
        # villagers: Factor Hask by the store-rack, Pan-keeper Collum between the pans, Old Mirren
        # at the yard fire, Tess by the gleaner tents, Watch-lene on the lane.
        "villagers": [point(near((25, 18))), point(near((10, 12))), point(near((16, 15))),
                      point(near((26, 8))), point(near((20, 13)))],
        # arrivals: the salt-thieves' rush across the pan yard; the yard's middle; the boiling apron.
        "arrivals": [point(near((10, 13))), point(near((18, 16))), point(near((25, 16)))],
        # timber (search spots): tally-sticks by the pans, the store key by the boiling row, four spills.
        "timber": [point(near((6, 12))), point(near((13, 7))), point(near((17, 12))), point(near((24, 18))),
                   point(near((5, 9))), point(near((14, 12))), point(near((12, 5))), point(near((23, 10)))],
        # stone (porters' wander homes): the yard between lane and boiling row.
        "stone": [point(near((21, 14))), point(near((24, 15)))],
        "plots": [],
    }


def main() -> None:
    camp = Camp()
    lay_ground(camp)
    dress_camp(camp)

    print(f"  dropped {drop_unknown_tiles(camp.layers)} cells the pack does not have")
    missing = check_tiles(camp.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in camp.blocked:
        camp.put("Colliders", cell, "Shadow5_E")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [16, 14],
        "anchors": anchors(camp),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(camp.layers.get("Ground", {})) - camp.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in camp.layers.items()],
    }
    out = PROJECT / "Docs" / "saltpans_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Saltpans: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if layer["name"] != "Colliders" for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "saltpans_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
