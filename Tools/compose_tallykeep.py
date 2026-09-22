"""The Tallykeep: the League's mint-fortress, composed from scratch — the grand finale of Act I.

Not a crop of the example map. A citadel laid cell by cell on its crag: a processional
causeway between banner poles up to the first curtain, a gatehouse mouth, an outer bailey
full of the washing army's camp, a second curtain, the mint court with its press row and
counting floor, and at the top the great press hall — three walls still standing, the
master press on the dais, the vault's chests behind it. Bigger and grander than the
Tollbank: this is where the washed coin was always going.
"""
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT / "Tools"))
from tileset_preview import render  # noqa: E402
from terrain import drop_unknown_tiles, check_tiles, reachable, fence_void  # noqa: E402

SIZE_X, SIZE_Y = 42, 30
DIRT = "Ground A1_E"
GRAVEL = "Ground G10_E"     # bailey gravel
STONE = "Ground G11_E"      # mint court flagstones
PATH = "Ground D1_E"        # the causeway's packed pale stone
FLOOR = "Ground E1_E"       # the press hall's dark planks
CLIFF = "Ground G14_E"      # bare crag at the map's flanks

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15, "Colliders": -100,
}

# The causeway climbs the south edge to the outer gate.
CAUSEWAY_X = (19, 22)        # x range of the processional road
GATE_S = (19, 22)            # the outer gate arch at the first curtain
CURTAIN_OUT_Y = 22
CURTAIN_IN_Y = 11
GATE_IN = (19, 22)           # the inner gate arch
# The press hall crowns the crag: three walls, open south face.
HALL = (15, 1, 26, 5)        # x0, y0, x1, y1


class Keep:
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


def lay_ground(keep: Keep) -> None:
    for x in range(SIZE_X):
        for y in range(SIZE_Y):
            keep.put("Ground", (x, y), DIRT)
    # The crag shoulders: bare stone at the map's east and west flanks above the bailey.
    for x in range(0, 3):
        for y in range(0, 18):
            keep.put("Ground 2", (x, y), CLIFF)
    for x in range(39, SIZE_X):
        for y in range(0, 18):
            keep.put("Ground 2", (x, y), CLIFF)
    # The causeway: pale packed stone from the south edge up through the outer gate.
    for y in range(CURTAIN_OUT_Y, SIZE_Y):
        for x in range(CAUSEWAY_X[0], CAUSEWAY_X[1] + 1):
            keep.put("Ground 2", (x, y), PATH)
    # And on through the bailey to the inner gate — the road the coin walked.
    for y in range(CURTAIN_IN_Y, CURTAIN_OUT_Y):
        for x in range(19, 23):
            keep.put("Ground 2", (x, y), PATH)
    # The outer bailey: a small gravel drill square west of the road; the camp keeps its churned dirt.
    for x in range(12, 18):
        for y in range(13, 19):
            keep.put("Ground 2", (x, y), GRAVEL)
    # The mint court between the curtains is flagstone.
    for x in range(9, 33):
        for y in range(5, 11):
            keep.put("Ground 2", (x, y), STONE)
    # The press hall floor.
    hx0, hy0, hx1, hy1 = HALL
    for x in range(hx0, hx1 + 1):
        for y in range(hy0, hy1 + 1):
            keep.put("Ground 2", (x, y), FLOOR)
            keep.block((x, y))  # a raised floor the court climbs to at the door
    # The hall's door step.
    keep.put("Ground 2", (20, 6), FLOOR)
    keep.put("Ground 2", (21, 6), FLOOR)
    keep.blocked.discard((20, 6))
    keep.blocked.discard((21, 6))


def lay_curtains(keep: Keep) -> None:
    # Outer curtain at y=22, x 4..37 — one clean tall line, broken only by the gate arch.
    for x in range(4, 38):
        if x in range(GATE_S[0], GATE_S[1] + 1):
            continue
        keep.prop((x, CURTAIN_OUT_Y), "Wall A12_E")
    # Gatehouse towers: heavier stubs flanking the arch, one cell thick.
    for x in (17, 18, 23, 24):
        keep.prop((x, CURTAIN_OUT_Y), "Wall A14_E")
    # Gate pylons at the arch mouth.
    keep.prop((19, 21), "Misc B52_E", solid=False)
    keep.prop((22, 21), "Misc B53_E", solid=False)
    # Bastion towers at the curtain ends.
    for cell in ((4, 21), (4, 22), (37, 21), (37, 22)):
        keep.prop(cell, "Wall A14_E")

    # Inner curtain at y=11, x 9..32 — the mint's own line.
    for x in range(9, 33):
        if x in range(GATE_IN[0], GATE_IN[1] + 1):
            continue
        keep.prop((x, CURTAIN_IN_Y), "Wall A10_E")
    for x in (17, 18, 23, 24):
        keep.prop((x, CURTAIN_IN_Y), "Wall A13_E")
    # Returns toward the crag shoulders.
    for y in range(6, 11):
        keep.prop((9, y), "Wall A10_E" if y % 2 else "Wall A11_E")
        keep.prop((32, y), "Wall A10_E" if y % 2 else "Wall A11_E")

    # The press hall: three walls standing — north face and both flanks; the south face
    # fell long ago, which is why the court can see the master press from below.
    hx0, hy0, hx1, hy1 = HALL
    for x in range(hx0, hx1 + 1):
        keep.prop((x, hy0), "Wall A15_N" if x % 2 else "Wall A16_N")
    for y in range(hy0, hy1 + 1):
        keep.prop((hx0, y), "Wall A15_W" if y % 2 else "Wall A16_W")
        keep.prop((hx1, y), "Wall A15_E" if y % 2 else "Wall A16_E")
    # Corner towers give the hall its silhouette.
    for cell in ((hx0, hy0), (hx0 - 1, hy0), (hx1, hy0), (hx1 + 1, hy0)):
        keep.prop(cell, "Wall A17_E")
    # The fallen south face: stump stubs at the corners only.
    keep.prop((hx0, hy1 + 1), "Misc E9_N")
    keep.prop((hx1, hy1 + 1), "Misc E9_N")


def dress_causeway(keep: Keep) -> None:
    p = keep.prop
    # Banner poles and torches alternate down the processional — the League's road of triumph.
    for i, y in enumerate(range(24, 29)):
        p((17, y), "Misc B43_N", solid=False)
        p((24, y), "Misc B43_E", solid=False)
        if i % 2:
            p((16, y), "Torch2", solid=False)
            p((25, y), "Torch2", solid=False)
    # A wrecked audit cart, a fallen siege frame, and spoil along the causeway's shoulders.
    p((15, 27), "Misc B55_E")
    p((14, 28), "Misc B58_E")
    p((27, 28), "Misc E9_N")
    p((28, 26), "Misc E8_N")
    p((10, 24), "Misc B55_E")
    p((11, 25), "Misc E9_N")
    p((31, 25), "Misc B58_E")
    p((33, 24), "Misc E10_N")
    for cell, tile in {(8, 27): "Misc E8_N", (13, 25): "Misc E6_N", (29, 27): "Misc E7_E",
                       (36, 26): "Misc E11_N", (5, 25): "Misc E9_N"}.items():
        p(cell, tile)
    # Dead sentinels at the map's south corners.
    for cell in ((6, 26), (10, 28), (33, 27), (36, 24), (2, 22), (39, 20), (16, 29), (26, 29)):
        p(cell, "Tree B4_W")


def dress_bailey(keep: Keep) -> None:
    p = keep.prop
    # The washing army's camp: dense tent rows west and east of the drill square.
    for cell in ((6, 14), (9, 13), (12, 14), (15, 13), (6, 17), (9, 18), (12, 17), (15, 18),
                 (7, 20), (10, 21), (13, 20), (29, 14), (32, 13), (35, 14), (27, 17), (30, 18),
                 (33, 17), (36, 18), (28, 20), (31, 21), (34, 20)):
        p(cell, "Misc B43_S")
    for cell in ((10, 15), (13, 16), (30, 15), (33, 16)):
        p(cell, "Misc B46_N")
    # Cookfires and their leavings.
    p((11, 16), "Misc C1_E", solid=False)
    p((30, 16), "Misc C1_E", solid=False)
    p((10, 17), "Misc B58_E")
    p((31, 17), "Misc B58_E")
    p((19, 17), "Misc C1_E", solid=False)
    p((22, 17), "Misc B58_E")
    # Pike racks and supply carts along the wall.
    for cell in ((6, 13), (15, 16), (26, 16), (36, 13)):
        p(cell, "Misc B52_E")
    for cell in ((7, 21), (34, 21), (14, 21), (28, 21)):
        p(cell, "Misc B54_E")
    # Camp clutter.
    for cell, tile in {(9, 16): "Misc B45_N", (16, 17): "Misc E6_N", (25, 17): "Misc E7_E",
                       (36, 19): "Misc E10_N", (5, 19): "Misc E8_N", (17, 19): "Misc B45_N",
                       (24, 19): "Misc E11_N", (35, 20): "Misc E9_N", (26, 20): "Misc B58_E"}.items():
        p(cell, tile)
    # The washing banner over the inner gate approach.
    p((19, 12), "Misc B43_N", solid=False)
    p((22, 12), "Misc B43_N", solid=False)


def dress_court(keep: Keep) -> None:
    p = keep.prop
    # The mint court: counting desks in a row — counter slabs with tally shelves.
    for i, x in enumerate(range(12, 30, 3)):
        p((x, 7), "Misc C9_N", solid=False)
        p((x, 8), "Misc B26_N", solid=False)
    # Coin-crate mounds against the curtains.
    for cell in ((10, 6), (11, 6), (30, 6), (31, 6), (12, 9), (29, 9)):
        p(cell, "Misc B54_E")
    # The furnace the dies were softened over — still lit.
    p((20, 8), "FirePlace", solid=False)
    p((21, 9), "Misc E6_N", solid=False)
    # Ledger shelves along the hall's fallen face.
    p((14, 6), "Misc C12_E", solid=False)
    p((27, 6), "Misc C12_E", solid=False)
    # Torch line so the night shift counted true.
    for cell in ((10, 10), (18, 10), (23, 10), (31, 10)):
        p(cell, "Torch2", solid=False)


def dress_hall(keep: Keep) -> None:
    p = keep.prop
    hx0, hy0, hx1, hy1 = HALL
    # The master press on its dais: the great slab and its beam frame.
    p((20, 2), "Misc C9_N", solid=False)      # press bed
    p((19, 2), "Misc C10_N", solid=False)
    p((21, 2), "Misc C10_N", solid=False)
    p((20, 3), "Misc B60_N", solid=False)     # the die in the press
    # The vault dais behind the press: chest rows and the seal shelves — the mint's hoard.
    for x in (16, 17, 18, 23, 24, 25):
        p((x, 2), "Chest A2_E", solid=False)
    for x in (16, 25):
        p((x, 3), "Chest B1_E", solid=False)
    p((17, 3), "Misc B54_E", solid=False)
    p((24, 3), "Misc B54_E", solid=False)
    p((16, 4), "Misc B8_E", solid=False)      # seal shelf
    p((25, 4), "Misc B8_E", solid=False)
    # Braziers flanking the dais so the mint burns through the night.
    p((18, 4), "FirePlace", solid=False)
    p((23, 4), "FirePlace", solid=False)
    # Grand banners on the hall's standing walls.
    p((hx0 + 1, 3), "Misc B43_N", solid=False)
    p((hx1 - 1, 3), "Misc B43_N", solid=False)
    # Assay clutter on the floor.
    p((18, 3), "Misc A1_E", solid=False)
    p((23, 3), "Misc A2_E", solid=False)
    p((19, 4), "Misc B45_N", solid=False)
    p((22, 4), "Misc B45_N", solid=False)
    p((21, 4), "Misc A3_E", solid=False)


def dress_flanks(keep: Keep) -> None:
    p = keep.prop
    # The crag's shoulders: rock piles and wind-bent trees the walls grew out of.
    for cell, tile in {(2, 8): "Misc E9_N", (1, 12): "Misc E10_N", (40, 7): "Misc E9_N",
                       (41, 13): "Misc E10_N", (1, 4): "Tree B4_W", (40, 3): "Tree B4_W",
                       (2, 16): "Misc E8_N", (40, 16): "Misc E11_N"}.items():
        p(cell, tile)
    for cell in ((3, 6), (5, 9), (38, 8), (37, 5), (4, 14), (38, 15)):
        keep.put("Objects", cell, "Flora B3_N")


def anchors(keep: Keep) -> dict:
    ground = set(keep.layers.get("Ground", {})) | set(keep.layers.get("Ground 2", {}))
    walkable = {c for c in ground if c not in keep.blocked}
    arrival = min(walkable, key=lambda c: (c[0] - 20) ** 2 + (c[1] - 28) ** 2)
    near = lambda t: min(walkable, key=lambda c: (c[0] - t[0]) ** 2 + (c[1] - t[1]) ** 2)
    reach = reachable(walkable, arrival)
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    print(f"[tallykeep] {len(reach)} of {len(walkable)} cells reachable from the causeway")
    return {
        "player": point(near((20, 28))),
        "roadOut": point(near((20, 29))),
        "hearth": point(near((11, 16))),           # the west cookfire
        # villagers (post-clear survivors): Keeper Soll on the press floor, Clerk Nance at
        # the seal shelf, Presshand Dory at the desks, Boy Kit by the gate.
        "villagers": [point(near((19, 6))), point(near((25, 6))), point(near((13, 8))),
                      point(near((21, 23)))],
        # arrivals: the gatehouse mouth, the bailey centre, the mint court, the press dais.
        "arrivals": [point(near((20, 23))), point(near((20, 16))), point(near((20, 9))),
                     point(near((20, 6)))],
        # timber (search spots): three mint tallies on the desks, the die-rack, four seal-boxes.
        "timber": [point(near((12, 7))), point(near((18, 7))), point(near((27, 7))),
                   point(near((15, 9))),
                   point(near((11, 6))), point(near((30, 6))), point(near((12, 20))), point(near((29, 20)))],
        # stone: two freed porters' wander homes, plus the assay-page corner.
        "stone": [point(near((15, 14))), point(near((26, 14)))],
        "plots": [],
    }


def main() -> None:
    keep = Keep()
    lay_ground(keep)
    lay_curtains(keep)
    dress_causeway(keep)
    dress_bailey(keep)
    dress_court(keep)
    dress_hall(keep)
    dress_flanks(keep)

    print(f"  dropped {drop_unknown_tiles(keep.layers)} cells the pack does not have")
    missing = check_tiles(keep.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    for cell in keep.blocked:
        keep.put("Colliders", cell, "Shadow5_E")

    fenced = fence_void(keep.layers, set(keep.layers.get("Ground", {})) - keep.blocked)
    print(f"  fenced {fenced} void edges")

    payload = {
        "region": [0, SIZE_X, 0, SIZE_Y],
        "plaza": [20, 16],
        "anchors": anchors(keep),
        "houses": [],
        "walkable": [[x, y] for x, y in sorted(set(keep.layers.get("Ground", {})) - keep.blocked)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in keep.layers.items()],
    }
    out = PROJECT / "Docs" / "tallykeep_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"composed Tallykeep: {sum(len(l['cells']) for l in payload['layers'])} tiles, "
          f"{len(payload['walkable'])} walkable cells")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Colliders") for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.8).save(scratch / "tallykeep_composed.png")
    print("preview written")


if __name__ == "__main__":
    main()
