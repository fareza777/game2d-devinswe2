"""Renders Oathfire's sound: effects and ambience with ElevenLabs, location music with MusicGen (Replicate).

Every request is cached by its content, so re-running only pays for sounds whose description changed.
Clips land in Assets/Oathfire/Resources/{Sfx,Ambience,Music} and are loaded by name at runtime.

Usage: python make_audio.py [sfx|music|all]
"""
import hashlib
import json
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from oathfire_tools.env import require
from replicate_generate import music

TOOLS = Path(__file__).resolve().parent
RESOURCES = TOOLS.parent / "Assets" / "Oathfire" / "Resources"
CACHE = TOOLS / "cache" / "sfx"

# name: (seconds, description). Short, dry and specific: these play dozens of times per session.
EFFECTS = {
    "ui_tap": (0.5, "a single soft wooden tap, like a fingertip on an oak table, short user interface click, no reverb"),
    "ui_open": (1.0, "a heavy leather-bound book opening with a parchment page turn"),
    "ui_close": (0.7, "a leather-bound book closing softly with a muffled thud"),
    "sword_swing": (0.5, "a fast steel sword swing whoosh through the air, close, dry"),
    "hit": (0.5, "a sword slash landing on a leather-armoured body, a meaty impact thud"),
    "enemy_death": (1.4, "a feral creature's dying snarl followed by a body falling onto dirt"),
    "player_hurt": (0.6, "a young man's short sharp grunt of pain in combat"),
    "footstep": (0.5, "a single light leather boot footstep on a packed dirt path"),
    "skill_dash": (0.7, "a quick lunging whoosh with a cloak flutter"),
    "skill_area": (1.5, "a burst of fire exploding outward in a ring, roaring whoosh with embers crackling"),
    "gather_wood": (1.2, "two hard axe chops into a log of wood"),
    "gather_stone": (1.0, "an iron pick striking rock twice, chips of stone falling"),
    "build": (1.6, "hammering nails into wooden planks, a short burst of carpentry"),
    "hearth_light": (2.2, "a large fire catching with a deep whoosh, then settling into a crackle"),
    "coin": (0.8, "a small handful of silver coins clinking into a leather purse"),
    "item_pickup": (0.6, "picking up a small object and tucking it into a leather satchel"),
    "equip": (0.8, "buckling on a leather and steel armour piece, straps and a metal clink"),
    "quest_new": (1.2, "a quill scratching quickly across parchment, then a soft tap"),
    "quest_complete": (2.5, "a short proud medieval fanfare on two brass horns over a single war drum"),
    "level_up": (2.0, "a rising warm shimmer of bright chimes swelling to a gentle glow"),
    "night_horn": (3.0, "a distant war horn blown three times at night, ominous, echoing over hills"),
    "dawn": (4.0, "morning birdsong over a quiet village with a rooster crowing in the distance"),
    "ember": (1.5, "soft embers crackling with a low warm whoosh of rising fire"),
    "travel": (2.0, "a heavy wooden gate creaking open followed by boots on gravel"),
    "event_alert": (1.5, "a single dramatic low drum hit with a tense rising string sting"),
    "enemy_alert": (1.0, "a hostile growl and the scrape of a blade being drawn"),
    "potion": (1.0, "uncorking a small glass bottle and drinking a quick gulp"),
    "denied": (0.5, "a dull muted wooden knock, short, meaning no"),
}

# Ambience beds loop quietly beneath the music, one per kind of place.
AMBIENCE = {
    "village_day": (22.0, "peaceful medieval village ambience: birdsong, wind in trees, distant chickens, faint voices and a far-off hammer, no music"),
    "night": (22.0, "night in the countryside: crickets, an owl in the distance, low wind through grass, no music"),
    "road": (22.0, "open countryside road: steady wind over fields, larks overhead, grass rustling, no music"),
    "city": (22.0, "busy medieval walled city: crowd murmur in a market square, cart wheels on cobbles, distant smithy, no music"),
    "ravine": (22.0, "a deep rocky ravine: wind moaning between cliffs, a campfire crackling nearby, a crow calling, a wooden palisade creaking, no music"),
}

# The codex look has a sound: small ensembles of old instruments, nothing synthetic, melancholy before heroic.
STYLE = "no vocals, acoustic medieval ensemble, cinematic, high quality recording, seamless loop"
MUSIC = {
    "village_day": (60, f"gentle melancholic medieval folk theme for a small village rebuilding after a plague, fingerpicked lute, wooden recorder melody, soft frame drum, warm and hopeful, 80 bpm, {STYLE}"),
    "night_siege": (60, f"tense dark orchestral battle music for defending a campfire against monsters at night, pounding war drums, low cellos ostinato, horn calls, urgent, 120 bpm, {STYLE}"),
    "road_travel": (60, f"adventurous travelling theme on a lonely trade road, hurdy-gurdy and fiddle melody over a walking bodhran rhythm, wide open fields, 100 bpm, {STYLE}"),
    "city_greymarch": (60, f"grand but weary walled city theme, low brass, harp arpeggios, hammered dulcimer, a sense of politics and old stone, 90 bpm, {STYLE}"),
    "hollow_camp": (60, f"tense outlaw camp theme in a dark ravine, low plucked cello and muted frame drum, sparse uneasy fiddle, creeping danger, 85 bpm, {STYLE}"),
    "battle": (45, f"fast fierce medieval skirmish music, driving taiko and snare drums, sharp fiddle runs, brass stabs, 140 bpm, {STYLE}"),
}


def sound(out: Path, seconds: float, text: str, loop: bool = False) -> Path:
    body = {"text": text, "duration_seconds": seconds, "prompt_influence": 0.55, "model_id": "eleven_text_to_sound_v2"}
    if loop:
        body["loop"] = True
    key = hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()[:16]
    cached = CACHE / f"{key}.mp3"
    if not cached.exists():
        request = urllib.request.Request(
            "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128",
            data=json.dumps(body).encode(),
            headers={"xi-api-key": require("ELEVENLABS_API_KEY"), "Content-Type": "application/json", "User-Agent": "OathfireTools/1.0"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                data = response.read()
        except urllib.error.HTTPError as error:
            raise RuntimeError(f"{out.name}: HTTP {error.code} {error.read()[:300]!r}") from None
        CACHE.mkdir(parents=True, exist_ok=True)
        cached.write_bytes(data)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(cached.read_bytes())
    return out


def make_effects() -> None:
    jobs = [(RESOURCES / "Sfx" / f"{name}.mp3", seconds, text, False) for name, (seconds, text) in EFFECTS.items()]
    jobs += [(RESOURCES / "Ambience" / f"{name}.mp3", seconds, text, True) for name, (seconds, text) in AMBIENCE.items()]
    failures = []

    def render(job):
        try:
            path = sound(*job)
            print(f"  {path.parent.name}/{path.name} {path.stat().st_size // 1024} KB")
        except Exception as error:  # report every failure, keep rendering the rest
            failures.append(str(error))

    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(render, jobs))
    if failures:
        raise SystemExit("sound effects failed:\n" + "\n".join(failures))


def make_music() -> None:
    def render(item):
        name, (seconds, prompt) = item
        path = music(RESOURCES / "Music" / f"{name}.mp3", seconds, prompt)
        print(f"  Music/{path.name} {path.stat().st_size // 1024} KB")

    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(render, MUSIC.items()))


def main() -> None:
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("sfx", "all"):
        make_effects()
    if what in ("music", "all"):
        make_music()


if __name__ == "__main__":
    main()
