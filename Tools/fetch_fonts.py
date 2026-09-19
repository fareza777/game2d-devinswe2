"""Downloads the game's typefaces (SIL Open Font License, free for commercial use) into the project.

Cinzel      - carved Roman capitals, used for the logo, titles and headings.
EB Garamond - a warm old-style book face, used for dialogue and body copy.

The exact static TTF urls change with each font release, so they are resolved through Google's CSS API
instead of being hard-coded.
"""
import re
import urllib.request
from pathlib import Path

OUT_DIR = Path(__file__).resolve().parent.parent / "Assets" / "Oathfire" / "Art" / "Fonts"
USER_AGENT = "Mozilla/5.0 OathfireTools"  # the CSS API serves woff2 to unknown agents, ttf to browsers

FAMILIES = {
    "Cinzel": ("Cinzel:wght@400;700", ["Cinzel-Regular.ttf", "Cinzel-Bold.ttf"]),
    "EBGaramond": ("EB+Garamond:ital,wght@0,400;0,600;1,400", ["EBGaramond-Regular.ttf", "EBGaramond-SemiBold.ttf", "EBGaramond-Italic.ttf"]),
}


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def style_of(data: bytes) -> str:
    """The face's own name for itself, e.g. 'Regular', 'SemiBold' or 'Italic'.

    The CSS API does not return faces in the order they were asked for, so naming files by request order
    silently swaps them — which ships italic body text and nobody notices until it is on screen. Weight
    variants put the weight in the family name and call their subfamily "Regular", so the style has to be
    read off the full name rather than the subfamily.
    """
    from io import BytesIO

    from fontTools.ttLib import TTFont

    names = {record.nameID: str(record) for record in TTFont(BytesIO(data), fontNumber=0, lazy=True)["name"].names}
    full = names.get(4, "")
    base = names.get(16) or names.get(1, "")
    return full.removeprefix(base).strip().replace(" ", "") or "Regular"


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for family, (query, names) in FAMILIES.items():
        css = fetch(f"https://fonts.googleapis.com/css2?family={query}").decode("utf-8")
        urls = re.findall(r"src: url\((https://[^)]+\.ttf)\)", css)
        if len(urls) < len(names):
            raise RuntimeError(f"{family}: expected {len(names)} faces, found {len(urls)}")

        wanted = {name.split("-", 1)[1].removesuffix(".ttf").lower(): name for name in names}
        for url in urls:
            data = fetch(url)
            style = style_of(data)
            name = wanted.get(style.lower())
            if not name:
                print(f"skip {family} {style} (not requested)")
                continue
            target = OUT_DIR / name
            if target.exists() and style_of(target.read_bytes()).lower() == style.lower():
                print(f"skip {name}")
                continue
            target.write_bytes(data)
            print(f"saved {name} = {family} {style} ({target.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
