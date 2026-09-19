"""Composes the OATHFIRE logo from the real typeface, rather than trusting an image model with letters.

The wordmark is built in layers: a deep carve shadow, a hot ember gradient fill, a thin bone rim light,
soot speckle, and a burnt underglow. Output is a transparent PNG the title screen drops straight in.
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

PROJECT = Path(__file__).resolve().parent.parent
FONT = PROJECT / "Assets" / "Oathfire" / "Art" / "Fonts" / "Cinzel-Bold.ttf"
OUT = PROJECT / "Assets" / "Oathfire" / "Resources" / "UI" / "logo_oathfire.png"

WIDTH, HEIGHT = 1600, 620
WORD = "OATHFIRE"
TRACKING = 26          # extra pixels between letters; carved capitals need air
EMBER_TOP = (255, 214, 148)
EMBER_MID = (216, 132, 40)
EMBER_LOW = (128, 44, 18)
BONE = (236, 228, 208)
SEED = 11


def letter_mask(font: ImageFont.FreeTypeFont, size: tuple[int, int], baseline_y: int) -> Image.Image:
    """Draws the word with manual tracking and returns it as a mask."""
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    widths = [draw.textlength(ch, font=font) for ch in WORD]
    total = sum(widths) + TRACKING * (len(WORD) - 1)
    x = (size[0] - total) / 2
    for ch, width in zip(WORD, widths):
        draw.text((x, baseline_y), ch, font=font, fill=255, anchor="ls")
        x += width + TRACKING
    return mask


def vertical_gradient(size: tuple[int, int], stops: list[tuple[float, tuple[int, int, int]]]) -> Image.Image:
    gradient = Image.new("RGB", size)
    pixels = gradient.load()
    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        lower = stops[0]
        upper = stops[-1]
        for index in range(len(stops) - 1):
            if stops[index][0] <= t <= stops[index + 1][0]:
                lower, upper = stops[index], stops[index + 1]
                break
        span = max(1e-6, upper[0] - lower[0])
        k = (t - lower[0]) / span
        color = tuple(int(lower[1][c] + (upper[1][c] - lower[1][c]) * k) for c in range(3))
        for x in range(size[0]):
            pixels[x, y] = color
    return gradient


def soot_texture(size: tuple[int, int]) -> Image.Image:
    random.seed(SEED)
    noise = Image.new("L", (size[0] // 3, size[1] // 3))
    pixels = noise.load()
    for y in range(noise.height):
        for x in range(noise.width):
            pixels[x, y] = random.randint(90, 255)
    return noise.resize(size, Image.BICUBIC).filter(ImageFilter.GaussianBlur(1.2))


def main() -> None:
    if not FONT.exists():
        raise SystemExit(f"Missing font: {FONT}. Run fetch_fonts.py first.")

    font = ImageFont.truetype(str(FONT), 210)
    canvas = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    baseline = 330
    mask = letter_mask(font, (WIDTH, HEIGHT), baseline)

    # Burnt halo behind the letters so the logo separates from any background art.
    halo = mask.filter(ImageFilter.GaussianBlur(38)).point(lambda v: int(v * 0.8))
    canvas.paste(Image.new("RGBA", canvas.size, (92, 28, 8, 255)), (0, 0), halo)

    # A dark contour around every letter, so the wordmark holds against a bright valley as well as a dark sky.
    contour = mask.filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.GaussianBlur(2))
    canvas.paste(Image.new("RGBA", canvas.size, (8, 7, 5, 255)), (0, 0), contour)

    # Carved shadow: the same letters, offset down and blurred, in near-black.
    carve = mask.filter(ImageFilter.GaussianBlur(4))
    shadow = Image.new("RGBA", canvas.size, (10, 8, 6, 255))
    canvas.paste(shadow, (0, 8), carve)

    # Ember body.
    body = vertical_gradient((WIDTH, HEIGHT), [(0.0, EMBER_TOP), (0.45, EMBER_MID), (1.0, EMBER_LOW)]).convert("RGBA")
    body.putalpha(mask)
    soot = soot_texture((WIDTH, HEIGHT)).convert("RGBA")
    soot.putalpha(mask)
    body = Image.blend(body, ImageChops.multiply(body, soot), 0.28)
    canvas.alpha_composite(body)

    # Rim light along the top edge of each letter: mask minus mask shifted down.
    shifted = ImageChops.offset(mask, 0, 7)
    rim = ImageChops.subtract(mask, shifted).filter(ImageFilter.GaussianBlur(1.1))
    rim_layer = Image.new("RGBA", canvas.size, BONE + (255,))
    rim_layer.putalpha(rim.point(lambda v: int(v * 0.85)))
    canvas.alpha_composite(rim_layer)

    # A single ember spark sits in the counter of the O, tying the mark to the hearth.
    spark = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    spark_draw = ImageDraw.Draw(spark)
    for radius, alpha in ((46, 60), (26, 120), (12, 220)):
        spark_draw.ellipse((205 - radius, 250 - radius, 205 + radius, 250 + radius), fill=(255, 176, 74, alpha))
    canvas.alpha_composite(spark.filter(ImageFilter.GaussianBlur(3)))

    # Subtitle: large enough to read on a phone, bone letters with an ink edge on a soft dark bed, and the rule
    # set either side of the words instead of struck through beneath them.
    small = ImageFont.truetype(str(FONT), 60)
    text = "CHAPTER I  ·  THE FIRST FIRE"
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    tracking = 9
    widths = [probe.textlength(ch, font=small) for ch in text]
    width = sum(widths) + tracking * (len(text) - 1)
    top = 440
    bed_mask = Image.new("L", canvas.size, 0)
    ImageDraw.Draw(bed_mask).rounded_rectangle(
        ((WIDTH - width) / 2 - 70, top - 16, (WIDTH + width) / 2 + 70, top + 84), radius=40, fill=200)
    canvas.paste(Image.new("RGBA", canvas.size, (8, 8, 6, 255)), (0, 0), bed_mask.filter(ImageFilter.GaussianBlur(22)))

    sub = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sub_draw = ImageDraw.Draw(sub)
    x = (WIDTH - width) / 2
    for ch, w in zip(text, widths):
        sub_draw.text((x, top), ch, font=small, fill=(242, 232, 206, 255), stroke_width=4, stroke_fill=(10, 9, 7, 255))
        x += w + tracking
    rule_y = top + 38
    for side in (-1, 1):
        inner = WIDTH / 2 + side * (width / 2 + 34)
        outer = WIDTH / 2 + side * (width / 2 + 170)
        sub_draw.line((inner, rule_y, outer, rule_y), fill=(214, 150, 64, 230), width=4)
        sub_draw.ellipse((inner - 7, rule_y - 7, inner + 7, rule_y + 7), fill=(236, 170, 80, 255))
    canvas.alpha_composite(sub)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(OUT)
    print(f"saved {OUT} ({canvas.size[0]}x{canvas.size[1]})")

    preview = Image.new("RGB", canvas.size, (18, 22, 18))
    preview.paste(canvas, (0, 0), canvas)
    preview.save(Path(__file__).resolve().parent / "cache" / "logo_preview.png")


if __name__ == "__main__":
    main()
