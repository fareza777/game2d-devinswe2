"""Generates the raw art for Oathfire's app icon and splash sigil into Tools/cache/brand for review.

The icon has to work at 48px on a crowded home screen: one subject, one light source, a silhouette readable at a
glance, and the game's ember against black. The sigil is a flat emblem the splash screen animates.

Usage: python make_brand_art.py
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from recraft_generate import CODEX_PALETTE, generate
from replicate_generate import image

OUT = Path(__file__).resolve().parent / "cache" / "brand"

ICON_SUBJECT = (
    "square game app icon, a hooded man warden seen close from the chest up, short dark beard, scarred brow, fierce "
    "glowing amber eyes, cupping a living flame in both hands in front of his chest, ember sparks rising, behind him "
    "in pitch darkness pale skeletal figures stand in a dead forest watching, strong central silhouette, extremely high "
    "contrast, one warm amber light source, deep black shadows, no text, no letters, no border"
)

JOBS = [
    ("icon_recraft_a.png", "recraft", "digital_illustration", "engraving_color", "1024x1024",
     f"{ICON_SUBJECT}, woodcut engraving print, bold carved ink lines"),
    ("icon_recraft_b.png", "recraft", "digital_illustration", "engraving_color", "1024x1024",
     "square game app icon, a great bonfire burning inside a ring of ancient standing stones at night, a lone armoured "
     "warden kneels before it with a sword planted in the ground, skeletal hands reaching from the darkness at the edge "
     "of the firelight, bold woodcut engraving, extremely high contrast, amber fire against black, no text"),
    ("icon_flux_man.png", "flux", None, None, "1:1",
     f"{ICON_SUBJECT}, dark fantasy key art, painterly, cinematic rim light, dramatic chiaroscuro"),
    ("icon_flux_b.png", "flux", None, None, "1:1",
     "square mobile game app icon, a flaming sword thrust into a cracked stone hearth, the fire forming the shape of a "
     "crown, glowing embers, skeletal silhouettes fading into black mist behind, dark fantasy, painterly, dramatic "
     "chiaroscuro, bold readable shape, amber and black, no text, no letters"),
    ("sigil.png", "recraft", "digital_illustration", "engraving_color", "1024x1024",
     "a round emblem seal centered on pure black: a single tall flame rising from a small stone hearth, encircled by an "
     "unbroken iron ring bound with thorns, symmetrical, flat woodcut engraving, bone white and amber ink on black, "
     "clean edges, no text"),
]


def run(job):
    name, engine, style, substyle, size, prompt = job
    out = OUT / name
    if engine == "recraft":
        generate(out, style, substyle, size, prompt, CODEX_PALETTE)
    else:
        image(out, size, prompt)
    return out


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=5) as pool:
        for path in pool.map(run, JOBS):
            print("  ", path.name, path.stat().st_size // 1024, "KB")


if __name__ == "__main__":
    main()
