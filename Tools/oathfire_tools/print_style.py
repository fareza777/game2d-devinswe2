"""Unifies generated art into Oathfire's three-ink woodcut look.

Generated images drift in style no matter how the prompt is written, so every portrait passes through this
deterministic pass: trim any paper margin, map luminance onto the locked palette, add wood grain and a vignette.
"""
from __future__ import annotations

import math
import random
from PIL import Image, ImageChops, ImageEnhance, ImageFilter

INK = (18, 20, 17)
MOSS = (54, 68, 54)
BONE = (214, 204, 182)
PALETTE = (INK, MOSS, BONE)
GRAIN_SEED = 7


def trim_margin(image: Image.Image, tolerance: int = 24) -> Image.Image:
    """Drops a light paper border some models add around the print."""
    grey = image.convert("L")
    corner = grey.getpixel((2, 2))
    if corner < 200:
        return image
    mask = grey.point(lambda value: 255 if abs(value - corner) > tolerance else 0)
    box = mask.getbbox()
    return image.crop(box) if box else image


def posterize_to_palette(image: Image.Image) -> Image.Image:
    grey = ImageEnhance.Contrast(image.convert("L")).enhance(1.35)
    width, height = grey.size
    out = Image.new("RGB", (width, height))
    source = grey.load()
    target = out.load()
    for y in range(height):
        for x in range(width):
            value = source[x, y]
            if value < 86:
                target[x, y] = PALETTE[0]
            elif value < 168:
                target[x, y] = PALETTE[1]
            else:
                target[x, y] = PALETTE[2]
    return out


def add_wood_grain(image: Image.Image, strength: float = 0.14) -> Image.Image:
    width, height = image.size
    grain = Image.new("L", (width, height))
    pixels = grain.load()
    random.seed(GRAIN_SEED)
    offsets = [random.uniform(0, math.tau) for _ in range(height)]
    for y in range(height):
        phase = offsets[y]
        for x in range(width):
            wave = math.sin(x * 0.06 + phase) + math.sin(x * 0.011 + phase * 0.5)
            pixels[x, y] = int(128 + wave * 42)
    grain = grain.filter(ImageFilter.GaussianBlur(0.6))
    return Image.blend(image, ImageChops.overlay(image, grain.convert("RGB")), strength)


def add_vignette(image: Image.Image, strength: float = 0.55) -> Image.Image:
    width, height = image.size
    mask = Image.new("L", (width, height))
    pixels = mask.load()
    cx, cy = width / 2, height / 2
    radius = math.hypot(cx, cy)
    for y in range(height):
        for x in range(width):
            distance = math.hypot(x - cx, y - cy) / radius
            pixels[x, y] = int(255 * max(0.0, 1.0 - max(0.0, distance - 0.45) * strength * 2.4))
    dark = Image.new("RGB", (width, height), INK)
    return Image.composite(image, dark, mask)


def apply(image: Image.Image) -> Image.Image:
    image = trim_margin(image.convert("RGB"))
    image = posterize_to_palette(image)
    image = add_wood_grain(image)
    return add_vignette(image)
