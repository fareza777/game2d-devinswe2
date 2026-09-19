"""Builds Greymarch, the walled city Rennfall answers to.

Rennfall is composed building by building because a village is a handful of houses on a plan. A city is not:
its walls, gate towers, streets and squares only make sense together, and the pack's own artist already
drew one as a single connected structure. That whole district is placed here as one piece — so nothing is
cut in half — and the valley around it, the approach road and our gameplay anchors are built around it.

Writes Docs/greymarch_map.json and a preview PNG.
"""
import json
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from tileset_preview import render  # noqa: E402
from doors import doors_for, hidden_by_roof, open_interiors, roof_cells  # noqa: E402
from walkability import clear_phantom_colliders  # noqa: E402
from terrain import check_tiles, clear_lane, drop_unknown_tiles, open_route, reachable  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
STAMPS = json.loads((PROJECT / "Docs" / "tileset_stamps.json").read_text(encoding="utf-8"))["stamps"]

CITY_STAMP = 0          # the walled district: keep, halls, market, walls, fields
MARGIN = 14             # open country around the walls
DIRT = "Ground A1_E"
GRASS = ["Ground A2_N", "Ground A2_E"]
ROAD = "Ground H1_E"
SCRUB = ["Flora A5_W", "Flora A1_E", "Flora A6_S"]
TREES = ["Tree A1_S", "Tree A3_S", "Tree D1_E", "Tree D3_N", "Tree A2_W",
         "Tree A4_N", "Tree B1_E", "Tree B1_S", "Tree E4_N", "Tree E4_E"]

LAYER_ORDER = {
    "Ground": -12, "Ground 2": -11, "Ground 3": -10, "Shadows1": -9,
    "BrokenObjects": -1, "WallDetail1": 0, "Walls": 0, "Objects": 0,
    "WallDetail2": 1, "Roof1": 2, "Roof2": 11, "Roof3": 15,
    "Colliders": -100, "Colliders(indestructible)": -100,
}

# Roofs are not here on purpose: they overhang the streets, and blocking every cell under an eave lined the
# city's lanes with invisible walls. Walls and the artist's own collider tiles are what stop a body.
STRUCTURE_LAYERS = ("Walls", "WallDetail1", "WallDetail2")


class City:
    def __init__(self, seed: int = 20260917):
        self.layers: dict[str, dict[tuple[int, int], str]] = {}
        self.blocked: set[tuple[int, int]] = set()
        self.random = random.Random(seed)

    def put(self, layer: str, cell: tuple[int, int], tile: str) -> None:
        self.layers.setdefault(layer, {})[cell] = tile

    def pick(self, options: list[str]) -> str:
        return options[self.random.randrange(len(options))]


def place_city(city: City, origin: tuple[int, int]) -> tuple[int, int, int, int]:
    """Stamps the district down whole and reports the rectangle it occupies."""
    stamp = STAMPS[CITY_STAMP]
    ox, oy = origin
    for piece in stamp["pieces"]:
        cell = (piece["x"] + ox, piece["y"] + oy)
        city.put(piece["layer"], cell, piece["tile"])
        if piece["layer"] in STRUCTURE_LAYERS or piece["layer"].startswith("Collider"):
            city.blocked.add(cell)
    # The city's houses have empty doorway arches too; each single arch gets its door.
    for x, y, tile in doors_for(CITY_STAMP, stamp["pieces"]):
        cell = (x + ox, y + oy)
        layer = next(name for name in ("Objects", "WallDetail1", "WallDetail2") if cell not in city.layers.get(name, {}))
        city.put(layer, cell, tile)
        city.blocked.add(cell)
    return ox, oy, ox + stamp["width"] - 1, oy + stamp["height"] - 1


def lay_country(city: City, size: tuple[int, int], walls: tuple[int, int, int, int]) -> None:
    """Grass, scrub and woodland outside the walls, so the city sits in a landscape."""
    width, height = size
    x0, y0, x1, y1 = walls
    for x in range(width):
        for y in range(height):
            if (x, y) in city.layers.get("Ground", {}):
                continue  # the district brought its own ground
            city.put("Ground", (x, y), DIRT)
            city.put("Ground 2", (x, y), city.pick(GRASS))
            outside = x < x0 - 2 or x > x1 + 2 or y < y0 - 2 or y > y1 + 2
            if outside and city.random.random() < 0.09:
                city.put("Ground 3", (x, y), city.pick(SCRUB))
            # Woodland thickens towards the map edge, and never blocks the approach road.
            edge = min(x, y, width - 1 - x, height - 1 - y)
            if outside and edge < 8 and city.random.random() < 0.45 and not on_road(x, y, walls):
                city.put("Walls", (x, y), city.pick(TREES))
                city.blocked.add((x, y))


def on_road(x: int, y: int, walls: tuple[int, int, int, int]) -> bool:
    """The road runs west out of the gate, level with the middle of the wall."""
    _x0, y0, _x1, y1 = walls
    return abs(y - (y0 + y1) // 2) <= 1


def lay_road(city: City, size: tuple[int, int], walls: tuple[int, int, int, int]) -> None:
    width, _height = size
    x0, y0, _x1, y1 = walls
    lane = (y0 + y1) // 2
    for x in range(0, x0):
        for offset in (-1, 0, 1):
            cell = (x, lane + offset)
            city.layers.get("Walls", {}).pop(cell, None)
            city.blocked.discard(cell)
            city.put("Ground 2", cell, ROAD)


# What makes the approach and the square feel lived in. Stalls and small goods can be walked past; tents, the well
# and watch posts are solid, but only ever on a single cell with open ground around it.
STALLS = ["Misc B17_E", "Misc B18_E", "Misc B19_E", "Misc B14_E"]
SMALL_GOODS = ["Misc B20_E", "Misc B2_E", "Barrel A1", "Barrel A3", "Misc B1_E"]
SOLID_DECOR = {"Misc B42_E", "Misc B43_E", "Misc B46_E", "Misc B6_E", "Misc B55_E", "Misc B52_E"}


def dress_city(city: "City", size: tuple[int, int], walls: tuple[int, int, int, int]) -> set[tuple[int, int]]:
    """Lights the approach road, pitches a caravan camp outside the gate and sets a market in the square.

    Returns every cell given a prop, so people are never placed standing on a stall.
    """
    width, height = size
    x0, y0, x1, y1 = walls
    lane = (y0 + y1) // 2
    ground = set(city.layers.get("Ground", {}))
    occupied = set().union(*(set(city.layers.get(name, {})) for name in ("Objects", "Walls", "WallDetail1", "WallDetail2")))
    placed: set[tuple[int, int]] = set()

    def open_around(cell, reach=1):
        return all((cell[0] + dx, cell[1] + dy) in ground
                   and (cell[0] + dx, cell[1] + dy) not in city.blocked
                   and (cell[0] + dx, cell[1] + dy) not in occupied
                   for dx in range(-reach, reach + 1) for dy in range(-reach, reach + 1))

    def put(cell, tile):
        if cell in placed or not open_around(cell, 0) or abs(cell[1] - lane) <= 1 and cell[0] < x0:
            return False
        city.put("Objects", cell, tile)
        occupied.add(cell)
        placed.add(cell)
        if tile in SOLID_DECOR:
            city.blocked.add(cell)
        return True

    # The approach: torches either side of the road every few steps, and a signpost before the gate.
    for x in range(2, x0 - 1, 3):
        put((x, lane - 2), "Torch2")
        put((x, lane + 2), "Torch2")
    put((x0 - 5, lane - 3), "Misc B52_E")

    # A caravan camp in the meadow north of the road, waiting on the gate.
    camp = [((x0 - 9, lane + 5), "Misc B43_E"), ((x0 - 5, lane + 6), "Misc B46_E"), ((x0 - 7, lane + 3), "Misc B6_E"),
            ((x0 - 10, lane + 3), "Barrel A1"), ((x0 - 11, lane + 4), "Barrel A3"), ((x0 - 4, lane + 4), "Misc B2_E")]
    for cell, tile in camp:
        if 0 <= cell[0] < width and 0 <= cell[1] < height and open_around(cell):
            put(cell, tile)

    # The market: open paving near the middle of the walls, stalls spaced so a body passes between them.
    centre = ((x0 + x1) / 2, (y0 + y1) / 2)
    # The district's paving sits in its base ground layer, the lanes' surface in the one above.
    paving = {cell: tile for name in ("Ground", "Ground 2") for cell, tile in city.layers.get(name, {}).items()
              if tile.startswith(("Ground H", "Ground I", "Ground D", "Ground B"))}
    candidates = sorted(
        (cell for cell in paving
         if x0 + 3 <= cell[0] <= x1 - 3 and y0 + 3 <= cell[1] <= y1 - 3 and open_around(cell, 1)),
        key=lambda c: (c[0] - centre[0]) ** 2 + (c[1] - centre[1]) ** 2)
    market: list[tuple[int, int]] = []
    for cell in candidates:
        if len(market) >= 10:
            break
        if all(abs(cell[0] - m[0]) + abs(cell[1] - m[1]) >= 3 for m in market):
            market.append(cell)
    for index, cell in enumerate(market):
        tile = "Misc B42_E" if index == 0 else STALLS[(index - 1) % len(STALLS)] if index <= 4 else city.pick(SMALL_GOODS)
        put(cell, tile)
    print(f"  dressed the city: {len(placed)} props, market of {len(market)}")
    return placed


def walkable_cells(city: City, size: tuple[int, int], interiors: set[tuple[int, int]]) -> set[tuple[int, int]]:
    width, height = size
    ground = set(city.layers.get("Ground", {}))
    return {cell for cell in ground
            if cell not in city.blocked and cell not in interiors and 0 <= cell[0] < width and 0 <= cell[1] < height}


def anchors(city: City, walkable: set[tuple[int, int]], walls: tuple[int, int, int, int],
            size: tuple[int, int]) -> dict:
    """Places arrivals, people and the way home on ground that is actually open."""
    x0, y0, x1, y1 = walls
    # Everything must stand somewhere a body can walk to from the city square.
    walkable = walkable & reachable(walkable, ((x0 + x1) // 2, (y0 + y1) // 2))
    lane = (y0 + y1) // 2
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    taken: list[tuple[int, int]] = []
    roofs = roof_cells(city.layers)

    def free_near(target: tuple[int, int], apart: float = 3.0) -> dict:
        for cell in sorted(walkable, key=lambda c: (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2):
            if any(((cell[0] - t[0]) ** 2 + (cell[1] - t[1]) ** 2) ** 0.5 < apart for t in taken):
                continue
            if hidden_by_roof(cell, roofs):
                continue
            taken.append(cell)
            return point(cell)
        return point(target)

    centre = ((x0 + x1) // 2, (y0 + y1) // 2)
    return {
        "hearth": free_near(centre),                      # the city square
        "player": free_near((x0 - 4, lane)),               # arriving on the road, outside the gate
        "roadOut": free_near((2, lane)),                   # the way back to the valley
        "board": free_near((x0 + 6, lane)),                # notices just inside the gate
        "brann": free_near((centre[0] - 4, centre[1] + 4)),
        "maren": free_near((centre[0] + 4, centre[1] - 4)),
        "villagers": [free_near((centre[0] + dx, centre[1] + dy), apart=2.5)
                      for dx, dy in ((-6, 0), (6, 2), (0, -6), (3, 6), (-3, -5), (8, -3))],
        "plots": [],
        "timber": [], "stone": [],
        "arrivals": [free_near((x0 - 2, lane)), free_near((centre[0], y1 - 4))],
    }


def main() -> None:
    city = City()
    stamp = STAMPS[CITY_STAMP]
    size = (stamp["width"] + MARGIN * 2, stamp["height"] + MARGIN * 2)
    walls = place_city(city, (MARGIN, MARGIN))
    lay_country(city, size, walls)
    lay_road(city, size, walls)

    decor = dress_city(city, size, walls)
    print(f"  dropped {drop_unknown_tiles(city.layers)} cells the example scene left as unresolved tiles")
    missing = check_tiles(city.layers)
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    houses = open_interiors(city.layers, city.blocked)
    print(f"  cleared {clear_phantom_colliders(city.layers, city.blocked)} colliders standing on open ground")
    # The road in: the example district's gate is decorated with crates and posts that carry colliders, so
    # the front gate was shut and the only way in was a detour around the wall.
    lane = (walls[1] + walls[3]) // 2
    centre = ((walls[0] + walls[2]) // 2, (walls[1] + walls[3]) // 2)
    print(f"  opened the gate road: {open_route(city.layers, city.blocked, (2, lane), centre)} props moved aside")
    print(f"  cleared the main street: {clear_lane(city.layers, city.blocked, lane, 2, centre[0])} stalls and crates stood in it")
    interiors = {(c["x"], c["y"]) for house in houses for c in house["interior"]}
    walkable = walkable_cells(city, size, interiors) - decor
    for cell in city.blocked:
        if 0 <= cell[0] < size[0] and 0 <= cell[1] < size[1]:
            city.put("Colliders", cell, "Shadow5_E")

    payload = {
        "region": [0, size[0], 0, size[1]],
        "plaza": [walls[0], walls[1]],
        "anchors": anchors(city, walkable, walls, size),
        "houses": houses,
        "walkable": [[x, y] for x, y in sorted(walkable)],
        "layers": [{"name": name, "order": LAYER_ORDER.get(name, 0),
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(cells.items())]}
                   for name, cells in city.layers.items()],
    }
    out = PROJECT / "Docs" / "greymarch_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"Greymarch: {size[0]}x{size[1]} cells, "
          f"{sum(len(l['cells']) for l in payload['layers'])} tiles, {len(houses)} enterable houses, {len(walkable)} walkable")

    pieces = [{"x": c["x"], "y": c["y"], "tile": c["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Collider")
              for c in layer["cells"]]
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    render(pieces, scale=0.32).save(scratch / "greymarch_preview.png")
    print("preview written")


if __name__ == "__main__":
    main()
