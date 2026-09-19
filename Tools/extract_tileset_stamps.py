"""Reads the tileset's own example town and writes the buildings out as reusable stamps.

The pack ships 1902 tiles whose names (Roof B7_W, Wall D3_N ...) say nothing about which piece is a gable,
a corner or a doorway. Rather than guess, this reads how the pack's own artist assembled the demo town and
saves each structure as a list of (layer, offset, tile) so the scene builder can stamp real buildings.

Output: Docs/tileset_stamps.json
"""
import json
import re
from collections import defaultdict
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
PACK = PROJECT / "Assets" / "SmallScaleInt" / "Fantasy kingdom Tileset"
SCENE = PACK / "Example scene" / "Example scene.unity"
TILES = PACK / "Environment" / "Tiles"
OUT = PROJECT / "Docs" / "tileset_stamps.json"

# Tiles whose names start with these belong to a structure rather than to the ground.
STRUCTURE = ("Wall", "Roof", "Door", "Window", "Stair")


def tile_names_by_guid() -> dict[str, str]:
    """Maps every tile asset's GUID to its name, read from the .meta files beside them."""
    guids = {}
    for meta in TILES.glob("*.asset.meta"):
        match = re.search(r"guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8", errors="ignore"))
        if match:
            guids[match.group(1)] = meta.name[:-len(".asset.meta")]
    return guids


def scene_objects(text: str) -> tuple[dict[str, str], dict[str, int]]:
    """GameObject names and TilemapRenderer sorting orders, keyed by the object's fileID."""
    names, orders = {}, {}
    for document in text.split("--- !u!")[1:]:
        class_id, _, body = document.partition(" ")
        anchor = body.split("\n", 1)[0].lstrip("&").strip()
        if class_id == "1":
            match = re.search(r"m_Name: (.*)", body)
            if match:
                names[anchor] = match.group(1).strip()
        elif class_id == "483693784":  # TilemapRenderer
            owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            order = re.search(r"m_SortingOrder: (-?\d+)", body)
            if owner:
                orders[owner.group(1)] = int(order.group(1)) if order else 0
    return names, orders


def parse_tilemaps(text: str, guids: dict[str, str]) -> list[dict]:
    """Every Tilemap component in the scene: its layer name, draw order, and the tile in each cell."""
    names, orders = scene_objects(text)
    maps = []
    for block_start in (m.start() for m in re.finditer(r"^  m_Tiles:$", text, re.M)):
        block_end = text.find("\n  m_AnimatedTiles:", block_start)
        if block_end < 0:
            block_end = len(text)
        block = text[block_start:block_end]

        # The component's own header sits just above m_Tiles; its m_GameObject names the layer.
        header = text.rfind("--- !u!", 0, block_start)
        owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", text[header:block_start])
        owner_id = owner.group(1) if owner else ""

        array_start = text.find("m_TileAssetArray:", block_end)
        array_end = text.find("m_TileSpriteArray:", array_start)
        palette = [guids.get(guid, f"?{guid[:6]}")
                   for guid in re.findall(r"guid: ([0-9a-f]{32})", text[array_start:array_end])]

        cells = {}
        for entry in re.finditer(
                r"- first: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}\s*\n\s*second:\s*\n\s*serializedVersion: \d+\s*\n\s*m_TileIndex: (\d+)",
                block):
            x, y, _z, index = (int(v) for v in entry.groups())
            if index < len(palette):
                cells[(x, y)] = palette[index]
        if cells:
            maps.append({"name": names.get(owner_id, "?"), "order": orders.get(owner_id, 0), "cells": cells})
    return maps


def find_structures(maps: list[dict]) -> list[dict]:
    """Groups structure tiles into connected clusters — one cluster is one building."""
    merged: dict[tuple[int, int], list[tuple[int, str]]] = defaultdict(list)
    for layer, layer_map in enumerate(maps):
        for cell, name in layer_map["cells"].items():
            if name.startswith(STRUCTURE):
                merged[cell].append((layer, name))

    seen: set[tuple[int, int]] = set()
    clusters = []
    for start in merged:
        if start in seen:
            continue
        stack, cluster = [start], []
        seen.add(start)
        while stack:
            cell = stack.pop()
            cluster.append(cell)
            x, y = cell
            for step in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
                neighbour = (x + step[0], y + step[1])
                if neighbour in merged and neighbour not in seen:
                    seen.add(neighbour)
                    stack.append(neighbour)
        clusters.append(cluster)
    return clusters, merged


def main() -> None:
    guids = tile_names_by_guid()
    text = SCENE.read_text(encoding="utf-8", errors="ignore")
    maps = parse_tilemaps(text, guids)
    print(f"tilemaps with content: {len(maps)}; cells: {sum(len(m['cells']) for m in maps)}")
    for index, layer in enumerate(maps):
        print(f"  layer {index}: {layer['name']!r} order={layer['order']} cells={len(layer['cells'])}")

    clusters, merged = find_structures(maps)
    stamps = []
    for cluster in clusters:
        if len(cluster) < 6:
            continue  # a stray wall fragment, not a building
        min_x = min(x for x, _ in cluster)
        min_y = min(y for _, y in cluster)
        max_x = max(x for x, _ in cluster)
        max_y = max(y for _, y in cluster)
        footprint = set(cluster)

        # A building is not just its walls. Taking only structure tiles leaves the interior floor, the
        # doorstep and the furniture behind, so the house is rebuilt standing on whatever ground happens to
        # be underneath — grass, in our case. Everything inside the footprint comes along: floors and props
        # from every layer, and structure tiles only where this building actually stands, so a neighbour's
        # wall is never dragged in with it.
        pieces = []
        for layer_index, layer_map in enumerate(maps):
            structural = layer_map["name"] in ("Walls", "WallDetail1", "WallDetail2", "Roof1", "Roof2", "Roof3")
            for cell, name in layer_map["cells"].items():
                if not (min_x <= cell[0] <= max_x and min_y <= cell[1] <= max_y):
                    continue
                if structural and cell not in footprint:
                    continue
                pieces.append({"x": cell[0] - min_x, "y": cell[1] - min_y,
                               "layer": layer_map["name"], "order": layer_map["order"], "tile": name})

        stamps.append({"cells": len(cluster), "width": max_x - min_x + 1, "height": max_y - min_y + 1,
                       "pieces": pieces})

    stamps.sort(key=lambda s: -s["cells"])
    OUT.write_text(json.dumps({"stamps": stamps}, indent=1), encoding="utf-8")
    print(f"structures found: {len(stamps)}")
    for stamp in stamps[:12]:
        print(f"  {stamp['width']}x{stamp['height']} cells={stamp['cells']} pieces={len(stamp['pieces'])}")


if __name__ == "__main__":
    main()
