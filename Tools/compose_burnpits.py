"""The Burnpits: the charcoal-burners' camp that feeds the pans' fires, composed from scratch.

A clearing in the woods at the valley's east edge: scorched dark ground, cordwood ricks stacked
along the treeline, earth-clamped burn mounds smoking low, and the burners' tents where the soot
ends. Smaller than the pans — a camp that smells of smoke, not a court that counts it.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable  # noqa: E402

SIZE_X, SIZE_Y = 32, 24
DIRT = "Ground A1_E"
SCORCHED = "Ground G2_E"     # burned-dark earth
SCORCHED2 = "Ground C1_E"    # grey slag seams inside the burn ground
PATH = "Ground D1_E"

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The burn ground: an irregular field of scorched earth where the clamps stand — the dark patch
# that names the place.
BURN = [(5, 6, 13, 14)]
CLAMPS = [(6, 7), (9, 7), (12, 7), (6, 11), (9, 11), (12, 11)]


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
    # The dragway in from the west edge, packed pale by the cart ruts, round the burn ground to the
    # stackyard where the wagon waits on charcoal.
    for x in range(0, 24):
        camp.put("Ground 2", (x, 16), PATH)
        camp.put("Ground 2", (x, 17), PATH)
    for y in range(14, 19):
        camp.put("Ground 2", (18, y), PATH)
        camp.put("Ground 2", (19, y), PATH)
    # The burn ground — scorched earth underfoot, walkable; burning clamps are what block, not the ash.
    for x0, y0, x1, y1 in BURN:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                camp.put("Ground 2", (x, y), SCORCHED2 if (x * 3 + y) % 5 == 0 else (SCORCHED if (x * 7 + y * 5) % 9 < 5 else DIRT))


def dress_camp(camp: Camp) -> None:
    p = camp.prop
    # The clamps themselves: earthed-over coal mounds that smoulder for a week, every third one
    # still burning at its side.
    for i, (x, y) in enumerate(CLAMPS):
        camp.prop((x, y), "Misc C3_N" if i % 2 else "Misc C3_E")
        if i % 3 == 0:
            camp.prop((x + 1, y), "Misc C1_E", solid=False)
    # The stackyard: cordwood ricks in tight rows along the north treeline — the camp's pantry and
    # its product at once.
    for x in range(3, 17):
        p((x, 2), "Misc B58_E")
        p((x, 3), "Misc B59_E")
    p((17, 3), "Misc B58_E")
    # Where the ricks are worked: a sawbuck and the splitter's block.
    p((10, 6), "Misc B40_E")
    p((14, 6), "Misc B26_N")
    # The burners' corner east of the burn ground: two canvas tents, their fire, their kettle-shelf.
    p((22, 8), "Misc B43_E")
    p((25, 9), "Misc B43_S")
    p((23, 6), "Misc B3_N", solid=False)
    p((21, 9), "Misc C1_E", solid=False)
    p((24, 7), "Misc B45_N")
    # The tally post by the dragway: every cart out is counted on it.
    p((2, 15), "Misc B52_E", solid=False)
    p((19, 13), "Misc B53_E", solid=False)
    # Torch baskets at the dragway corners — the camp burns its own fuel to mark the way.
    p((6, 17), "Torch2", solid=False)
    p((16, 18), "Torch2", solid=False)
    p((23, 14), "Torch2", solid=False)
    # The wagon that never quite leaves: a cart by the stackyard end.
    p((26, 16), "Misc B6_E")
    # The wood closes round — a burned clearing: live trees lean in at the edge, dead ones stand
    # grey where the burning took them.
    for cell, tile in {(0, 4): "Tree A2_S", (2, 1): "Tree A3_S", (8, 0): "Tree A2_S",
                       (15, 0): "Tree A1_S", (21, 0): "Tree A2_S", (27, 0): "Tree A3_S",
                       (31, 4): "Tree A2_S", (30, 10): "Tree A1_S", (30, 18): "Tree A2_S",
                       (1, 20): "Tree A3_S", (9, 21): "Tree A2_S", (17, 22): "Tree A1_S",
                       (24, 21): "Tree A2_S", (29, 22): "Tree A3_S"}.items():
        camp.prop(cell, tile)
    for cell in ((5, 9), (11, 10), (8, 14)):
        camp.prop(cell, "Tree D1_S")
    for cell in ((0, 8), (4, 4), (16, 1), (29, 14), (26, 21), (11, 22), (20, 4), (3, 19)):
        camp.put("Objects", cell, "Flora B3_N")
    # Split logs and offcuts where the axe work happens.
    for cell, tile in {(8, 6): "Misc B58_E", (15, 8): "Misc B59_E", (24, 16): "Misc B58_E"}.items():
        p(cell, tile)


def anchors(camp: Camp) -> dict:
    ground = set(camp.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in camp.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 2) ** 2 + (c[1] - 16) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[burnpits] {len(reach)} of {len(walkable)} cells reachable from the dragway")
    return {
        "player": point(near((2, 16))),
        "roadOut": point(near((0, 16))),
        "hearth": point(near((21, 9))),
        # villagers: kiln-keeper Wren at the clamps, tally-foreman Pyke on the dragway, soot-boy Penn
        # by the tents, Old Clod at the camp fire, rack-warden Ferra in the stackyard.
        "villagers": [point(near((10, 9))), point(near((17, 16))), point(near((24, 8))),
                      point(near((22, 10))), point(near((14, 4)))],
        # arrivals: the poachers' rush out of the treeline; the yard's middle; the stackyard end.
        "arrivals": [point(near((8, 12))), point(near((16, 15))), point(near((26, 14)))],
        # timber (search spots): cordwood tallies in the stackyard, the keeper's rod by the clamps,
        # four dropped tools around the burn ground.
        "timber": [point(near((5, 4))), point(near((11, 4))), point(near((15, 4))), point(near((8, 10))),
                   point(near((4, 12))), point(near((13, 13))), point(near((11, 15))), point(near((6, 15)))],
        # stone (porters' wander homes): between dragway and stackyard.
        "stone": [point(near((16, 17))), point(near((20, 16)))],
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
        "plaza": [16, 15],
        "anchors": anchors(camp),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(camp.layers.get("Ground", {})) - camp.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in camp.layers.items()],
    }
    out = PROJECT / "Docs" / "burnpits_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Burnpits: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if layer["name"] != "Colliders" for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "burnpits_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
