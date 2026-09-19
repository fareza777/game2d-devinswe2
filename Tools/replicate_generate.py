"""Thin Replicate client for Oathfire: music (MusicGen), images (FLUX) and upscaling.

Replicate complements Recraft: it renders figures and creatures Recraft refuses, and it is the only one of the
four services that makes music. Results are cached by request hash so re-runs never spend credits twice.

Usage:
  python replicate_generate.py music <out.mp3> <seconds> "<prompt>"
  python replicate_generate.py image <out.png> <aspect> "<prompt>"
"""
import hashlib
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from oathfire_tools.env import require

CACHE_DIR = Path(__file__).resolve().parent / "cache" / "replicate"
MUSIC_MODEL = "meta/musicgen"
IMAGE_MODEL = "black-forest-labs/flux-1.1-pro"
POLL_SECONDS = 4
MAX_POLLS = 150


def _request(url: str, body: dict | None = None, method: str = "GET") -> dict:
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode() if body is not None else None,
        headers={
            "Authorization": f"Bearer {require('REPLICATE_API_TOKEN')}",
            "Content-Type": "application/json",
            # Replicate sits behind Cloudflare, which rejects urllib's default agent with error 1010.
            "User-Agent": "OathfireTools/1.0",
        },
        method=method,
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.loads(response.read())


def latest_version(model: str) -> str:
    return _request(f"https://api.replicate.com/v1/models/{model}")["latest_version"]["id"]


def run(model: str, payload: dict) -> str:
    """Starts a prediction, waits for it, and returns the output URL."""
    # /v1/models/<model>/predictions only serves official models, so always go through /v1/predictions.
    prediction = _request("https://api.replicate.com/v1/predictions", {"version": latest_version(model), "input": payload}, "POST")
    for _ in range(MAX_POLLS):
        status = prediction.get("status")
        if status == "succeeded":
            output = prediction["output"]
            return output if isinstance(output, str) else output[0]
        if status in ("failed", "canceled"):
            raise RuntimeError(f"Replicate {model} {status}: {prediction.get('error')}")
        time.sleep(POLL_SECONDS)
        prediction = _request(prediction["urls"]["get"])
    raise TimeoutError(f"Replicate {model} did not finish in time")


def fetch(model: str, payload: dict, out_path: Path, suffix: str) -> Path:
    key = hashlib.sha256(json.dumps({"model": model, "input": payload}, sort_keys=True).encode()).hexdigest()[:16]
    cached = CACHE_DIR / f"{key}{suffix}"
    if not cached.exists():
        CACHE_DIR.mkdir(parents=True, exist_ok=True)
        url = run(model, payload)
        with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "OathfireTools/1.0"}), timeout=300) as response:
            cached.write_bytes(response.read())
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(cached.read_bytes())
    return out_path


def music(out_path: Path, seconds: int, prompt: str) -> Path:
    return fetch(MUSIC_MODEL, {
        "prompt": prompt,
        "duration": seconds,
        "model_version": "stereo-large",
        "output_format": "mp3",
        "normalization_strategy": "loudness",
        "continuation": False,
    }, out_path, ".mp3")


def image(out_path: Path, aspect_ratio: str, prompt: str) -> Path:
    return fetch(IMAGE_MODEL, {
        "prompt": prompt,
        "aspect_ratio": aspect_ratio,
        "output_format": "png",
        "safety_tolerance": 2,
        "prompt_upsampling": False,
    }, out_path, ".png")


if __name__ == "__main__":
    if len(sys.argv) < 5:
        sys.exit(__doc__)
    kind, out, arg, prompt = sys.argv[1], Path(sys.argv[2]), sys.argv[3], sys.argv[4]
    path = music(out, int(arg), prompt) if kind == "music" else image(out, arg, prompt)
    print(f"saved {path}")
