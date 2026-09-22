"""The Drover's Rest: a wayside camp off the east of the toll road, composed from scratch.

Not a crop of the example map — a drovers' ring laid cell by cell: open meadow, a fire ring
at its heart, a wagon row along the north, bedrolls and tent rows east, and a fenced corral
where the pack animals overnight. Side-quest ground: small, open, and lived-in.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable, fence_void  # noqa: E402

SIZE_X, SIZE_Y = 28, 20
MEADOW = "Ground A2_N"      # open grass
LANE = "Ground D1_E"        # the drovers' track in from the west
DIRT = "Ground A1_E"        # worn ground round the fire

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The corral the jackals have been working: fenced pen in the south-east.
CORRAL = (19, 11, 25, 16)


class Rest:
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


def lay_ground(rest: Rest) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            rest.put("Ground", (x, y), MEADOW)
    # The drovers' track: pale lane in from the west edge, forking to the fire.
    for x in range(0, 14):
        rest.put("Ground 2", (x, 9), LANE)
        rest.put("Ground 2", (x, 10), LANE)
    for y in range(9, 14):
        rest.put("Ground 2", (13, y), LANE)
        rest.put("Ground 2", (14, y), LANE)
    # Worn dirt apron round the fire ring.
    for x in range(12, 17):
        for y in range(12, 15):
            rest.put("Ground 2", (x, y), DIRT)


def dress_rest(rest: Rest) -> None:
    p = rest.prop
    # The fire ring and its seats — the camp's heart, where everything is told.
    p((14, 13), "Misc C1_E")
    p((12, 13), "Misc B26_N", solid=False)   # sitting stone
    p((16, 13), "Misc B26_N", solid=False)
    p((14, 14), "Misc E6_N", solid=False)    # ash
    # The wagon row along the north: three wagons and their loads.
    p((8, 3), "Misc B6_E")
    p((12, 3), "Misc B6_E")
    p((16, 3), "Misc B6_E")
    p((9, 2), "Misc B45_N")
    p((13, 2), "Misc A1_E")
    p((17, 2), "Misc B45_N")
    p((11, 2), "Misc E8_N", solid=False)     # spilled sacks
    # The tent row east of the lane: canvas tents and a red one for the head drover.
    p((20, 4), "Misc B43_E")
    p((23, 5), "Misc B43_S")
    p((24, 3), "Misc B46_N")
    p((21, 6), "Misc C8_S", solid=False)     # bedroll
    p((25, 6), "Misc B3_N", solid=False)
    # The corral: fence of crate-rails on three sides, gate open on the lane side.
    cx0, cy0, cx1, cy1 = CORRAL
    for x in range(cx0, cx1 + 1):
        p((x, cy0), "Misc B52_E")
        p((x, cy1), "Misc B52_E")
    for y in range(cy0, cy1 + 1):
        p((cx1, y), "Misc B53_E")
    # west side left open — the animals come in off the meadow
    p((22, 14), "Misc B45_N", solid=False)   # hay inside
    p((24, 12), "Misc C8_E", solid=False)    # pail
    p((19, 13), "Misc B26_N", solid=False)   # mounting stone at the open side
    # Cook side: spit and pot crates by the fire.
    p((16, 14), "Misc B54_E")
    p((17, 13), "Misc C2_N", solid=False)
    # Torch baskets marking the lane and the corral mouth.
    for cell in ((4, 8), (13, 11), (19, 10), (25, 9)):
        p(cell, "Torch2", solid=False)
    # Meadow dressing: scrub, a few trees at the rim, drovers' leavings.
    for cell, tile in {(1, 2): "Tree A2_S", (2, 16): "Tree A3_S", (26, 17): "Tree A2_S",
                       (26, 1): "Tree A2_S", (5, 18): "Tree A2_S"}.items():
        p(cell, tile)
    for cell, tile in {(3, 6): "Misc E9_N", (7, 15): "Misc E8_N", (10, 17): "Misc E10_N",
                       (18, 8): "Misc B45_N", (22, 8): "Misc E7_E", (6, 12): "Misc E11_N",
                       (26, 12): "Misc E8_N", (15, 17): "Misc B58_E", (9, 6): "Misc E6_N"}.items():
        p(cell, tile)
    p((5, 4), "Misc B40_E", solid=False)     # spread drying-cloth
    for cell in ((2, 8), (6, 1), (11, 6), (27, 7), (3, 14), (24, 17), (18, 17)):
        rest.put("Objects", cell, "Flora B3_N")


def anchors(rest: Rest) -> dict:
    ground = set(rest.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in rest.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 2) ** 2 + (c[1] - 9) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[drovers] {len(reach)} of {len(walkable)} cells reachable from the lane")
    return {
        "player": point(near((2, 9))),
        "roadOut": point(near((0, 9))),
        "hearth": point(near((14, 13))),          # the fire ring
        # villagers: Head Drover Ossel by the wagons, Cook Brigid at the fire, Harness-boy Pate
        # by the corral, Old Fen under the tents, Whip Nell on the lane.
        "villagers": [point(near((12, 4))), point(near((15, 14))), point(near((19, 12))),
                      point(near((22, 5))), point(near((8, 9)))],
        # arrivals: the jackal rush across the corral mouth, the lane end, the tent row.
        "arrivals": [point(near((19, 12))), point(near((12, 10))), point(near((22, 7)))],
        # timber (search spots): three dropped tack pieces among the wagons/corral, four supper
        # fixings scattered round the meadow.
        "timber": [point(near((10, 4))), point(near((18, 5))), point(near((23, 13))),
                   point(near((4, 12))), point(near((11, 8))), point(near((16, 16))),
                   point(near((25, 15)))],
        # stone: drovers' wander homes in the meadow.
        "stone": [point(near((10, 11))), point(near((17, 10)))],
        "plots": [],
    }


def main() -> None:
    rest = Rest()
    lay_ground(rest)
    dress_rest(rest)

    print(f"  dropped {drop_unknown_tiles(rest.layers)} cells the pack does not have")
    missing = check_tiles(rest.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in rest.blocked:
        rest.put("Colliders", cell, "Shadow5_E")

    fenced = fence_void(rest.layers, set(rest.layers.get("Ground", {})) - rest.blocked)
    print(f"  fenced {fenced} void edges")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [14, 12],
        "anchors": anchors(rest),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(rest.layers.get("Ground", {})) - rest.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in rest.layers.items()],
    }
    out = PROJECT / "Docs" / "drovers_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Drover's Rest: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Colliders") for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "drovers_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
