"""Voices the story's spoken scenes, not just the opening cinematic.

Characters who appear only in dialogue have no designed voice yet, so this designs one from a written
brief, turns it into a real voice, and then renders every node of the chosen scripts. Clips are named
dlg_<script>_<node>, which is the name build_content.py looks for — so a line becomes voiced simply by its
audio file existing, with no change to the dialogue source.

Usage: python make_dialogue_vo.py <script_id> [more_script_ids...] [--force]
"""
import json
import sys
import urllib.request
from pathlib import Path

from oathfire_tools.env import require

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
DOCS = PROJECT / "Docs"
VOICE_CACHE = TOOLS / "cache" / "voices"
OUT_DIR = PROJECT / "Assets" / "Oathfire" / "Resources" / "Voice"
MODEL = "eleven_multilingual_v2"
OUTPUT_FORMAT = "mp3_44100_128"
MIN_PREVIEW_CHARS = 100

# Briefs for the people who speak only in dialogue. Written the way a casting note would be.
BRIEFS = {
    "crane": "A man who has been dead for years and is still talking. Sixties, northern, once a soldier and "
             "then a village warden. Dry, patient, faintly amused at being feared. The voice is worn thin and "
             "quiet — never a growl, never theatrical, like someone who has had a long time to choose his words.",
    "tobias": "A government rider in his early thirties. Educated, clipped, careful. Speaks like a man reading "
              "a report he does not entirely believe, with the tiredness of someone who has delivered bad news "
              "to four valleys this month. Warm underneath, but he keeps it behind the procedure.",
    "maren": "An inspector in her forties. Cool, precise, unhurried, used to being obeyed and used to being "
             "disliked for it. A civil servant's voice, not a soldier's, with a hairline crack of doubt in it.",
    "sabel": "A travelling packman in his fifties. Quick, cheerful, transactional, a trader's patter worn "
             "smooth by a thousand repetitions. Friendly without being warm.",
    "refugee": "A woman in her late twenties who has walked four days without sleeping. Exhausted, frightened, "
               "holding herself together by talking steadily. Not hysterical — carefully, dangerously calm.",
    "surveyor": "A Protectorate surveyor in his forties. Flat, bureaucratic, faintly apologetic. The voice of a "
                "man who measures things for a living and knows what is done with his measurements.",
}


def post(url: str, body: dict) -> bytes:
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode(),
        headers={"xi-api-key": require("ELEVENLABS_API_KEY"),
                 "Content-Type": "application/json", "User-Agent": "OathfireTools/1.0"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=300) as response:
        return response.read()


def speaker_lines(script: dict, speaker: str) -> str:
    """A sample of the character's own words, so the audition is judged on what they actually say."""
    said = [node["text"]["en"] for node in script["nodes"] if node.get("speaker") == speaker]
    text = " ".join(said)
    while len(text) < MIN_PREVIEW_CHARS and said:
        text = f"{text} {said[0]}"
    return text[:1000]


def ensure_voice(speaker: str, sample: str) -> str:
    """Designs and creates the character's voice once; later runs reuse the saved id."""
    voices_path = VOICE_CACHE / "voices.json"
    voices = json.loads(voices_path.read_text(encoding="utf-8")) if voices_path.exists() else {}
    if speaker in voices:
        return voices[speaker]

    brief = BRIEFS.get(speaker)
    if not brief:
        raise SystemExit(f"no casting brief for '{speaker}' — add one to BRIEFS first")

    designed = json.loads(post("https://api.elevenlabs.io/v1/text-to-voice/design",
                               {"voice_description": brief, "text": sample}))
    previews = designed.get("previews") or []
    if not previews:
        raise SystemExit(f"voice design returned no previews for {speaker}")

    # Pick the most deliberate take. Delivery speed is the one quality that can be judged without ears,
    # and every one of these characters is written to speak slowly.
    def duration(preview: dict) -> float:
        return float(preview.get("duration_secs") or preview.get("duration") or 0)

    best = max(previews, key=duration)
    created = json.loads(post("https://api.elevenlabs.io/v1/text-to-voice/create-voice-from-preview", {
        "voice_name": f"Oathfire {speaker.capitalize()}",
        "voice_description": brief,
        "generated_voice_id": best["generated_voice_id"],
    }))

    VOICE_CACHE.mkdir(parents=True, exist_ok=True)
    voices[speaker] = created["voice_id"]
    voices.setdefault("_selection", {})[speaker] = f"{speaker}#slowest({duration(best):.1f}s)"
    voices_path.write_text(json.dumps(voices, indent=2), encoding="utf-8")
    print(f"voice created: {speaker} (chose the slowest of {len(previews)} takes, {duration(best):.1f}s)")
    return created["voice_id"]


def render(voice_id: str, text: str, out: Path) -> None:
    body = {
        "text": text,
        "model_id": MODEL,
        "voice_settings": {"stability": 0.45, "similarity_boost": 0.8, "style": 0.35, "use_speaker_boost": True},
    }
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(post(f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format={OUTPUT_FORMAT}", body))
    print(f"  {out.name}  ({len(text)} chars)")


def main() -> None:
    script_ids = [arg for arg in sys.argv[1:] if not arg.startswith("--")]
    force = "--force" in sys.argv
    if not script_ids:
        raise SystemExit(__doc__)

    for script_id in script_ids:
        path = DOCS / f"dialogue_{script_id}.json"
        if not path.exists():
            raise SystemExit(f"missing {path}")
        script = json.loads(path.read_text(encoding="utf-8"))
        print(f"{script_id}: {len(script['nodes'])} nodes")

        voices: dict[str, str] = {}
        for node in script["nodes"]:
            speaker = node.get("speaker")
            if not speaker:
                continue
            out = OUT_DIR / f"dlg_{script_id}_{node['id']}.mp3"
            if out.exists() and not force:
                print(f"  skip {out.name}")
                continue
            if speaker not in voices:
                voices[speaker] = ensure_voice(speaker, speaker_lines(script, speaker))
            render(voices[speaker], node["text"]["en"], out)


if __name__ == "__main__":
    main()
