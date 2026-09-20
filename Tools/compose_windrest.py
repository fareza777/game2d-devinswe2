"""The Windrest: the mill knoll on the rise north of Rennfall, composed from scratch.

A green hill field, not a camp: the League's windmill turns over a stone yard, rows of stooked
grain stand in the tilled west field, and the cart lane climbs from the town's north street to
the mill door. Open to the sky on purpose — the wind is the point, and nothing else in the
valley looks this bare and green.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable  # noqa: E402

SIZE_X, SIZE_Y = 34, 26
MEADOW = "Ground A2_N"
FIELD = "Ground B2_N"        # tilled gold-brown soil under the stook rows
LANE = "Ground D1_E"         # pale packed cart track
YARD = "Ground E1_N"         # worn cobbles round the mill door

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The stooked grain field west of the yard — the knoll's reason to exist.
FIELD_RECT = (3, 6, 14, 13)
# The cobbled mill yard on the rise, mill at its high corner.
YARD_RECT = (18, 4, 27, 11)
MILL = (23, 5)


class Knoll:
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


def lay_ground(knoll: Knoll) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            knoll.put("Ground", (x, y), MEADOW)
    # The tilled field — one unbroken sheet of worked soil, stooks are what stand on it.
    for x in range(FIELD_RECT[0], FIELD_RECT[2] + 1):
        for y in range(FIELD_RECT[1], FIELD_RECT[3] + 1):
            knoll.put("Ground 2", (x, y), FIELD)
    # The cart lane: in at the south edge, climbing the rise, bending east to the yard gate.
    for y in range(12, SIZE_Y):
        for x in (9, 10):
            knoll.put("Ground 2", (x, y), LANE)
    for x in range(9, 21):
        for y in (12, 13):
            knoll.put("Ground 2", (x, y), LANE)
    for y in range(7, 14):
        for x in (20, 21):
            knoll.put("Ground 2", (x, y), LANE)
    # The mill yard apron, cobbled where the carts turn.
    for x in range(YARD_RECT[0], YARD_RECT[2] + 1):
        for y in range(YARD_RECT[1], YARD_RECT[3] + 1):
            knoll.put("Ground 2", (x, y), YARD)


def dress_knoll(knoll: Knoll) -> None:
    p = knoll.prop
    # The mill itself: the pack's animated windmill — stone tower, turning vanes, the only
    # moving landmark in the valley. Its base stands on the yard's high corner.
    knoll.put("Objects", MILL, "WindMill 1")
    for cell in ((23, 5), (24, 5), (23, 6), (24, 6)):
        knoll.block(cell)
    # The yard's working clutter: the sack hoist, winnowing frame, standing stone, meal sacks.
    p((26, 6), "Misc B29_N")                    # sack hoist by the mill door
    p((19, 6), "Misc B14_E")                    # the count's canopy where sacks are weighed
    p((18, 8), "Misc B21_E")                    # drying rack
    p((19, 9), "Misc B21_N", solid=False)       # second rack, laid flat
    p((25, 9), "Misc B48_N")                    # winnowing frame
    p((26, 8), "Misc B15_N", solid=False)       # meal sacks waiting on the cart
    p((20, 10), "Misc C14_N")                   # the cracked spare millstone, kept as a marker
    p((21, 8), "Misc C5_N", solid=False)        # quern pot by the door
    p((28, 10), "Misc B6_E")                    # the grain cart, loaded and waiting
    p((27, 4), "Misc B41_N")                    # miller's awning tent against the wall of weather
    p((29, 7), "Misc B42_N")                    # the yard well
    p((19, 4), "Misc B54_E", solid=False)       # notice board at the yard gate
    # Stook rows in the field — cut grain standing to dry, every other cell so it reads as rows.
    for x in range(FIELD_RECT[0] + 1, FIELD_RECT[2]):
        for y in range(FIELD_RECT[1] + 1, FIELD_RECT[3]):
            if (x + y) % 2 == 0:
                knoll.prop((x, y), "Misc B8_N" if (x * 5 + y) % 3 else "Misc B8_E")
    # Field markers: the scare-pole and a banner that shows the wind's mind.
    p((8, 9), "Misc B52_E", solid=False)
    p((9, 9), "Misc B45_N", solid=False)
    # Loose sheaves along the lane where the cart rattles uphill.
    for cell, tile in {(10, 15): "Misc B8_E", (9, 19): "Misc B8_N", (11, 22): "Misc B8_E"}.items():
        knoll.put("Objects", cell, tile)
    # Torches at the lane bend and the yard gate — the mill works before dawn.
    p((8, 12), "Torch2", solid=False)
    p((22, 13), "Torch2", solid=False)
    p((20, 5), "Torch2", solid=False)
    # The hill stays open — trees only where the ground falls away at the edges.
    for cell, tile in {(0, 3): "Tree A2_S", (0, 8): "Tree A3_S", (1, 14): "Tree A2_S",
                       (2, 20): "Tree A1_S", (5, 24): "Tree A2_S", (14, 25): "Tree A3_S",
                       (15, 3): "Tree A1_S", (16, 0): "Tree A2_S", (30, 1): "Tree A2_S",
                       (32, 5): "Tree A3_S", (33, 11): "Tree A2_S", (33, 18): "Tree A1_S",
                       (30, 24): "Tree A2_S", (22, 25): "Tree A3_S"}.items():
        knoll.prop(cell, tile)
    # Hedge tufts marking the field's edges — wind-breaks, not walls.
    for cell in ((4, 5), (7, 5), (11, 5), (13, 5), (15, 9), (15, 12), (3, 14), (6, 14), (13, 14)):
        knoll.put("Objects", cell, "Flora B1_S")
    for cell in ((5, 16), (12, 17), (16, 16), (17, 20), (24, 15), (31, 14), (31, 20), (4, 22), (12, 2)):
        knoll.put("Objects", cell, "Flora B3_N")


def anchors(knoll: Knoll) -> dict:
    ground = set(knoll.layers.get("Ground", {}))
    walkable = {c for c in ground if c not in knoll.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 9) ** 2 + (c[1] - 24) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[windrest] {len(reach)} of {len(walkable)} cells reachable from the lane")
    return {
        "player": point(near((9, 23))),
        "roadOut": point(near((9, 25))),
        "hearth": point(near((26, 8))),
        # villagers: Miller Corl at the mill door, Bryd at the drying racks, mill-lad Tam in the
        # field, and the count's man Odo at the weigh canopy.
        "villagers": [point(near((22, 7))), point(near((18, 9))), point(near((8, 10))),
                      point(near((19, 7)))],
        # arrivals: grain-raiders rush the field rows; the lane bend; the yard gate.
        "arrivals": [point(near((6, 8))), point(near((12, 13))), point(near((17, 12)))],
        # timber (search spots): tally-sticks dropped in the stooks, then spilled meal sacks
        # along the cart lane.
        "timber": [point(near((5, 8))), point(near((10, 7))), point(near((12, 11))),
                   point(near((10, 16))), point(near((9, 19))), point(near((11, 21))),
                   point(near((10, 24)))],
        # stone (wander homes): Odle the carter walks the lane between cart and gate.
        "stone": [point(near((15, 12)))],
        "plots": [],
    }


def main() -> None:
    knoll = Knoll()
    lay_ground(knoll)
    dress_knoll(knoll)

    print(f"  dropped {drop_unknown_tiles(knoll.layers)} cells the pack does not have")
    missing = check_tiles(knoll.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in knoll.blocked:
        knoll.put("Colliders", cell, "Shadow5_E")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [20, 8],
        "anchors": anchors(knoll),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(knoll.layers.get("Ground", {})) - knoll.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in knoll.layers.items()],
    }
    out = PROJECT / "Docs" / "windrest_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Windrest: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if layer["name"] != "Colliders" for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "windrest_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
