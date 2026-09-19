"""Creates ElevenLabs voice-design previews for each character so a human can audition them.

Usage: python voice_design.py
Writes Tools/cache/voices/<character>_<n>.mp3 plus previews.json (generated_voice_id per preview).
"""
import base64
import json
import urllib.error
import urllib.request
from pathlib import Path

from oathfire_tools.env import require

TOOLS = Path(__file__).resolve().parent
SCRIPT = TOOLS.parent / "Docs" / "opening_cinematic.json"
OUT_DIR = TOOLS / "cache" / "voices"
DESIGN_URL = "https://api.elevenlabs.io/v1/text-to-voice/design"
LEGACY_URL = "https://api.elevenlabs.io/v1/text-to-voice/create-previews"
MIN_PREVIEW_CHARS = 100


def post(url: str, body: dict) -> dict:
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode(),
        headers={"xi-api-key": require("ELEVENLABS_API_KEY"), "Content-Type": "application/json", "User-Agent": "OathfireTools/1.0"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=240) as response:
        return json.loads(response.read())


def preview_text(data: dict, speaker: str) -> str:
    lines = [line["en"] for panel in data["panels"] for line in panel["lines"] if line["speaker"] == speaker]
    text = " ".join(lines)
    while len(text) < MIN_PREVIEW_CHARS:
        text = f"{text} {lines[0]}"
    return text[:1000]


def design(description: str, text: str) -> list[dict]:
    try:
        result = post(DESIGN_URL, {"voice_description": description, "text": text, "auto_generate_text": False, "model_id": "eleven_multilingual_ttv_v2"})
    except urllib.error.HTTPError as error:
        if error.code not in (404, 405, 422):
            raise RuntimeError(f"ElevenLabs design HTTP {error.code}: {error.read().decode('utf-8', 'replace')[:300]}") from error
        result = post(LEGACY_URL, {"voice_description": description, "text": text})
    return result["previews"]


def main() -> None:
    data = json.loads(SCRIPT.read_text(encoding="utf-8"))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    manifest_path = OUT_DIR / "previews.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}

    for speaker, description in data["voices"].items():
        if speaker in manifest:
            print(f"skip {speaker} (already designed)")
            continue
        text = preview_text(data, speaker)
        previews = design(description, text)
        entries = []
        for index, preview in enumerate(previews, start=1):
            file = OUT_DIR / f"{speaker}_{index}.mp3"
            file.write_bytes(base64.b64decode(preview["audio_base_64"]))
            entries.append({"file": file.name, "generated_voice_id": preview["generated_voice_id"], "duration": preview.get("duration_secs")})
        manifest[speaker] = {"description": description, "text": text, "previews": entries}
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"{speaker}: {len(entries)} previews")


if __name__ == "__main__":
    main()
