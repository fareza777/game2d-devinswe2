"""Renders every voiced line of a cinematic with ElevenLabs.

Voices come from the designs in cache/voices/previews.json. Which candidate is used per character is set in
cache/voices/chosen.json ({"brann": 1, ...}); until the human picks, candidate 1 is used as a placeholder.

Usage: python make_voice_over.py [--force] [--script Docs/opening_cinematic.json]
"""
import json
import sys
import urllib.request
from pathlib import Path

from oathfire_tools.env import require

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
_script_arg = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--script" and i + 1 < len(sys.argv)),
                   "Docs/opening_cinematic.json")
SCRIPT = PROJECT / _script_arg
VOICE_CACHE = TOOLS / "cache" / "voices"
OUT_DIR = PROJECT / "Assets" / "Oathfire" / "Resources" / "Voice"
MODEL = "eleven_multilingual_v2"
OUTPUT_FORMAT = "mp3_44100_128"


def post(url: str, body: dict) -> bytes:
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode(),
        headers={"xi-api-key": require("ELEVENLABS_API_KEY"), "Content-Type": "application/json", "User-Agent": "OathfireTools/1.0"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=300) as response:
        return response.read()


def ensure_voices() -> dict[str, str]:
    """Turns the chosen previews into real voices once, and remembers their ids."""
    previews = json.loads((VOICE_CACHE / "previews.json").read_text(encoding="utf-8"))
    chosen_path = VOICE_CACHE / "chosen.json"
    chosen = json.loads(chosen_path.read_text(encoding="utf-8")) if chosen_path.exists() else {}
    voices_path = VOICE_CACHE / "voices.json"
    voices = json.loads(voices_path.read_text(encoding="utf-8")) if voices_path.exists() else {}

    for speaker, entry in previews.items():
        index = int(chosen.get(speaker, 1))
        key = f"{speaker}#{index}"
        if voices.get("_selection", {}).get(speaker) == key and speaker in voices:
            continue
        preview = entry["previews"][index - 1]
        payload = {
            "voice_name": f"Oathfire {speaker.capitalize()}",
            "voice_description": entry["description"],
            "generated_voice_id": preview["generated_voice_id"],
        }
        result = json.loads(post("https://api.elevenlabs.io/v1/text-to-voice/create-voice-from-preview", payload))
        voices[speaker] = result["voice_id"]
        voices.setdefault("_selection", {})[speaker] = key
        voices_path.write_text(json.dumps(voices, indent=2), encoding="utf-8")
        print(f"voice created: {speaker} -> candidate {index}")
    return voices


def render(voice_id: str, text: str, direction: str, out: Path) -> None:
    body = {
        "text": text,
        "model_id": MODEL,
        "voice_settings": {"stability": 0.45, "similarity_boost": 0.8, "style": 0.35, "use_speaker_boost": True},
    }
    audio = post(f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format={OUTPUT_FORMAT}", body)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(audio)
    print(f"{out.name}  ({len(text)} chars, {direction})")


def main() -> None:
    data = json.loads(SCRIPT.read_text(encoding="utf-8"))
    voices = ensure_voices()
    force = "--force" in sys.argv

    for panel in data["panels"]:
        for index, line in enumerate(panel["lines"], start=1):
            clip_id = f"{panel['id']}_{index}_{line['speaker']}"
            out = OUT_DIR / f"{clip_id}.mp3"
            line["voiceClip"] = clip_id
            if out.exists() and not force:
                print(f"skip {out.name}")
                continue
            render(voices[line["speaker"]], line["en"], line.get("direction", ""), out)

    SCRIPT.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
