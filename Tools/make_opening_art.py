"""Generates the opening cinematic panels from Docs/opening_cinematic.json.

Recraft's colour controls make every palette colour occupy a large share of the image, so panels whose only warm
light should be a fire use the "night" palette (no amber); the fire glow is layered on in Unity instead.
The title card is composed in-engine (logo, hearth, animated flame), not generated.

Usage: python make_opening_art.py [panel_id ...] [--force]
"""
import json
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from recraft_generate import CODEX_PALETTE, generate

PROJECT = Path(__file__).resolve().parent.parent
SCRIPT = PROJECT / "Docs" / "opening_cinematic.json"
OUT_DIR = PROJECT / "Assets" / "Oathfire" / "Art" / "Cinematic" / "Opening"
PANEL_SIZE = "1024x1820"

# Ink black, bone paper, moss, deep dusk teal: no warm colour for the model to spread around.
NIGHT_PALETTE = [(18, 20, 17), (214, 204, 182), (52, 64, 50), (40, 66, 64)]
PALETTES = {"codex": CODEX_PALETTE, "night": NIGHT_PALETTE}


def job(panel: dict, style: str) -> str:
    out = OUT_DIR / f"{panel['id']}.png"
    if out.exists() and "--force" not in sys.argv:
        return f"skip {out.name}"
    palette = PALETTES[panel.get("palette", "codex")]
    generate(out, "digital_illustration", "engraving_color", PANEL_SIZE, f"{panel['prompt']} {style}", palette)
    return f"saved {out.name}"


def main() -> None:
    data = json.loads(SCRIPT.read_text(encoding="utf-8"))
    only = [arg for arg in sys.argv[1:] if not arg.startswith("--")]
    panels = [p for p in data["panels"] if not only or p["id"] in only]
    with ThreadPoolExecutor(max_workers=4) as pool:
        for result in pool.map(lambda p: job(p, data["artStyle"]), panels):
            print(result)


if __name__ == "__main__":
    main()
