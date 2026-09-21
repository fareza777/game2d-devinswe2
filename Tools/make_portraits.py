"""Generates dialogue portraits with Replicate FLUX, in the same woodcut codex style as the cinematic.

Recraft refuses or mangles specific figures, so portraits go through FLUX with the style described in the
prompt instead of palette controls. Output: Assets/Oathfire/Resources/Portraits/<speaker>.png (square bust).

Usage: python make_portraits.py [speaker ...] [--force]
"""
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from PIL import Image

from oathfire_tools.print_style import apply as apply_print_style
from replicate_generate import image

PROJECT = Path(__file__).resolve().parent.parent
OUT_DIR = PROJECT / "Assets" / "Oathfire" / "Resources" / "Portraits"
RAW_DIR = Path(__file__).resolve().parent / "cache" / "portraits"

# One locked treatment so every portrait reads as the same printed set.
STYLE = (
    "woodcut linocut print portrait from one matching series, carved gouge strokes, thick black ink, "
    "bone-white highlights, muted moss green mid-tone, three inks only, flat with no soft shading, "
    "head and shoulders portrait, face centred and filling the frame, fully clothed, solid near-black background, "
    "edge-to-edge full bleed with no white margin, no border, no frame, no text, no signature"
)

PORTRAITS = {
    "brann": "An old gravedigger in his late sixties, weathered lined face, grey stubble, deep-set tired eyes, "
             "battered wide-brimmed hat, worn leather coat with a shovel strap, dry knowing half-smile",
    "kael": "A man knight in his mid thirties, short dark hair, close-cropped dark beard, a scar through one "
              "eyebrow, weathered stubborn face, plain dented breastplate over a travel-stained gambeson, "
              "steady tired eyes of someone who has lost everything and kept walking",
    "isolde": "A queen in her forties on the night of her death, pale drawn face, braided dark hair, "
              "a crown of woven roots cracking apart above her brow, calm exhausted eyes, heavy embroidered collar",
    "aurel": "A lord protector in his fifties, sleek silver-streaked hair, neat pointed beard, cold courteous smile, "
             "high fur-trimmed collar, a scaled lion brooch at his throat, aristocratic and dangerous",
    "maren": "A young priestess inquisitor of a fire church, severe short auburn hair, freckles, doubting earnest eyes, "
             "high-collared robe with a flame emblem, small brass censer chain at her belt",
    "galen": "A young knight in his twenties, honest open face, close-cropped fair hair, faint sunburn, "
             "polished breastplate with a scaled lion crest, slightly uncertain expression",
    "tobias": "A lean young poacher, unkempt dark hair, sharp watchful eyes, hood down, quiver strap across chest, "
              "smudged face, defiant and hungry",
    "iskra": "A necromancer apothecary woman in her thirties, shaved temple and long braid, ash markings on her cheekbones, "
             "hooded robe hung with small bone charms and glass vials, unsettling calm gaze",
    "crane": "An ancient necromancer shepherd, gaunt hollow face half hidden by a deep grey hood, long white beard, "
             "eyes like cold lanterns, tattered warden's cloak, a shepherd's crook of bound bones",
    "corl": "A master miller in his fifties, broad flour-dusted face, grizzled short beard white with meal dust, "
            "heavy brow and patient tired eyes, rolled linen sleeves and a leather apron strap over one shoulder",
    "bryd": "A miller's wife in her forties, sharp clever face, hair tucked under a plain kerchief, "
            "chalk marks of tally notches on her cheek and knuckles, appraising eyes that count everything",
    "tam": "A miller's lad of about sixteen, narrow freckled face under a flop of pale hair, flour on his nose, "
           "eager worried eyes, oversized rolled-up work shirt",
    "odo": "A salt League weigher in his forties, thin precise face, pinched lips, ink-stained fingers folded, "
           "a flat ledger clerk's cap, suspicious exact eyes behind a still expression",
    "odle": "A carter in his thirties, lean sun-browned face, dusty curls under a flat wool cap, "
            "easy half-smile, rope burns on his knuckles, a traveller's unhurried gaze",
    "asta": "A widowed lamp-keeper in her seventies, deeply lined face under a black mourning shawl, "
            "pale watery eyes that have seen too many roads, thin lips pressed in patient grief",
    "boru": "A salt porter in his thirties, heavy sun-burnt face, salt-white dust in his eyebrows and beard, "
            "a flattened nose from an old break, broad shoulders implied under a stained jerkin",
    "clod": "An ancient charcoal burner, face like blackened bark, deep creases filled with soot, "
            "a wispy white fringe of hair, small bright eyes in all that dark, bent but unbroken",
    "collum": "A salt-pan keeper in his fifties, leathery creased face, cracked lips, a salt-scaled brow, "
              "watchful sunken eyes of a man who guards measures, canvas hood pushed back",
    "courier": "A wiry bannerless courier in his twenties, sharp clean-shaven face, dark quick eyes, "
               "a hood half-drawn over oiled hair, the careful smile of someone paid not to be remembered",
    "dace": "A corporal of the watch in his forties, square jaw heavy with old stubble, a broken nose set crooked, "
            "small shrewd eyes under a kettle helm brim, a career soldier's tired certainty",
    "durn": "An aging castellan in his sixties, once a paymaster — jowled severe face, iron-grey tonsure of hair, "
            "a fur-lined collar over a dented cuirass, cold counting eyes of a man who keeps keys",
    "ferra": "A rack-warden woman in her forties, broad soot-smudged face, hair bound tight in a dark scarf, "
             "steady smoke-stung eyes, a heavy set jaw, hands that have carried a thousand racks",
    "gethin": "A village record-keeper in his sixties, thin stooped shoulders, papery lined face, "
              "ink-stained fingertips, pale eyes behind a squint, a robe of dusty grey wool",
    "hask": "A salt League factor in his fifties, pompous full face, carefully combed greying hair, "
            "small ringed fingers steepled, a measure-master's chain of office, imperious squint",
    "brack": "A quarry foreman in his fifties, massive weathered face dusted white with chalk, "
             "shaved grey bristles on a square jaw, deep furrows at the eyes, a leather shoulder-yoke still on",
    "ines": "A tally clerk woman in her thirties, sharp thin face under a clerk's cap, chalk dust in her "
            "hairline, ink-stained lips pressed thin, a stick of chalk behind one ear, quick exact eyes",
    "simm": "A seal warden in his forties, narrow careful face, neat dark beard, a brass seal-stamp hung "
            "at his chest on a chain, polished precise eyes that weigh everything",
    "ket": "A squatter woman in her forties, lean chalk-pale face, wild unbound hair greyed with dust, "
           "bright unfazed eyes, a patched shawl, the smile of someone who owns a hole",
    "rill": "A stone cutter in his thirties, broad flat nose and heavy jaw, face pale with rock dust, "
            "a leather headband, calm stubborn eyes, forearms wrapped in chalk-stained cloth",
    "dess": "A pit porter in his twenties, round open face gone pale under chalk dust, short cropped hair, "
            "a sling strap across his chest, the blank patience of someone who carries things",
    "hesketh": "A thatcher and roofer in his forties, weather-roughened face, straw-yellow hair tied back, "
               "chalk dust on his brow, a craftsman's squint, cracked thumbnail on a worried hand",
    "kell": "A coal cart driver in his fifties, round honest face smudged with charcoal, "
            "thick white sideburns, watery eyes from smoke and wind, a drover's patient frown",
    "lene": "A gate-watch woman in her thirties, hard sun-browned face, cropped dark hair under a helm rim, "
            "a scar along the jaw, level unimpressed stare of someone who checks every cart",
    "mira": "A wagon-master woman in her forties, strong weathered face, auburn braid coiled at her neck, "
            "keen calculating eyes, a trader's guarded warmth, dust of forty-one roads on her collar",
    "mirren": "An old salt-pan worker in her seventies, shrunken wrinkled face, few teeth, "
              "whites of the eyes gone pearl-grey, a knotted kerchief, sharp ears that miss nothing",
    "oona": "A head porter woman in her thirties, broad cheerful face with a broken-toothed grin, "
            "hair cropped short under a sweat-stained wrap, muscled neck, kind forceful eyes",
    "pell": "A town crier in his fifties, long horse face, extravagant grey moustache, "
            "brass bell at his belt, mouth open mid-cry, theatrical faded dignity",
    "penn": "A soot-blackened boy of about thirteen, narrow grubby face, eyes startlingly white in all the soot, "
            "tangled hair with dust in it, quick wary grin of a child who works among kilns",
    "pyke": "A tally-foreman in his forties, lean hard face, a shaved scalp going to stubble, "
            "a knotted cord of tallies at his belt, counting eyes that never rest",
    "reckoner": "An Ash Line reckonner in his fifties, gaunt grey face, thin pate circled by iron hair, "
                "spectacle-less squint, a ledger strap across one shoulder, the stillness of a man who audits the dead",
    "refugee": "A road refugee woman in her thirties, hollow exhausted face, dust and tear tracks, "
               "a torn shawl over loose hair, holding herself together by her hands alone",
    "roane": "A ledger clerk in his thirties, soft frightened face, ink smudge on one cheek, "
             "hair thinning early, darting eyes of a man who hid in a vault and lived",
    "rook": "A scarred mercenary captain in his forties, lean weathered face, grey-streaked beard, "
            "a worn officer's cloak, dark amused eyes that measure men's nerve",
    "sabel": "A smith woman in her forties, heavy forearms, forge-tanned broad face, "
             "a burn scar on one cheek, iron-filing speckled brow, blunt appraising stare",
    "straggler": "A deserter straggler in his twenties, gaunt hungry face, a week of stubble, "
                 "hollow eyes above sunken cheeks, a torn Ash Line tunic belted with rope",
    "surveyor": "A royal surveyor in his fifties, neat precise face, waxed grey moustache, "
                "a chain and rod case over one shoulder, pale distant eyes that are always measuring",
    "tess": "A cheerful middle-aged provisioner, freckled weathered face, "
            "a canvas hood over tied-back hair, quick warm eyes, patient smile",
    "vesna": "A quiet quartermaster woman in her fifties, narrow face gone austere, "
             "iron-grey hair in a tight bun, sealed lips, eyes that file and forget nothing",
    "wick": "A lamplighter in his thirties, long melancholy face, a lampblack smudge on his chin, "
            "shoulder-length dark hair, gentle sleepless eyes, a taper still in his hand",
    "winna": "A village elder in her seventies, round wrinkled face dusted with flour, "
             "small sharp eyes under a white kerchief, cheeks like dried apples, a shrewd knowing mouth",
    "wren": "A kiln-keeper man in his forties, gaunt sooty face, lashes singed short, "
            "a leather hood pushed back from close-cropped hair, eyes reddened by years of smoke",
}


def job(speaker: str, description: str) -> str:
    out = OUT_DIR / f"{speaker}.png"
    if out.exists() and "--force" not in sys.argv:
        return f"skip {speaker}"

    raw = RAW_DIR / f"{speaker}.png"
    image(raw, "1:1", f"{description}. {STYLE}")

    portrait = apply_print_style(Image.open(raw))
    portrait.thumbnail((512, 512))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    portrait.save(out)
    return f"saved {speaker}"


def main() -> None:
    wanted = [arg for arg in sys.argv[1:] if not arg.startswith("--")] or list(PORTRAITS)
    with ThreadPoolExecutor(max_workers=3) as pool:
        for result in pool.map(lambda s: job(s, PORTRAITS[s]), wanted):
            print(result)


if __name__ == "__main__":
    main()
