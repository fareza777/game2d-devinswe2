"""Paints the inventory icons the pack does not ship: rings, amulets, gloves and boots.

The pack's LootIcons sheet covers weapons, chests, helms, shields and leg armour, so every piece added for
the new equipment slots needs a picture of its own. Each is rendered with FLUX in the sheet's own manner —
hand-painted, thick dark outline, soft shading — on white, then the white is cut away from the edges inwards
so the outline stays whole.

Writes Assets/Oathfire/Art/Icons/<name>.png (128x128, transparent). Requests are cached by content.
"""
import sys
from collections import deque
from pathlib import Path

from PIL import Image

from replicate_generate import image

TOOLS = Path(__file__).resolve().parent
OUT = TOOLS.parent / "Assets" / "Oathfire" / "Art" / "Icons"
SCRATCH = TOOLS / "cache" / "gear_icons"

STYLE = ("single hand-painted 2D fantasy RPG inventory icon, cartoon painterly style like a mobile game item, "
         "thick dark brown outline around the whole object, soft cel shading, slight three-quarter view, "
         "centered with generous empty margin, isolated on a plain pure white background, no text, "
         "no drop shadow, no frame, no border")

ICONS = {
    "gear_ring_copper": "a plain copper ring band with a small dull brown stone",
    "gear_ring_iron": "a sturdy iron signet ring with a square grey stone",
    "gear_ring_gold": "an ornate gold ring set with a glowing red ruby",
    "gear_amulet_bone": "a necklace: a thin leather cord loop with one small carved white bone tooth pendant hanging from it",
    "gear_amulet_silver": "a silver amulet with a round blue gem on a chain",
    "gear_amulet_gold": "a golden sun-shaped amulet with an amber gem on a gold chain",
    "gear_gloves_leather": "a pair of brown leather gloves",
    "gear_gloves_iron": "a pair of iron plated gauntlets over leather",
    "gear_gloves_gold": "a pair of gold trimmed steel gauntlets",
    "gear_boots_leather": "a pair of tall brown leather boots",
    "gear_boots_iron": "a pair of brown leather boots reinforced with iron plates on the shins and toes, standing side by side",
    "gear_boots_gold": "a pair of knight boots made of polished steel plates with gold trim, standing side by side",
}


def cut_out(path: Path, size: int = 128) -> Image.Image:
    """Makes the white surround transparent, working in from the edges so the object's own light areas stay."""
    picture = Image.open(path).convert("RGBA")
    width, height = picture.size
    pixels = picture.load()

    def is_background(colour) -> bool:
        r, g, b, _ = colour
        return r > 226 and g > 226 and b > 226

    seen = set()
    queue = deque((x, y) for x in range(width) for y in (0, height - 1))
    queue.extend((x, y) for y in range(height) for x in (0, width - 1))
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not (0 <= x < width and 0 <= y < height):
            continue
        seen.add((x, y))
        if not is_background(pixels[x, y]):
            continue
        pixels[x, y] = (255, 255, 255, 0)
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    # Holes the edge fill cannot reach — the inside of a ring — are cleared when they are big and flat white.
    # Small white patches are highlights on the object and stay.
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or not is_background(pixels[x, y]):
                continue
            region, pocket = [], deque([(x, y)])
            while pocket:
                px, py = pocket.popleft()
                if (px, py) in seen or not (0 <= px < width and 0 <= py < height):
                    continue
                seen.add((px, py))
                if not is_background(pixels[px, py]):
                    continue
                region.append((px, py))
                pocket.extend(((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)))
            if len(region) > width * height * 0.004:
                for px, py in region:
                    pixels[px, py] = (255, 255, 255, 0)

    box = picture.getbbox()
    if box:
        picture = picture.crop(box)
    # Square, with a little air round it, the way the pack's own icons sit in their cells.
    side = int(max(picture.size) * 1.12)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(picture, ((side - picture.width) // 2, (side - picture.height) // 2))
    return square.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    only = set(sys.argv[1:])
    for name, subject in ICONS.items():
        if only and name not in only:
            continue
        raw = image(SCRATCH / f"{name}.png", "1:1", f"{subject}, {STYLE}")
        cut_out(raw).save(OUT / f"{name}.png")
        print(f"  Icons/{name}.png")


if __name__ == "__main__":
    main()
