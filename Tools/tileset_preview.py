"""Renders an isometric tile layout to a PNG, outside Unity.

Building a settlement is a drawing job, and drawing it through a Unity batch build takes minutes per look.
This draws the same cells in seconds, so the town can be composed, judged and reworked before Unity ever
sees it. It reads the pack's real sprites, pivots and pixels-per-unit, so what it shows is what ships.
"""
import re
from functools import lru_cache
from pathlib import Path

from PIL import Image

PROJECT = Path(__file__).resolve().parent.parent
PACK = PROJECT / "Assets" / "SmallScaleInt" / "Fantasy kingdom Tileset" / "Environment"
TILES = PACK / "Tiles"
SPRITES = PACK / "Sprites"

CELL_W, CELL_H = 1.0, 0.5  # the isometric grid Rennfall uses


@lru_cache(maxsize=1)
def sprite_files() -> dict[str, Path]:
    """Sprite GUID to its PNG, read from the .meta beside each image."""
    table = {}
    for meta in SPRITES.glob("*.png.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8", errors="ignore"), re.M)
        if match:
            table[match.group(1)] = meta.with_suffix("")
    return table


@lru_cache(maxsize=4096)
def tile_sprite(tile_name: str) -> tuple[Image.Image, float, float, float] | None:
    """The tile's image, its pivot in pixels, and its pixels-per-unit."""
    asset = TILES / f"{tile_name}.asset"
    if not asset.exists():
        return None
    text = asset.read_text(encoding="utf-8", errors="ignore")
    guid = re.search(r"m_Sprite: \{fileID: \d+, guid: ([0-9a-f]{32})", text)
    if not guid:
        return None
    png = sprite_files().get(guid.group(1))
    if not png or not png.exists():
        return None

    meta = png.with_suffix(".png.meta").read_text(encoding="utf-8", errors="ignore")
    pivot = re.search(r"spritePivot: \{x: ([\d.]+), y: ([\d.]+)\}", meta)
    ppu = re.search(r"spritePixelsToUnits: ([\d.]+)", meta)
    image = Image.open(png).convert("RGBA")
    px = float(pivot.group(1)) * image.width if pivot else image.width / 2
    py = float(pivot.group(2)) * image.height if pivot else image.height / 2
    return image, px, py, float(ppu.group(1)) if ppu else 100.0


def cell_to_world(x: int, y: int) -> tuple[float, float]:
    """Unity's isometric cell-to-world, so the preview matches the engine exactly."""
    return (x - y) * CELL_W / 2.0, (x + y) * CELL_H / 2.0


def render(pieces, scale: float = 1.0, background=(28, 34, 26, 255), margin: int = 64) -> Image.Image:
    """Draws pieces — dicts of x, y, tile and order — into one image, painter's order.

    Within a draw order, cells further back (smaller screen y) are drawn first, which is how the engine
    sorts isometric sprites along the Y axis.
    """
    placed = []
    for piece in pieces:
        loaded = tile_sprite(piece["tile"])
        if not loaded:
            continue
        image, px, py, ppu = loaded
        wx, wy = cell_to_world(piece["x"], piece["y"])
        # Screen space: y grows downward, and the sprite hangs off its pivot.
        left = wx * ppu * scale - px * scale
        top = -wy * ppu * scale - (image.height - py) * scale
        placed.append((piece.get("order", 0), wy, left, top, image))

    if not placed:
        raise ValueError("nothing to draw")

    placed.sort(key=lambda item: (item[0], -item[1]))
    min_x = min(item[2] for item in placed)
    min_y = min(item[3] for item in placed)
    max_x = max(item[2] + item[4].width * scale for item in placed)
    max_y = max(item[3] + item[4].height * scale for item in placed)

    canvas = Image.new("RGBA", (int(max_x - min_x) + margin * 2, int(max_y - min_y) + margin * 2), background)
    for _order, _wy, left, top, image in placed:
        if scale != 1.0:
            image = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.LANCZOS)
        canvas.alpha_composite(image, (int(left - min_x) + margin, int(top - min_y) + margin))
    return canvas
