"""Generates dialogue portraits with Replicate FLUX, in the same woodcut codex style as the cinematic.

Recraft refuses or mangles specific figures, so portraits go through FLUX with the style described in the
prompt instead of palette controls. Output: Assets/Oathfire/Resources/Portraits/<speaker>.png (square bust).

Usage: python make_portraits.py [speaker ...] [--force]
"""
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from PIL import Image

from oathfire_tools.print_style import apply as apply_print_style
from replicate_generate import image

PROJECT = Path(__file__).resolve().parent.parent
OUT_DIR = PROJECT / "Assets" / "Oathfire" / "Resources" / "Portraits"
RAW_DIR = Path(__file__).resolve().parent / "cache" / "portraits"

# One locked treatment so every portrait reads as the same printed set.
STYLE = (
    "woodcut linocut print portrait from one matching series, carved gouge strokes, thick black ink, "
    "bone-white highlights, muted moss green mid-tone, three inks only, flat with no soft shading, "
    "head and shoulders portrait, face centred and filling the frame, fully clothed, solid near-black background, "
    "edge-to-edge full bleed with no white margin, no border, no frame, no text, no signature"
)

PORTRAITS = {
    "brann": "An old gravedigger in his late sixties, weathered lined face, grey stubble, deep-set tired eyes, "
             "battered wide-brimmed hat, worn leather coat with a shovel strap, dry knowing half-smile",
    "kael": "A man knight in his mid thirties, short dark hair, close-cropped dark beard, a scar through one "
              "eyebrow, weathered stubborn face, plain dented breastplate over a travel-stained gambeson, "
              "steady tired eyes of someone who has lost everything and kept walking",
    "isolde": "A queen in her forties on the night of her death, pale drawn face, braided dark hair, "
              "a crown of woven roots cracking apart above her brow, calm exhausted eyes, heavy embroidered collar",
    "aurel": "A lord protector in his fifties, sleek silver-streaked hair, neat pointed beard, cold courteous smile, "
             "high fur-trimmed collar, a scaled lion brooch at his throat, aristocratic and dangerous",
    "maren": "A young priestess inquisitor of a fire church, severe short auburn hair, freckles, doubting earnest eyes, "
             "high-collared robe with a flame emblem, small brass censer chain at her belt",
    "galen": "A young knight in his twenties, honest open face, close-cropped fair hair, faint sunburn, "
             "polished breastplate with a scaled lion crest, slightly uncertain expression",
    "tobias": "A lean young poacher, unkempt dark hair, sharp watchful eyes, hood down, quiver strap across chest, "
              "smudged face, defiant and hungry",
    "iskra": "A necromancer apothecary woman in her thirties, shaved temple and long braid, ash markings on her cheekbones, "
             "hooded robe hung with small bone charms and glass vials, unsettling calm gaze",
    "crane": "An ancient necromancer shepherd, gaunt hollow face half hidden by a deep grey hood, long white beard, "
             "eyes like cold lanterns, tattered warden's cloak, a shepherd's crook of bound bones",
}


def job(speaker: str, description: str) -> str:
    out = OUT_DIR / f"{speaker}.png"
    if out.exists() and "--force" not in sys.argv:
        return f"skip {speaker}"

    raw = RAW_DIR / f"{speaker}.png"
    image(raw, "1:1", f"{description}. {STYLE}")

    portrait = apply_print_style(Image.open(raw))
    portrait.thumbnail((512, 512))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    portrait.save(out)
    return f"saved {speaker}"


def main() -> None:
    wanted = [arg for arg in sys.argv[1:] if not arg.startswith("--")] or list(PORTRAITS)
    with ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(lambda s: job(s, PORTRAITS[s]), wanted):
            print(result)


if __name__ == "__main__":
    main()
