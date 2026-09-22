"""Voices the story's spoken scenes, not just the opening cinematic.

Characters who appear only in dialogue have no designed voice yet, so this designs one from a written
brief, turns it into a real voice, and then renders every node of the chosen scripts. Clips are named
dlg_<script>_<node>, which is the name build_content.py looks for — so a line becomes voiced simply by its
audio file existing, with no change to the dialogue source.

Usage: python make_dialogue_vo.py <script_id> [more_script_ids...] [--force]
"""
import json
import sys
import time
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
    "brann": "The village's oldest warden's-helper. Seventies, gravel in the throat, unhurried. He buried the "
             "dead for fifty years and the dead stopped staying buried — he tells you this the way other men "
             "tell you the weather. Dry, steady, fond of you in a way he would deny.",
    "gethin": "A young gravedigger, twenty-something, with a count he cannot stop keeping. Anxious, quick, "
              "breath held between sentences. Grieving and trying to sound practical about it.",
    "hesketh": "A cooper, forties, craftsman through and through. Mildly wounded pride — a man who made barrels "
               "for a city now hauling buckets for a village. Wry, measured, never self-pitying.",
    "iskra": "The village herb-woman, forties, blunt and low. Everyone gives her table a wide berth and she "
             "notices, and files it away. Speaks like someone dosing poison — carefully, precisely.",
    "wick": "A young villager who repeats what his mother says and then asks the real question underneath "
            "it. Light, quick, serious — earnest rather than childish.",
    "winna": "A weaver in her fifties. She wove the wrappings for half the village's dead and her hands still "
             "remember it. Warm, worn, quiet — kindness held together with habit.",
    "asta": "An old woman at Greymarch, seventies, who watched the road for her son for forty years. Thin, "
            "papery, patient — grief gone so deep it reads as calm.",
    "dace": "A Greymarch gate captain, forties, all procedure on the outside and worry underneath. Reads "
            "people the way he reads writs — fast, and usually right.",
    "mira": "A young widow who escaped the Hollow camp. Twenties, steadier than she should be — the voice of "
            "someone who decided the dead do not get to finish her sentences.",
    "oona": "A Salt League factor in Greymarch, fifties, dry as salt. Every kindness has a ledger entry; she "
            "admits this cheerfully. Trader's rhythm, sharp eyes in the voice.",
    "pell": "The Moot-crier of Greymarch. Projecting, heraldic, slightly ridiculous and entirely sincere. "
            "Every sentence ends like a proclamation because to him, it is one.",
    "vesna": "Vesna Kell, Salt League courier, thirties. Talks over the noise of a wagon that will not stop — "
             "fast, warm, always halfway to the next thing. Trustworthy in a hurry.",
    "galen": "A bookbinder in Greymarch, sixties. Handles a government ledger like a sick animal — curious, "
             "wary, quietly thrilled. The voice of a man who reads spines before faces.",
    "roane": "A clerk who hid in the keep's vault rather than die at his desk. Thirties, nervous energy, talks "
             "fast when scared and he is scared most of the time — but he keeps his figures straight.",
    "rook": "A brigand captain who thinks of himself as a quartermaster. Forties, educated, ironic. He "
            "bargains because it costs him nothing and menaces because it costs you something.",
    "durn": "The keep's castellan and a deserter king. Fifties, military, bone-tired — a man still "
            "wearing a uniform for an army that left him. Controlled, heavy, almost polite.",
    "halvard": "The Stampworks' master coiner, fifties, precise, clever and long past excuses for himself. "
               "Cultivated voice, faint contempt in it — for the League, for the watch, for the wages. "
               "Calm in the way only a man who has already lost is calm.",
    "ansel": "The Charter Warden of Greymarch, sixties, careful, dry. A man who has stamped his own signature "
             "ten thousand times and grown suspicious of paper.",
    "collum": "A reeve who counts loads at the Saltpans. Fifties, sings the numbers under his breath. "
              "Exact, mild, unstoppable — arithmetic as a form of prayer.",
    "lene": "The pans' gatekeeper, thirties, a spear where most people keep a spine. Flat, clipped, "
            "unimpressed — she has turned back richer men than you.",
    "mirren": "An old salt-worker, seventies, sitting by the one fire the League does not count. Slow, "
              "amused, wheezy — every sentence walks instead of runs.",
    "tess": "A porter's wife stranded at the pans with a broken cart. Thirties, worn, unsentimental. She "
            "asks for help like it costs her something — because it does.",
    "boru": "A salt-worker, forties, half-philosophy and half-crust. Slow, flat, faintly amused — a man who "
            "has stood on a white plain too long and come back strange.",
    "hask": "The Saltpans' factor. Fifties, League-trained, dryly courteous. Every sentence has a clause in "
            "it — literal-minded, reliable, faintly tired of everything counting.",
    "brack": "The chalk pit's old overseer, sixties. Scarred, blunt, unhurried. He ran the pit when it was "
             "alive and watches it now the way you'd watch a grave you dug yourself.",
    "dess": "A chalk-pit clerk, fifties, retired into memory. Slab to scale, scale to ledger — he recites "
            "the dead pit's bookkeeping like a litany. Mild, murmured, a little lost.",
    "ines": "A tally-clerk, forties, still posting a count nobody reads. Fussy, precise, quietly defiant — "
            "routine is the last thing between her and the empty pit.",
    "ket": "A squatter living in the pit floor. Sixties, rough, warm once the kettle's on. Speaks like a man "
           "who chose his walls and doesn't care that they're chalk.",
    "rill": "A face-cutter, fifties, the only one who stayed. Hard, even, patient — the voice of a man who "
            "knows a half-cut face will fall on somebody, and counts who.",
    "simm": "The seal-warden, forties, whose whole office is a brass stamp. Pompous in an official way and "
            "aware of it — small authority carried very carefully.",
    "novak": "A weigh-house clerk in the Stampworks, thirties, whispering. Every sentence pitched under the "
             "watch's hearing — frightened, precise, quietly furious.",
    "jory": "A die-setter, forties, nervous hands and a nervous mouth. Talks about dies like old friends. "
            "Jumpy, conspiratorial, decent.",
    "vall": "The Stampworks' cook, fifties, ladle in one hand and grudge in the other. Earthy, warm, "
            "deliberate — she feeds the yard because a fed yard stays bought, and remembers why.",
    "wicke": "A blank-carrier, twenties, tired and honest. Young voice, older eyes — he counts crucibles "
             "because counting is the only thing that hasn't lied to him.",
    "yard_hand": "A Stampworks labourer, twenties, happy not to ask questions. Easy, blunt, cheerful — he "
                 "lifts, weighs, marks, and lets the seals worry about themselves.",
    "joss": "A toll-counter in his fifties, paid in sticks he couldn't read. Gentle, slow, faintly "
            "embarrassed — a man who counted honestly for people who weren't.",
    "effa": "The crew's daybook-keeper, sixties. Careful, soft, unbreakable — she kept the count in the cage "
            "and the voice still has the cage in it.",
    "pike": "The crew's errand-runner, twenties, quick and small. Grateful, jumpy, a voice that ran messages "
            "under fists and learned to stay light.",
    "annet": "A caged toll-keeper, forties, who counted her captors by candle. Even, controlled, quietly "
             "enraged — patience as a weapon she kept sharp.",
    "toll_porter": "A freed porter, thirties, blunt and relieved. Paid in crusts and counted like coin — "
                   "says it flat, like weather that's finally over.",
    "reckoner": "An Ash Line book-closer, forties. Dry, exact, faintly sorrowful — he finishes the dead's "
                "accounts because somebody has to. Civil, methodical, unsettlingly calm.",
    "straggler": "A deserter from the keep's watch, thirties, hoarse. Guilt kept under a soldier's bark — he "
                 "walked out before the doors locked and hasn't stopped walking.",
    "courier": "A bannerless courier, thirties, quick and carefully honest. No writ, no flag — just a price "
               "and the manners to say it plainly.",
    "brigid": "A drover's cook at the Rest, fifties, feeding fourteen off a fire meant for six. Brisk, warm, "
              "exasperated — generosity at twice the needed volume.",
    "fen": "An old drover, sixties, sixty years on the road. Slow, contented, faintly amused — every camp "
           "has a smell and he can name it.",
    "drover": "A drover's hand, twenties, easy and weatherbeaten. Friendly, casual — the Rest doesn't close "
              "for anyone and neither does he.",
    "nell": "A wagon-driver, thirties, whose cart held together out of spite. Wry, harried, practical — she "
            "counts her pins the way other people count blessings.",
    "ossel": "The corral's keeper, forties, apologetic and worried. The jackals have been feeding through "
             "the fence and he's run out of apologies that fix it.",
    "pate": "A tack-hand, twenties, blaming himself for bridles he couldn't hold. Young, contrite, quick — "
            "a boy who keeps replaying the one moment he froze.",
    "corl": "The Windrest's miller, fifties. Loud enough to be heard over the vanes, comfortable in the "
            "noise. Grounded, plain, faintly proud — the mill turns and so does he.",
    "bryd": "The mill's tally-keeper, forties, notching every stook twice. Exact, quiet, methodical — the "
            "voice of a man whose arithmetic is the hill's memory.",
    "odo": "The League's weigh-count at the canopy, fifties. Procedural, unmoved, sits where the sacks sit. "
           "The voice of a man who chose his post and defends it without raising his voice.",
    "odle": "The mill's carter, forties, walking the lane so the ruts remember wheels. Rough, easy, "
            "talkative — a man happiest mid-haul even with no cart.",
    "tam": "A meal-stacker, twenties, strong and out of work. Frustrated energy, honest, young — the cart "
            "isn't rolling and he doesn't know what to do with his hands.",
    "clod": "An old charcoal-burner, sixties, smoke-cured and unhurried. The fire doesn't mind if you sit "
            "or walk, and neither does he. Flat, dry, warm at the edges.",
    "ferra": "A burn-boss, forties, blunt and exact. The burn doesn't care which way you stand, and she'll "
             "tell you so. Even, hard, fair.",
    "kell": "The last cart of the day, fifties, hauling coal with a bad back and good cheer. Wheezy, warm, "
            "talkative — every load gets a joke.",
    "penn": "A soot-streaked young man who knows the camp better than anyone and is barred from the burn "
            "line. Quick, bright, shameless — a light voice carrying a grown man's map.",
    "pyke": "The pits' tally-warden, forties, guarding notches like coin. Clipped, suspicious, precise — "
            "the count is the law and he is the count.",
    "wren": "The Burnpits' kiln-keeper, thirties, standing upwind of everything. Terse, hard, fair — a "
            "woman who'd rather hold the burn than fight poachers, and resents choosing.",
}


# Speakers without a designed voice use a stock library voice — the account's custom-voice
# slots are full, and a stock voice is far better than silence.
STOCK = {
    "crane": "zLdB71ys6965KlA2pgSr",       # Oathfire Crane (existing custom)
    "refugee": "ZbR3S1wnsdOdGme2BuLI",     # Oathfire Refugee (existing custom)
    "tobias": "t5d8vleo0ua5O58EjnYe",      # Oathfire Tobias (existing custom)
    "rook": "N2lVS1w4EtoT3dr4eOWO",        # Callum - husky trickster
    "durn": "pNInz6obpgDQGcFmaJgB",        # Adam - dominant, firm
    "halvard": "onwK4e9ZLuTAKqWW03F9",     # Daniel - steady broadcaster
    "vesna": "cgSgspJ2msm6clMCkdW9",       # Jessica - playful, warm
    "galen": "pqHfZKP75CvOlQylNhV4",       # Bill - wise, mature
    "reckoner": "nPczCjzI2devNBz1zQrb",    # Brian - deep, resonant
    "courier": "IKne3meq5aSn9XLyUdCD",     # Charlie - energetic
    "straggler": "SOYHLrjzK2X1ezoPC6cr",   # Harry - fierce warrior
    "gethin": "bIHbv24MWmeRgasZH58o",      # Will - relaxed optimist
    "hesketh": "CwhRBWXzGAHq8TQ4Fs17",     # Roger - laid-back
    "iskra": "Xb7hH8MSUJpSbSDYk0k2",       # Alice - clear educator
    "wick": "dRqoXefWJMo2lbk72Ucp",        # Jake - young, british
    "winna": "hpp4J3VqNfWAUOO0d1Us",       # Bella - bright, warm
    "boru": "iP95p4xoKVk53GoZ742B",        # Chris - down-to-earth
    "collum": "JBFqnCBsd6RMkjVDRZzb",      # George - warm storyteller
    "lene": "XrExE9yKIg1WjnnlVkGX",        # Matilda - professional
    "mirren": "bfGb7JTLUnZebZRiFYyq",      # Adam - deep, engaging
    "tess": "SAz9YHcvj6GT2YYXdXww",        # River - relaxed, neutral
    "corl": "JBFqnCBsd6RMkjVDRZzb",        # George - warm storyteller
    "bryd": "W0T33KO9BEThHKbmurx1",        # Magic Jack - narrator
    "odo": "CwhRBWXzGAHq8TQ4Fs17",         # Roger - laid-back
    "odle": "iP95p4xoKVk53GoZ742B",        # Chris - down-to-earth
    "tam": "TX3LPaxmHKxFdv7VOQHJ",         # Liam - energetic
    "annet": "pFZP5JQG7iQjIQuC4Bku",       # Lily - velvety actress
    "effa": "Xb7hH8MSUJpSbSDYk0k2",        # Alice - clear educator
    "joss": "pqHfZKP75CvOlQylNhV4",        # Bill - wise, mature
    "pike": "IKne3meq5aSn9XLyUdCD",        # Charlie - energetic
    "toll_porter": "TX3LPaxmHKxFdv7VOQHJ", # Liam - energetic
    "drover": "bIHbv24MWmeRgasZH58o",      # Will - relaxed optimist
    "fen": "pqHfZKP75CvOlQylNhV4",         # Bill - wise, mature
    "nell": "EXAVITQu4vr4xnSDxMaL",        # Sarah - mature, reassuring
    "ossel": "bIHbv24MWmeRgasZH58o",       # Will - relaxed optimist
    "pate": "dRqoXefWJMo2lbk72Ucp",        # Jake - young, british
    "yard_hand": "N2lVS1w4EtoT3dr4eOWO",   # Callum - husky
    "vall": "FGY2WhTYpPnrIDTdsKH5",        # Laura - sassy
    "jory": "bIHbv24MWmeRgasZH58o",        # Will - relaxed optimist
    "novak": "dRqoXefWJMo2lbk72Ucp",       # Jake - young, british
    "wicke": "TX3LPaxmHKxFdv7VOQHJ",       # Liam - energetic
    "sabel": "CwhRBWXzGAHq8TQ4Fs17",       # Roger - laid-back
    "surveyor": "onwK4e9ZLuTAKqWW03F9",    # Daniel - steady broadcaster
    "roane": "bIHbv24MWmeRgasZH58o",       # Will - relaxed optimist
}


def post(url: str, body: dict, retries: int = 5) -> bytes:
    for attempt in range(retries):
        request = urllib.request.Request(
            url,
            data=json.dumps(body).encode(),
            headers={"xi-api-key": require("ELEVENLABS_API_KEY"),
                     "Content-Type": "application/json", "User-Agent": "OathfireTools/1.0"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            body_out = error.read()
            if "safety" in body_out[:400].decode(errors="ignore").lower():
                raise
            if attempt == retries - 1:
                raise RuntimeError(f"{url}: HTTP {error.code} {body_out[:300]!r}") from None
            wait = 15 * (attempt + 1)
            print(f"  HTTP {error.code}, retrying in {wait}s")
            time.sleep(wait)


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
    if speaker in STOCK:
        voices[speaker] = STOCK[speaker]
        voices_path.parent.mkdir(parents=True, exist_ok=True)
        voices_path.write_text(json.dumps(voices, indent=2), encoding="utf-8")
        print(f"stock voice: {speaker}")
        return STOCK[speaker]

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
