"""The Stampworks: the coiners' yard the jacks' spoil climbs to, composed from scratch.

A walled night-yard up the far face of the chalk pit: dark stamped ground where a counting dais
overlooks rows of press benches, a crucible line that never goes cold, and a pens-corner where
the League's borrowed clerks keep the false books by torchlight. This is where chalk stops being
chalk and becomes someone else's money. The deepest location yet — the end of the pay-roll's trail.
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
YARD = "Ground D1_E"       # dark cobble: the stamped yard floor
GRIT = "Ground G10_E"      # trodden spoil where boots turn
CHALK = "Ground H4_E"      # pale spill around the press tables — the yard's lie showing
PLANK = "Ground E1_E"      # the counting dais

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The yard proper: the walled stamping floor, entered by the lane at the south.
YARD_RECT = (4, 3, 27, 16)
# The lane in: south, off the chalk spur.
LANE = [(13, 16, 15, 21)]
# The counting dais: the raised plank platform at the yard's north-east, over the weigh line.
DAIS = (22, 4, 26, 6)
# The clerks' pens: the south-east corner, railed off behind crates.
PENS = (20, 12, 26, 15)


class Yard:
    def __init__(self) -> None:
        self.layers: dict[str, dict] = {}
        self.blocked: set[tuple[int, int]] = set()

    def put(self, layer: str, cell: tuple[int, int], tile: str) -> None:
        self.layers.setdefault(layer, {})[cell] = tile

    def block(self, cell: tuple[int, int]) -> None:
        self.blocked.add(cell)

    def prop(self, tile: str, cell: tuple[int, int], layer: str = "Objects", solid: bool = True) -> None:
        self.put(layer, cell, tile)
        if solid:
            self.block(cell)


def lay_ground(yard: Yard) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            yard.put("Ground", (x, y), DIRT)
    # The lane in.
    for x0, y0, x1, y1 in LANE:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                yard.put("Ground 2", (x, y), YARD)
    # The stamped floor: dark cobble through the whole yard, trodden to spoil along the west run.
    for x in range(YARD_RECT[0], YARD_RECT[2] + 1):
        for y in range(YARD_RECT[1], YARD_RECT[3] + 1):
            yard.put("Ground 2", (x, y), YARD)
    for x in range(4, 10):
        for y in range(4, 16):
            if (x * 3 + y) % 4:
                yard.put("Ground 3", (x, y), GRIT)
    # Chalk spill: pale dust where the press benches stand — the lie showing through the floor.
    for x in range(10, 21):
        for y in range(7, 11):
            if (x + y * 2) % 5:
                yard.put("Ground 3", (x, y), CHALK)
    # The counting dais: planks over the yard's north-east corner.
    for x in range(DAIS[0], DAIS[2] + 1):
        for y in range(DAIS[1], DAIS[3] + 1):
            yard.put("Ground 3", (x, y), PLANK)


def dress_yard(yard: Yard) -> None:
    block, prop = yard.block, yard.prop

    # The yard wall: broken stubs with two gaps — the lane mouth at the south, and a spoil-side
    # gap in the west where the carts come down from the pit.
    for x in range(4, 28):
        prop("Wall A10_E", (x, 2))
        prop("Wall A11_E", (x, 16))
    for y in range(3, 16):
        prop("Wall A10_E", (3, y))
        prop("Wall A11_E", (27, y))
    # The lane mouth.
    for x in (13, 14, 15):
        yard.layers["Objects"].pop((x, 16), None)
        yard.blocked.discard((x, 16))
    # The west gap: spoil carts' way, blocked by two rubble piles so only a walker threads it.
    yard.layers["Objects"].pop((3, 8), None)
    yard.blocked.discard((3, 8))
    prop("BrokenStone1", (2, 7), layer="BrokenObjects")
    prop("BrokenStone1", (2, 9), layer="BrokenObjects")
    prop("BrokenStone small1", (2, 8), layer="BrokenObjects", solid=False)

    # The press line: three slab tables as stamping benches, pillar stubs as the uprights that
    # take the hammer frames, a scaffold beam over the middle bench.
    for i, x in enumerate((11, 14, 17)):
        prop("Misc C9_N", (x, 8))
        prop("Misc C10_N", (x - 1, 8))
        prop("Misc C10_N", (x + 1, 8))
    prop("Misc B60_N", (14, 7))
    prop("Misc E9_N", (15, 7), solid=False)

    # The crucible line: fires down the yard's east run with carts and pots beside.
    for y in (7, 9, 11):
        prop("Misc C1_E", (24, y))
        prop("Barrel A1", (25, y + 1))
        prop("Misc B6_E", (26, y), solid=False)
    prop("Misc B60_N", (25, 13))
    prop("Misc B43_N", (6, 5))
    prop("Misc B43_S", (8, 5))

    # The counting dais: tally posts at its edge, the scale table on the boards.
    prop("Misc B52_E", (22, 6))
    prop("Misc B52_E", (24, 6))
    prop("Misc C9_N", (25, 4))
    prop("Barrel A1", (26, 3))
    prop("Barrel A1", (26, 4))

    # The clerks' pens: a crate rail across the corner, one gap at the north.
    for x in range(20, 27):
        prop("Misc A1_E", (x, 11))
    yard.layers["Objects"].pop((22, 11), None)
    yard.blocked.discard((22, 11))
    prop("Barrel A2", (20, 13))
    prop("Barrel A2", (24, 14))
    prop("Misc C9_N", (25, 13))

    # Torches: the yard works by firelight — the coin-strike is a night craft.
    for cell in [(5, 4), (5, 15), (13, 15), (21, 6), (24, 7), (26, 10), (9, 12)]:
        prop("Torch2", cell, solid=False)

    # Spoil heaps in the west run — cast-off false strikes dumped by the cart gap.
    for cell in [(5, 8), (6, 9), (5, 10), (6, 7)]:
        prop("BrokenStone1", cell, layer="BrokenObjects")
    prop("BrokenStone small1", (7, 8), layer="BrokenObjects", solid=False)
    prop("BrokenStone small1", (5, 6), layer="BrokenObjects", solid=False)

    # The treeline hides the yard from the road — close canopy all around, heaviest north.
    for x in range(0, SIZE_X):
        prop("Tree A1_S", (x, 0), solid=True)
        prop("Tree A1_S", (x, SIZE_Y - 1), solid=True)
    for y in range(0, SIZE_Y):
        prop("Tree A1_S", (0, y), solid=True)
        prop("Tree A1_S", (SIZE_X - 1, y), solid=True)
    for x in (2, 6, 10, 18, 22, 28):
        prop("Tree A2_S", (x, 1), solid=True)
    for y in (2, 5, 9, 13, 17):
        prop("Tree A2_S", (1, y), solid=True)
        prop("Tree A2_S", (SIZE_X - 2, y), solid=True)

    # Grass scraps off the lane outside the wall.
    for cell in [(10, 19), (17, 18), (9, 17), (19, 20), (6, 19), (23, 19)]:
        prop("Flora B3_N", cell, layer="Objects", solid=False)


def anchors(yard: Yard) -> dict:
    ground = set(yard.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in yard.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 14) ** 2 + (c[1] - 18) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[stampworks] {len(reach)} of {len(walkable)} cells reachable from the lane")
    return {
        "player": point(near((14, 18))),
        "roadOut": point(near((14, 21))),
        "hearth": point(near((24, 11))),
        # villagers: clerk Novak penned at the crates, cook Vall by the crucible line,
        # die-setter Jory at the dais edge, runner Wicke watching the lane mouth.
        "villagers": [point(near((23, 13))), point(near((23, 12))), point(near((21, 7))),
                      point(near((14, 14)))],
        # arrivals: the coiners' watch — centre of the press floor.
        "arrivals": [point(near((14, 10))), point(near((11, 12))), point(near((18, 12)))],
        # timber (search spots): three writ-slips left on the benches, the master die on the dais
        # under guard, a cracked die-plate at the press line, four silver blanks in the spoil run.
        "timber": [point(near((12, 9))), point(near((15, 9))), point(near((18, 9))), point(near((24, 4))),
                   point(near((9, 9))), point(near((6, 8))), point(near((8, 12))), point(near((9, 6))),
                   point(near((7, 14)))],
        # stone (porters' wander homes): the weigh line and the lane.
        "stone": [point(near((17, 13))), point(near((11, 13)))],
        "plots": [],
    }


def main() -> None:
    yard = Yard()
    lay_ground(yard)
    dress_yard(yard)

    print(f"  dropped {drop_unknown_tiles(yard.layers)} cells the pack does not have")
    missing = check_tiles(yard.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in yard.blocked:
        yard.put("Colliders", cell, "Shadow5_E")

    fenced = fence_void(yard.layers, set(yard.layers.get("Ground", {})) - yard.blocked)
    print(f"  fenced {fenced} void edges")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [15, 12],
        "anchors": anchors(yard),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(yard.layers.get("Ground", {})) - yard.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in yard.layers.items()],
    }
    out = PROJECT / "Docs" / "stampworks_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Stampworks: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Colliders") for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "stampworks_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
