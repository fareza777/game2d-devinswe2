"""Generates art with Recraft v3 using Oathfire's locked palette.

Usage: python recraft_generate.py <out.png> <style> <substyle|-> <size> "<prompt>"
Outputs are cached by request hash so re-runs never spend credits twice.
"""
import hashlib
import json
import shutil
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from oathfire_tools.env import require

API_URL = "https://external.api.recraft.ai/v1/images/generations"
CACHE_DIR = Path(__file__).resolve().parent / "cache" / "recraft"

# Oathfire palette: grave moss, loam, bone, ember, dusk teal, rust.
PALETTE = [(20, 24, 19), (58, 66, 48), (220, 212, 192), (201, 147, 59), (94, 139, 131), (154, 70, 50)]
# Codex woodcut print: ink, bone paper, ember, moss. Used for cinematic panels.
CODEX_PALETTE = [(18, 20, 17), (214, 204, 182), (201, 140, 52), (52, 64, 50)]


TRANSIENT_HTTP_CODES = {429, 500, 502, 503, 504}
MAX_ATTEMPTS = 4


def _post_with_retry(request: urllib.request.Request) -> dict:
    """Recraft occasionally returns 502/503 under load; failed requests are not billed, so retry with backoff."""
    for attempt in range(1, MAX_ATTEMPTS + 1):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                return json.loads(response.read())
        except urllib.error.HTTPError as error:
            if error.code not in TRANSIENT_HTTP_CODES or attempt == MAX_ATTEMPTS:
                detail = error.read().decode("utf-8", "replace")[:300]
                raise RuntimeError(f"Recraft HTTP {error.code}: {detail}") from error
            time.sleep(5 * attempt)
    raise RuntimeError("unreachable")


def generate(out_path: Path, style: str, substyle: str | None, size: str, prompt: str, palette=None) -> Path:
    body = {
        "prompt": prompt,
        "model": "recraftv3",
        "style": style,
        "size": size,
        "n": 1,
        "response_format": "url",
        "controls": {"colors": [{"rgb": list(rgb)} for rgb in (palette or PALETTE)]},
    }
    if substyle:
        body["substyle"] = substyle

    key = hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()[:16]
    cached = CACHE_DIR / f"{key}.png"
    if not cached.exists():
        CACHE_DIR.mkdir(parents=True, exist_ok=True)
        request = urllib.request.Request(
            API_URL,
            data=json.dumps(body).encode(),
            headers={
                "Authorization": f"Bearer {require('RECRAFT_API_TOKEN')}",
                "Content-Type": "application/json",
                "User-Agent": "OathfireTools/1.0",
            },
            method="POST",
        )
        result = _post_with_retry(request)
        image_url = result["data"][0]["url"]
        with urllib.request.urlopen(urllib.request.Request(image_url, headers={"User-Agent": "OathfireTools/1.0"}), timeout=180) as image:
            cached.write_bytes(image.read())
        (CACHE_DIR / f"{key}.json").write_text(json.dumps(body, indent=2), encoding="utf-8")

    out_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(cached, out_path)
    return out_path


if __name__ == "__main__":
    if len(sys.argv) != 6:
        sys.exit(__doc__)
    _, out, style, substyle, size, prompt = sys.argv
    palette = CODEX_PALETTE if style.endswith("+codex") else None
    style = style.replace("+codex", "")
    path = generate(Path(out), style, None if substyle == "-" else substyle, size, prompt, palette)
    print(f"saved {path}")
