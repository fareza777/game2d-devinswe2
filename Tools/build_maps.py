"""Cuts the game's locations out of the tileset's example town.

The pack ships a finished map: a village, a walled town, farms, a trade road, graveyards and ruins.
Rather than hand-place 1902 kinds of tile, each of our scenes is a region of that map, cropped and given
the anchor points our gameplay objects stand on.

Writes Docs/<name>_map.json (read by the Unity scene builders) and a preview PNG for each.
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from extract_tileset_stamps import SCENE, parse_tilemaps, tile_names_by_guid  # noqa: E402
from walkability import clear_phantom_colliders  # noqa: E402
from terrain import check_tiles, drop_unknown_tiles  # noqa: E402
from tileset_preview import render  # noqa: E402

PROJECT = Path(__file__).resolve().parent.parent
CACHE = PROJECT / "Tools" / "cache" / "demo_town_layers.json"

SCENES = {
    # Rennfall is no longer cropped from the example scene: compose_rennfall.py plans it and owns its map file.
    # The trade road south of it: cobbles through woodland, a farmstead, hay fields and wayside ruins.
    "trade_road": {"region": (-90, -30, -60, -10), "plaza": 4, "style": "road"},
    # Blackthorn Hollow: a palisaded bandit camp in a ravine, tents round a cookfire, a long pass west to an
    # overgrown ruin where their captain keeps his prisoner. Reached by a trail off the trade road.
    "hollow": {"region": (-159, -114, -56, -30), "plaza": 3, "style": "hollow"},
    # The old iron keep north of Greymarch: the pack's walled great hall stands alone at the map's edge,
    # so the build adds its own approach — a bare forecourt on the rise and a breach in the vestibule wall.
    "keep": {"region": (-158, -135, -116, -66), "plaza": 3, "style": "keep"},
    # Saltwold Grange: the Salt League's walled weigh-court, cut from the example map's tithe-barn
    # compound — a gatehouse lane off the west wall, then the market court under the manor terrace.
    # The farm fields south of the channel are the League's own land: kept as backdrop beyond the walls.
    "grange": {"region": (32, 92, -46, 20), "plaza": 2, "style": "grange"},
}


def load_layers() -> list[dict]:
    if CACHE.exists():
        data = json.loads(CACHE.read_text(encoding="utf-8"))
        return [{"name": l["name"], "order": l["order"],
                 "cells": {tuple(int(v) for v in k.split(",")): t for k, t in l["cells"].items()}}
                for l in data]

    print("parsing the example scene (about a minute)...")
    layers = parse_tilemaps(SCENE.read_text(encoding="utf-8", errors="ignore"), tile_names_by_guid())
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    CACHE.write_text(json.dumps([{"name": l["name"], "order": l["order"],
                                  "cells": {f"{k[0]},{k[1]}": v for k, v in l["cells"].items()}}
                                 for l in layers]), encoding="utf-8")
    return layers


def crop(layers: list[dict], region: tuple[int, int, int, int]) -> list[dict]:
    x0, x1, y0, y1 = region
    cropped = []
    for layer in layers:
        cells = {(x - x0, y - y0): tile for (x, y), tile in layer["cells"].items()
                 if x0 <= x <= x1 and y0 <= y <= y1}
        if cells:
            cropped.append({"name": layer["name"], "order": layer["order"], "cells": cells})
    return cropped


def open_ground(layers: list[dict]) -> set[tuple[int, int]]:
    """Cells that have ground under them and nothing standing on them — where people can walk."""
    ground, blocked = set(), set()
    for layer in layers:
        for cell, tile in layer["cells"].items():
            if layer["name"].startswith("Ground"):
                ground.add(cell)
            elif layer["name"].startswith("Collider") or tile.startswith(("Wall", "Roof", "Door", "Tree")):
                blocked.add(cell)
    return ground - blocked


def built_centre(layers: list[dict]) -> tuple[float, float] | None:
    """The middle of the standing architecture.

    A settlement scene has to open among its buildings. Choosing by open ground alone puts the player in
    the widest empty field, which is exactly where a village looks like scattered props.
    """
    cells = [cell for layer in layers if layer["name"] in ("Walls", "Roof1", "Roof2", "Roof3", "WallDetail1")
             for cell, tile in layer["cells"].items() if tile.startswith(("Wall", "Roof", "Door"))]
    if not cells:
        return None
    return sum(x for x, _ in cells) / len(cells), sum(y for _, y in cells) / len(cells)


def largest_clearing(walkable: set[tuple[int, int]], size: int,
                     towards: tuple[float, float] | None = None) -> tuple[int, int] | None:
    """Finds a square of free ground big enough for the hearth plaza, nearest a chosen point."""
    if not walkable:
        return None
    if towards:
        centre_x, centre_y = towards
    else:
        centre_x = sum(x for x, _ in walkable) / len(walkable)
        centre_y = sum(y for _, y in walkable) / len(walkable)
    best, best_distance = None, float("inf")
    for x, y in walkable:
        if not all((x + dx, y + dy) in walkable for dx in range(size) for dy in range(size)):
            continue
        distance = (x + size / 2 - centre_x) ** 2 + (y + size / 2 - centre_y) ** 2
        if distance < best_distance:
            best, best_distance = (x, y), distance
    return best


def road_anchors(walkable: set[tuple[int, int]]) -> dict:
    """Spreads the anchors along the length of a road instead of around a square.

    Travel scenes read front to back: you arrive at one end, the trouble is waiting in the middle, and the
    way home is behind you. Placing them by ring distance would put the ambush on top of the arrival.
    """
    ordered = sorted(walkable, key=lambda cell: cell[0] + cell[1])
    open_all_round = lambda cell: all((cell[0] + dx, cell[1] + dy) in walkable for dx in (-1, 0, 1) for dy in (-1, 0, 1))
    # The hero arrives with open ground on every side, never shoulder to shoulder with a wall.
    arrival = next((cell for cell in ordered[len(ordered) // 12:] if open_all_round(cell)), ordered[len(ordered) // 12])
    near, middle, far = ordered[len(ordered) // 12], ordered[len(ordered) // 2], ordered[-len(ordered) // 8]
    quarter, three_quarter = ordered[len(ordered) // 4], ordered[3 * len(ordered) // 4]
    point = lambda cell: {"x": cell[0], "y": cell[1]}
    # A trail off the road: open ground well past the ambush and clear of the salvage, where a post marks the way.
    taken = [middle, quarter, three_quarter, arrival, far]
    trail = next((cell for cell in ordered[5 * len(ordered) // 8:]
                  if open_all_round(cell) and all(abs(cell[0] - t[0]) + abs(cell[1] - t[1]) >= 6 for t in taken)),
                 ordered[5 * len(ordered) // 8])
    # Wick's lamp: out in the open a third of the way along, where a boy running home would have dropped it.
    taken.append(trail)
    search = next((cell for cell in ordered[3 * len(ordered) // 8:]
                   if open_all_round(cell) and all(abs(cell[0] - t[0]) + abs(cell[1] - t[1]) >= 5 for t in taken)),
                  ordered[3 * len(ordered) // 8])
    # The overgrown keep road, past the ambush toward the Greymarch end — it only becomes a road once
    # Vesna's ledger says someone is using it.
    taken.append(search)
    trail2 = next((cell for cell in ordered[3 * len(ordered) // 4:]
                   if open_all_round(cell) and all(abs(cell[0] - t[0]) + abs(cell[1] - t[1]) >= 6 for t in taken)),
                  ordered[3 * len(ordered) // 4])
    # The League's weigh-court lane: off the road early, before the ambush — a toll lane that answers
    # to the grange's walls, not the keep's silence. Maren's writ names it once the manifest is read.
    taken.append(trail2)
    trail3 = next((cell for cell in ordered[len(ordered) // 5:len(ordered) // 3]
                   if open_all_round(cell) and all(abs(cell[0] - t[0]) + abs(cell[1] - t[1]) >= 6 for t in taken)),
                  ordered[len(ordered) // 5])
    return {
        "search": point(search),
        "trail": point(trail),
        "trail2": point(trail2),
        "trail3": point(trail3),
        "hearth": point(middle),   # where the road closes
        "player": point(arrival),
        "board": point(ordered[len(ordered) // 12 + 3]),  # the way back, a few steps from where you arrived
        "brann": point(far), "maren": point(far),
        "villagers": [],
        "plots": [],
        "timber": [point(quarter), point(three_quarter)],
        "stone": [],
        "arrivals": [point(far)],
    }


def hollow_anchors(layers: list[dict], walkable: set[tuple[int, int]]) -> dict:
    """Lays the Hollow out as a walk from the trail down into the camp, along the pass, to the ruin.

    Distances are walked, not measured in a straight line: the ravine doubles back on itself, and a camp that
    is close as the crow flies can be a long way round.
    """
    from collections import deque

    def tiles(prefix):
        return [cell for layer in layers for cell, tile in layer["cells"].items() if tile.startswith(prefix)]

    def nearest_walkable(target):
        return min(walkable, key=lambda c: (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2)

    def centroid(cells):
        return (sum(x for x, _ in cells) / len(cells), sum(y for _, y in cells) / len(cells))

    def walk_from(start):
        dist = {start: 0}
        queue = deque([start])
        while queue:
            cell = queue.popleft()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nxt = (cell[0] + dx, cell[1] + dy)
                if nxt in walkable and nxt not in dist:
                    dist[nxt] = dist[cell] + 1
                    queue.append(nxt)
        return dist

    def openness(cell, reach=2):
        return sum((cell[0] + dx, cell[1] + dy) in walkable for dx in range(-reach, reach + 1) for dy in range(-reach, reach + 1))

    # Only the ravine itself: stray open cells walled off from it are unreachable and must not hold anything.
    any_cell = max(walkable, key=openness)
    walkable = set(walk_from(any_cell))
    ruin = nearest_walkable(centroid(tiles("WallFlora")))
    from_ruin = walk_from(ruin)
    reachable = set(from_ruin)
    far_end = max(reachable, key=lambda c: (from_ruin[c], openness(c)))

    fire = nearest_walkable(centroid(tiles("FirePlace")))
    camp_distance = from_ruin.get(fire, from_ruin[far_end] // 2)

    def best(candidates):
        return max(candidates, key=openness)

    arrival = best([c for c in reachable if from_ruin[far_end] - 7 <= from_ruin[c] <= from_ruin[far_end] - 4])
    boss = best([c for c in reachable if 1 <= from_ruin[c] <= 5])
    from_boss = walk_from(boss)
    prisoner = best([c for c in reachable if 3 <= from_boss.get(c, 0) <= 4])
    pass_point = best([c for c in reachable if abs(from_ruin[c] - camp_distance * 0.5) <= 2])
    camp = best([c for c in reachable if from_ruin[c] >= camp_distance - 1 and abs(from_ruin[c] - camp_distance) <= 2])
    # Stores the camp lives on: one crate by the tents, one at the mouth of the pass.
    crates = [best([c for c in reachable if abs(from_ruin[c] - (camp_distance - 5)) <= 1 and openness(c) < 25]
                   or [c for c in reachable if abs(from_ruin[c] - (camp_distance - 5)) <= 1]),
              best([c for c in reachable if abs(from_ruin[c] - (camp_distance * 0.5 + 6)) <= 1 and openness(c) < 25]
                   or [c for c in reachable if abs(from_ruin[c] - (camp_distance * 0.5 + 6)) <= 1])]
    print(f"[hollow] walk: far end {from_ruin[far_end]}, camp {camp_distance}, arrival {from_ruin[arrival]}, "
          f"pass {from_ruin[pass_point]}, boss {from_ruin[boss]}")

    point = lambda cell: {"x": cell[0], "y": cell[1]}
    return {
        "player": point(arrival),
        "roadOut": point(far_end),
        "hearth": point(boss),
        "villagers": [point(prisoner)],
        "arrivals": [point(camp), point(pass_point)],
        "timber": [point(c) for c in crates],
        "plots": [], "stone": [],
    }


def pick_anchors(walkable: set[tuple[int, int]], plaza: tuple[int, int]) -> dict:
    """Chooses where the gameplay objects stand, on ground that is actually free.

    Everything is placed relative to the plaza and spread apart, so the settlement reads as a place people
    laid out rather than a grid of props.
    """
    centre = (plaza[0] + 2, plaza[1] + 2)
    free = sorted(walkable, key=lambda c: (c[0] - centre[0]) ** 2 + (c[1] - centre[1]) ** 2)
    taken: list[tuple[int, int]] = []

    def claim(min_ring: float, max_ring: float, apart: float = 4.0):
        for cell in free:
            distance = ((cell[0] - centre[0]) ** 2 + (cell[1] - centre[1]) ** 2) ** 0.5
            if not min_ring <= distance <= max_ring:
                continue
            if any(((cell[0] - t[0]) ** 2 + (cell[1] - t[1]) ** 2) ** 0.5 < apart for t in taken):
                continue
            taken.append(cell)
            return {"x": cell[0], "y": cell[1]}
        return {"x": centre[0], "y": centre[1]}

    # Unity's JsonUtility cannot read nested arrays, so every anchor is an object with named fields.
    return {
        "hearth": {"x": centre[0], "y": centre[1]},
        "player": claim(3, 5),
        "board": claim(3, 6),
        "brann": claim(3, 7),
        "maren": claim(5, 9),
        "villagers": [claim(4, 10) for _ in range(3)],
        "plots": [claim(6, 11) for _ in range(4)],
        "timber": [claim(9, 16) for _ in range(3)],
        "stone": [claim(9, 16) for _ in range(2)],
        "arrivals": [claim(13, 20, apart=6.0) for _ in range(3)],
    }


def keep_approach(layers: list[dict]) -> None:
    """Builds the keep's forecourt and opens the vestibule wall.

    The walled hall stands alone at the edge of the example map — nothing connects to it. The keep gets
    what a ruin on a rise would have: a bare earth forecourt outside the vestibule's north wall, a breach
    where the masonry has fallen, and scattered wreckage of the carts that came here loaded and left empty.
    Region-relative cells: x' = x + 158, y' = y + 116.
    """
    ground = next(layer for layer in layers if layer["name"] == "Ground")
    objects = next(layer for layer in layers if layer["name"] == "Objects")
    walls = next(layer for layer in layers if layer["name"] == "Walls")
    collider_layers = [layer for layer in layers if layer["name"].startswith("Collider")]

    # The forecourt: plain earth on the rise outside the vestibule's north wall.
    apron = [(x, y) for x in range(6, 19) for y in range(1, 7)]
    for i, cell in enumerate(apron):
        ground["cells"][cell] = PLAIN_EARTH[(cell[0] + cell[1]) % len(PLAIN_EARTH)]

    # The breach: two fallen wall cells in the vestibule's north face, earth going through.
    for cell in ((11, 7), (12, 7)):
        walls["cells"].pop(cell, None)
        for layer in collider_layers:
            layer["cells"].pop(cell, None)
        ground["cells"][cell] = "Ground A1_S"
    # Broken masonry either side of the gap.
    objects["cells"][(10, 7)] = "Stone A5_N"
    objects["cells"][(13, 7)] = "Misc C9_E"

    # The vestibule's double door to the corridor has rusted off its hinges: remove the leaves, the
    # mullion and their blockers so the passage is open. Abs (-147/-146/-145, -101) -> rel (11/12/13, 15).
    for cell in ((11, 15), (12, 15), (13, 15)):
        objects["cells"].pop(cell, None)
        for layer in collider_layers:
            layer["cells"].pop(cell, None)
    detail = next((layer for layer in layers if layer["name"] == "WallDetail1"), None)
    if detail:
        detail["cells"].pop((12, 15), None)

    # The quartermaster's last barricade: a crate and its blocker sealing the vault arch from inside
    # the hall. Cleared so the manifest room can be reached. Abs (-145, -73) -> rel (13, 43).
    objects["cells"].pop((13, 43), None)
    for layer in collider_layers:
        layer["cells"].pop((13, 43), None)

    # What a siege camp leaves: a dead cart, dropped crates, stone off the wall.
    objects["cells"][(8, 2)] = "Misc B5_E"      # a cart that never made the return run
    objects["cells"][(16, 3)] = "Misc B8_E"     # split crates
    objects["cells"][(7, 5)] = "Stone A5_N"     # fallen masonry
    objects["cells"][(17, 2)] = "Misc B45_N"    # stone pile
    # Wall stumps and dead trees frame the forecourt instead of fence posts.
    for cell, tile in {(5, 1): "Wall A11_E", (5, 3): "Wall A11_E", (5, 6): "Wall A10_E",
                     (19, 1): "Wall A11_E", (19, 4): "Wall A11_E", (19, 6): "Wall A10_E",
                     (7, 1): "Tree B4_W", (13, 1): "Tree B4_W", (17, 6): "Tree B4_W"}.items():
        walls["cells"][cell] = tile


def keep_anchors(layers: list[dict], walkable: set[tuple[int, int]]) -> dict:
    """Places the keep's anchors on its spine: forecourt, vestibule, corridor, hall, vault.

    The scene reads as a gauntlet — in through the breach, down the torch-lit corridor, across the great
    hall — so anchors sit at the rooms, not in rings round a square.
    """
    from collections import deque

    def nearest_walkable(target):
        return min(walkable, key=lambda c: (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2)

    # Everything must be reachable from the forecourt through the breach.
    arrival = nearest_walkable((12, 3))
    dist = {arrival: 0}
    queue = deque([arrival])
    while queue:
        cell = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (cell[0] + dx, cell[1] + dy)
            if nxt in walkable and nxt not in dist:
                dist[nxt] = dist[cell] + 1
                queue.append(nxt)
    reachable = set(dist)
    print(f"[keep] {len(reachable)} of {len(walkable)} cells reachable from the forecourt")

    def room(abs_x, abs_y):
        return nearest_walkable((abs_x + 158, abs_y + 116))

    vestibule = room(-146, -105)
    corridor = room(-146, -97)
    hall = room(-147, -82)
    dais = room(-143, -75)
    vault = room(-145, -68)
    vault_stores = room(-145, -70)
    vestibule_stores = room(-148, -105)
    road_out = nearest_walkable((12, 1))

    def walk_dist(target):
        return dist.get(target, -1)

    print(f"[keep] walk: breach->vestibule {walk_dist(vestibule)}, corridor {walk_dist(corridor)}, "
          f"hall {walk_dist(hall)}, dais {walk_dist(dais)}, vault {walk_dist(vault)}")

    point = lambda cell: {"x": cell[0], "y": cell[1]}
    return {
        "player": point(arrival),
        "roadOut": point(road_out),
        "hearth": point(dais),
        "villagers": [point(vault)],
        "arrivals": [point(vestibule), point(corridor), point(hall)],
        "timber": [point(vault_stores), point(vestibule_stores)],
        "plots": [], "stone": [],
    }


def grange_approach(layers: list[dict]) -> None:
    """Opens the grange's west gate and the barn bays; dresses the lane the gleaners wait in.

    The example map runs the compound as set dressing: the gatehouse doors are leaves glued to wall
    cells, and the barn bays are arch-fronted stalls sealed by Door objects. Cutting them open turns
    the court into somewhere that works. Region-relative cells: x' = x - 32, y' = y + 46.
    """
    objects = next(layer for layer in layers if layer["name"] == "Objects")
    walls = next(layer for layer in layers if layer["name"] == "Walls")
    collider_layers = [layer for layer in layers if layer["name"].startswith("Collider")]

    # The gatehouse pair, abs (39,-35)/(39,-34): wall faces with Door C5_S leaves pinned on. Open —
    # the League wants the court working again, not sealed. Abs -> rel (7, 11), (7, 12).
    for cell in ((7, 11), (7, 12)):
        objects["cells"].pop(cell, None)
        walls["cells"].pop(cell, None)
        for layer in collider_layers:
            layer["cells"].pop(cell, None)

    # Two barn bays along the yard's south face lose their rusted leaves: the arches stay, so they read
    # as open stall mouths. Abs (58,-18)/(66,-18) -> rel (26, 28), (34, 28). The third bay's lock held.
    for cell in ((26, 28), (34, 28)):
        objects["cells"].pop(cell, None)
        for layer in collider_layers:
            layer["cells"].pop(cell, None)

    # The lane the gleaners are camped in: a handcart that never made the gate, and their watch-fire
    # below the wall. Abs (34,-30)/(35,-37) -> rel (2, 16), (3, 9).
    objects["cells"][(2, 16)] = "Misc B5_E"
    objects["cells"][(3, 9)] = "Misc C8_S"
    objects["cells"][(4, 10)] = "Misc B45_N"


def grange_ground(layers: list[dict]) -> set[tuple[int, int]]:
    """Walkable cells for a walled working court: ground minus colliders, wall faces and props.

    The shared open_ground treats every tile named Wall*/Roof*/Door*/Tree* as a stopper, which is right
    for the hollow and the keep but wrong here: roof tiles sit on Roof layers above covered ways (the
    gatehouse tunnel is one), and Wall C6/D6 arches are open doorways the pack leaves passable until
    doors.py pins a leaf on them. Blocking by layer instead: colliders, the real wall layer (less the
    arches), standing props, and trees.
    """
    ground, blocked = set(), set()
    arch = ("Wall C6", "Wall D6", "Wall D11")
    props = ("Chest", "FirePlace", "Brazier", "Door", "Misc", "Stone", "Table", "Torch", "Cart", "Tree")
    for layer in layers:
        name = layer["name"]
        for cell, tile in layer["cells"].items():
            if name.startswith("Ground"):
                ground.add(cell)
            elif name.startswith("Collider"):
                blocked.add(cell)
            elif name in ("Walls", "BrokenObjects"):
                if not tile.startswith(arch):
                    blocked.add(cell)
            elif name in ("Objects", "WallDetail1", "WallDetail2"):
                if tile.startswith(props) and not tile.startswith(arch):
                    blocked.add(cell)
    return ground - blocked


def grange_anchors(layers: list[dict], walkable: set[tuple[int, int]]) -> dict:
    """Places the grange's anchors up its spine: lane, gatehouse, weigh-court, bays, terrace.

    The compound was a walled tithe-barn in the example map — two districts (lane, court) that only
    the opened gate joins. Anchors are pinned to named rooms the way the keep's were, and every one
    is proven reachable by walking from the lane's top.
    """
    from collections import deque

    def nearest_walkable(target):
        return min(walkable, key=lambda c: (c[0] - target[0]) ** 2 + (c[1] - target[1]) ** 2)

    arrival = nearest_walkable((34 - 32, -45 + 46))
    dist = {arrival: 0}
    queue = deque([arrival])
    while queue:
        cell = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (cell[0] + dx, cell[1] + dy)
            if nxt in walkable and nxt not in dist:
                dist[nxt] = dist[cell] + 1
                queue.append(nxt)
    reachable = set(dist)
    print(f"[grange] {len(reachable)} of {len(walkable)} cells reachable from the lane")

    def near(abs_x, abs_y):
        return min(reachable, key=lambda c: (c[0] - (abs_x - 32)) ** 2 + (c[1] - (abs_y + 46)) ** 2)

    point = lambda cell: {"x": cell[0], "y": cell[1]}
    return {
        # In at the lane's top, out the way you came.
        "player": point(near(34, -45)),
        "roadOut": point(near(33, -46)),
        # The brazier corner of the weigh-floor, where the factor's people warm themselves.
        "hearth": point(near(47, -21)),
        # villagers: Factor Hask at the weigh stalls, Reeve Collum by the bays, Old Mirren at the fire,
        # Tess among the gleaners in the lane, Gate-warden Lene inside the gatehouse.
        "villagers": [point(near(57, -31)), point(near(56, -17)), point(near(46, -22)),
                      point(near(35, -36)), point(near(41, -33))],
        # arrivals: the bay foragers' fight (between the opened stalls), the weigh-floor's middle,
        # the sack terrace under the manor.
        "arrivals": [point(near(60, -19)), point(near(56, -29)), point(near(72, -27))],
        # timber (search spots): inside the two opened bays, the sack row, the manor terrace where the
        # missing key-warden was last seen — then four cells where grain gets spilled and stays spilled.
        "timber": [point(near(59, -16)), point(near(67, -16)), point(near(68, -26)), point(near(70, -40)),
                   point(near(60, -24)), point(near(52, -22)), point(near(70, -30)), point(near(44, -25))],
        # stone (porters' wander homes): two knots on the weigh-floor.
        "stone": [point(near(58, -28)), point(near(63, -32))],
        "plots": [],
    }


PLAIN_EARTH = ["Ground A1_E", "Ground A1_N", "Ground A1_S", "Ground A1_W"]


def plain_earth(layers: list[dict]) -> int:
    """Makes a ravine floor read as bare ground.

    The example map paves this hollow with edged transition tiles, so the floor looks like it is divided into
    panels with borders drawn between them. Down here it should simply be earth.
    """
    changed = 0
    for layer in layers:
        if layer["name"] != "Ground":
            continue
        for index, (cell, tile) in enumerate(sorted(layer["cells"].items())):
            if tile.startswith("Ground A1"):
                continue
            layer["cells"][cell] = PLAIN_EARTH[index % len(PLAIN_EARTH)]
            changed += 1
    return changed


def fence_edges(layers: list[dict], walkable: set[tuple[int, int]]) -> int:
    """Adds an invisible collider on every empty cell that touches walkable ground.

    A region cut from the example map ends where the crop does, not where a wall does. A town has buildings
    and trees round its edges; a ravine cut out of the middle of the map has gaps onto nothing.
    """
    ground = {cell for layer in layers if layer["name"].startswith("Ground") for cell in layer["cells"]}
    blocked = {cell for layer in layers if layer["name"].startswith("Collider") for cell in layer["cells"]}
    fence = {}
    for x, y in walkable:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                cell = (x + dx, y + dy)
                if cell not in ground and cell not in blocked:
                    fence[cell] = "Ground G21_S"   # the pack's own full-diamond collision tile
    if fence:
        layers.append({"name": "Colliders(edge)", "order": 0, "cells": fence})
    return len(fence)


def build(name: str, spec: dict, scratch: Path) -> None:
    layers = crop(load_layers(), spec["region"])
    print(f"[{name}] layers:", {layer["name"]: len(layer["cells"]) for layer in layers})
    cleared = clear_phantom_colliders({layer["name"]: layer["cells"] for layer in layers}, set())
    print(f"[{name}] cleared {cleared} colliders standing on open ground")

    if spec.get("style") == "hollow":
        print(f"[{name}] levelled {plain_earth(layers)} cells of patchy ground into plain earth")
    if spec.get("style") == "keep":
        keep_approach(layers)
        print(f"[{name}] paved the forecourt and breached the vestibule wall")
    if spec.get("style") == "grange":
        grange_approach(layers)
        print(f"[{name}] opened the west gate and the barn bays")
    named = {layer["name"]: layer["cells"] for layer in layers}
    dropped = drop_unknown_tiles(named)
    missing = [entry for entry in check_tiles(named) if not entry.startswith("?")]
    if missing:
        raise SystemExit("tiles the pack does not have: " + ", ".join(missing))
    print(f"[{name}] dropped {dropped} cells the example scene left as unresolved tiles")
    walkable = open_ground(layers)
    if spec.get("style") == "grange":
        walkable = grange_ground(layers)
    if spec.get("style") == "keep":
        # The vault's doorway is a Wall D11 arch: it renders a doorframe but has no collider under it.
        walkable.add((13, 44))
    if spec.get("style") in ("hollow", "keep", "grange"):
        fenced = fence_edges(layers, walkable)
        print(f"[{name}] fenced {fenced} open edges so nobody walks off into the void")
    towards = built_centre(layers) if spec.get("style") == "settlement" else None
    plaza = largest_clearing(walkable, spec["plaza"], towards)
    print(f"[{name}] walkable {len(walkable)}; buildings centre {towards}; square at {plaza}")

    if spec.get("style") == "road":
        anchors = road_anchors(walkable)
    elif spec.get("style") == "hollow":
        anchors = hollow_anchors(layers, walkable)
    elif spec.get("style") == "keep":
        anchors = keep_anchors(layers, walkable)
    elif spec.get("style") == "grange":
        anchors = grange_anchors(layers, walkable)
    else:
        anchors = pick_anchors(walkable, plaza or (30, 30))
    payload = {
        "region": list(spec["region"]),
        "plaza": list(plaza) if plaza else None,
        "anchors": anchors,
        "walkable": [[x, y] for x, y in sorted(walkable)],
        "layers": [{"name": l["name"], "order": l["order"],
                    "cells": [{"x": c[0], "y": c[1], "tile": t} for c, t in sorted(l["cells"].items())]}
                   for l in layers],
    }
    out = PROJECT / "Docs" / f"{name}_map.json"
    out.write_text(json.dumps(payload), encoding="utf-8")
    print(f"[{name}] wrote {out.name}: {sum(len(l['cells']) for l in payload['layers'])} cells")

    pieces = [{"x": cell["x"], "y": cell["y"], "tile": cell["tile"], "order": layer["order"]}
              for layer in payload["layers"] if not layer["name"].startswith("Collider")
              for cell in layer["cells"]]
    render(pieces, scale=0.5).save(scratch / f"{name}_preview.png")


def main() -> None:
    scratch = Path(sys.argv[1]) if len(sys.argv) > 1 else PROJECT / "Builds"
    scratch.mkdir(parents=True, exist_ok=True)
    for name, spec in SCENES.items():
        build(name, spec, scratch)


if __name__ == "__main__":
    main()
