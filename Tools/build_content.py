"""Turns authoring data into Unity runtime content.

- Docs/opening_cinematic.json -> Resources/Cinematic/opening.json (+ panel sprites)
- line text -> Resources/Localization/{en,id}.json, merged with the UI strings below
- procedural FX sprites (fire glow, ember) and a watcher silhouette cut from the tileset's skeleton sheet

Usage: python build_content.py
"""
import json
import re
import shutil
from pathlib import Path

from PIL import Image

TOOLS = Path(__file__).resolve().parent
PROJECT = TOOLS.parent
DOCS = PROJECT / "Docs"
RESOURCES = PROJECT / "Assets" / "Oathfire" / "Resources"
ART = PROJECT / "Assets" / "Oathfire" / "Art" / "Cinematic" / "Opening"
SKELETON_SHEET = PROJECT / "Assets" / "SmallScaleInt" / "Fantasy kingdom Tileset" / "Characters" / "Enemy 1" / "Idle.png"

UI_STRINGS = {
    "menu.continue": ("Continue", "Lanjutkan"),
    "menu.newGame": ("New Game", "Permainan Baru"),
    "menu.options": ("Options", "Pengaturan"),
    "menu.quit": ("Quit", "Keluar"),
    "options.title": ("Options", "Pengaturan"),
    "options.language": ("Language", "Bahasa"),
    "options.music": ("Music", "Musik"),
    "options.sfx": ("Effects", "Efek Suara"),
    "options.voice": ("Voice", "Suara Tokoh"),
    "options.textSpeed": ("Text speed", "Kecepatan Teks"),
    "options.close": ("Close", "Tutup"),
    "options.shake": ("Screen shake", "Guncangan layar"),
    "options.leftHanded": ("Left-handed controls", "Kontrol tangan kiri"),
    "options.subtitles": ("Subtitles", "Subtitle"),
    "options.questArrow": ("Quest arrow", "Panah quest"),
    "options.on": ("ON", "NYALA"),
    "options.off": ("OFF", "MATI"),
    "prompt.use": ("Use", "Pakai"),
    "prompt.talk": ("Talk", "Bicara"),
    "prompt.hearth.light": ("Light the oathfire", "Nyalakan api sumpah"),
    "prompt.hearth.tend": ("Tend the oathfire", "Rawat api sumpah"),
    "prompt.gather.timber": ("Chop timber: tap USE or strike the tree", "Tebang kayu: ketuk PAKAI atau tebas pohonnya"),
    "prompt.gather.stone": ("Quarry stone: tap USE or strike the rock", "Gali batu: ketuk PAKAI atau pukul batunya"),
    "prompt.door.open": ("Open the door", "Buka pintu"),
    "prompt.door.close": ("Close the door", "Tutup pintu"),
    "prompt.gather.regrowing": ("Picked clean. It grows back soon", "Sudah habis. Sebentar lagi tumbuh lagi"),
    "toast.hearthNeeds": ("The hearth needs timber {0}/{1} and stone {2}/{3}", "Perapian butuh kayu {0}/{1} dan batu {2}/{3}"),
    "toast.gathered": ("+{0} {1}  ·  {2} in your pack", "+{0} {1}  ·  {2} di tas"),
    "prompt.gather.iron": ("Mine iron", "Tambang besi"),
    "prompt.build.shelter": ("Raise a shelter (6 timber, 2 stone)", "Bangun tempat berlindung (6 kayu, 2 batu)"),
    "prompt.build.palisade": ("Raise a palisade (8 timber, 4 stone)", "Bangun pagar kayu (8 kayu, 4 batu)"),
    "prompt.built": ("Standing", "Sudah berdiri"),
    "toast.needMaterials": ("Not enough materials", "Bahan tidak cukup"),
    "toast.built": ("The hearth light reaches further", "Cahaya perapian menjangkau lebih jauh"),
    "toast.nightFalls": ("Night falls. Something is moving in the trees.", "Malam turun. Ada yang bergerak di antara pepohonan."),
    "toast.dawn": ("Dawn. The valley is quiet again.", "Fajar. Lembah kembali sunyi."),
    "toast.hearthTended": ("The oathfire burns steady", "Api sumpah menyala mantap"),
    "toast.nothingToSay": ("Nothing more to say for now", "Belum ada yang perlu dibicarakan"),
    "inv.title": ("Pack", "Bawaan"),
    "inv.close": ("Close", "Tutup"),
    "inv.equip": ("EQUIP", "PAKAI"),
    "inv.unequip": ("TAKE OFF", "LEPAS"),
    "inv.use": ("USE", "GUNAKAN"),
    "inv.back": ("BACK", "KEMBALI"),
    "inv.tab.equipment": ("EQUIPMENT", "PERLENGKAPAN"),
    "inv.tab.bag": ("BAG", "TAS"),
    "inv.dollStats": ("Damage {0}   ·   Armour {1}   ·   Health {2}", "Serangan {0}   ·   Zirah {1}   ·   Nyawa {2}"),
    "inv.pick.title": ("Wear on: {0}", "Pakai di: {0}"),
    "inv.pick.none": ("Nothing in your bag fits there", "Tak ada barang di tas yang cocok di sana"),
    "rarity.Common": ("Common", "Biasa"),
    "rarity.Fine": ("Fine", "Bagus"),
    "rarity.Rare": ("Rare", "Langka"),
    "rarity.Oathbound": ("Oathbound", "Terikat Sumpah"),
    "kind.Material": ("Material", "Bahan"),
    "kind.Consumable": ("Consumable", "Habis pakai"),
    "kind.Weapon": ("Weapon", "Senjata"),
    "kind.Armor": ("Armour", "Zirah"),
    "kind.Trinket": ("Trinket", "Perhiasan"),
    "kind.Quest": ("Story item", "Barang cerita"),
    "kind.Relic": ("Relic", "Pusaka"),
    "stat.damage": ("Damage", "Serangan"),
    "stat.armour": ("Armour", "Zirah"),
    "stat.health": ("Health", "Nyawa"),
    "stat.heals": ("Restores", "Memulihkan"),
    "stat.mana": ("Ember", "Bara"),
    "stat.value": ("Worth", "Harga"),
    "inv.equipped": ("  (worn)", "  (dipakai)"),
    "inv.stats": ("Level {0}   HP {1}   DMG {2}   ARM {3}   Coin {4}", "Level {0}   HP {1}   SRG {2}   ZRH {3}   Koin {4}"),
    "hero.title": ("The Warden's Book", "Buku Sang Warden"),
    "hero.name": ("Kael Voss  ·  Warden of Rennfall", "Kael Voss  ·  Warden Rennfall"),
    "hero.section.self": ("HIMSELF", "DIRINYA"),
    "hero.section.worn": ("WHAT HE CARRIES", "YANG IA BAWA"),
    "hero.section.standing": ("HOW HE IS REGARDED", "BAGAIMANA IA DIPANDANG"),
    "hero.section.deeds": ("WHAT HE HAS DONE", "APA YANG TELAH IA LAKUKAN"),
    "hero.level": ("Level", "Level"),
    "hero.experience": ("Experience", "Pengalaman"),
    "hero.health": ("Health", "Nyawa"),
    "hero.damage": ("Damage", "Serangan"),
    "hero.armour": ("Armour", "Zirah"),
    "hero.coin": ("Coin", "Koin"),
    "hero.slot.mainHand": ("Sword hand", "Tangan pedang"),
    "hero.slot.offHand": ("Off hand", "Tangan kiri"),
    "hero.slot.head": ("Head", "Kepala"),
    "hero.slot.body": ("Body", "Badan"),
    "hero.slot.trinket": ("Kept close", "Dibawa dekat"),
    "hero.slot.empty": ("nothing", "kosong"),
    "quest.returnTo": ("Return to {0}", "Kembali ke {0}"),
    "offer.doneHeading": ("Work done", "Pekerjaan selesai"),
    "offer.handIn": ("Hand it over", "Serahkan"),
    "toast.questDone": ("Done: {0}", "Selesai: {0}"),
    "prompt.search": ("Search here", "Geledah di sini"),
    "toast.found": ("Found: {0}", "Ditemukan: {0}"),
    "toast.graveStir": ("The graves stir.", "Kuburan-kuburan bergerak."),
    "prompt.hearth.callNight": ("Call the night", "Panggil malam"),
    "toast.hearthResting": ("The valley is still catching its breath", "Lembah masih mengatur napas"),
    "shop.buy": ("Buy", "Beli"),
    "shop.sell": ("Sell", "Jual"),
    "shop.leave": ("Leave", "Pergi"),
    "shop.coin": ("Your purse: {0} coin", "Kantongmu: {0} koin"),
    "shop.price": ("{0} coin", "{0} koin"),
    "shop.sellFor": ("Sells for {0}", "Laku {0}"),
    "shop.bought": ("Bought: {0}", "Dibeli: {0}"),
    "shop.cannotAfford": ("Not enough coin", "Koin tidak cukup"),
    "shop.nothingToSell": ("Nothing in the pack worth selling.", "Tak ada di tas yang layak dijual."),
    "skills.dodge": ("Dodge is always ready on its own button.", "Menghindar selalu siap di tombolnya sendiri."),
    "skills.cost": ("{0} mana  ·  {1}s to recover", "{0} mana  ·  pulih {1} dtk"),
    "skills.locked": ("Opens at level {0}", "Terbuka di level {0}"),
    "skills.heading": ("Two skills can be carried into a fight. Tap I or II to carry one.", "Dua skill bisa dibawa bertarung. Ketuk I atau II untuk membawanya."),
    "journal.tabSkills": ("Skills", "Skill"),
    "hero.slot.neck": ("Neck", "Leher"),
    "hero.slot.hands": ("Hands", "Tangan"),
    "hero.slot.legs": ("Legs", "Kaki"),
    "hero.slot.feet": ("Feet", "Alas kaki"),
    "hero.slot.ring": ("Ring", "Cincin"),
    "inv.cat.all": ("All", "Semua"),
    "inv.cat.weapons": ("Arms", "Senjata"),
    "inv.cat.armour": ("Armour", "Zirah"),
    "inv.cat.trinkets": ("Trinkets", "Perhiasan"),
    "inv.cat.supplies": ("Supplies", "Ramuan"),
    "inv.cat.materials": ("Materials", "Bahan"),
    "inv.cat.quest": ("Quest", "Quest"),
    "inv.cat.empty": ("Nothing of this kind in the pack.", "Tak ada barang jenis ini di tas."),
    "journal.following": ("FOLLOWING", "DIIKUTI"),
    "journal.tapToFollow": ("Tap to follow it with the arrow", "Ketuk untuk diikuti panah"),
    "hero.nights": ("Nights held", "Malam dipertahankan"),
    "hero.contracts": ("Contracts cleared", "Kontrak diselesaikan"),
    "hero.built": ("Roofs raised", "Atap didirikan"),
    "hero.felled": ("Put down", "Ditumbangkan"),
    "hero.close": ("Close the book", "Tutup buku"),
    "journal.tab": ("JOURNAL", "JURNAL"),
    "journal.tabWarden": ("WARDEN", "WARDEN"),
    "journal.main": ("THE STORY", "KISAH UTAMA"),
    "journal.side": ("SIDE WORK", "PEKERJAAN SAMPINGAN"),
    "journal.done": ("FINISHED", "SELESAI"),
    "journal.mainNone": ("Nothing presses. Rest while it lasts.", "Tak ada yang mendesak. Beristirahatlah selagi bisa."),
    "journal.sideNone": ("Nothing taken on. Ask the people here — they have work.", "Belum ada yang diterima. Tanyakan pada orang-orang di sini, mereka punya pekerjaan."),
    "faction.hearth": ("Rennfall hearth", "Perapian Rennfall"),
    "faction.crown": ("The Protectorate", "Protektorat"),
    "faction.ash": ("Ash Marshals", "Barisan Abu"),
    "faction.salt": ("Salt League", "Serikat Garam"),
    "hud.hero": ("BOOK", "BUKU"),
    "hud.pack": ("PACK", "TAS"),
    "toast.equipped": ("Equipped", "Dipakai"),
    "toast.used": ("Used", "Terpakai"),
    "tone.iron": ("Iron", "Besi"),
    "tone.mercy": ("Mercy", "Belas"),
    "tone.guile": ("Guile", "Siasat"),
    "board.title": ("Warden's Board", "Papan Warden"),
    "board.summary": ("{0} posted  ·  {1} taken  ·  {2} completed", "{0} ditawarkan  ·  {1} diambil  ·  {2} selesai"),
    "board.reward": ("{0} required  ·  {1} coin", "{0} diperlukan  ·  {1} koin"),
    "board.accept": ("TAKE", "AMBIL"),
    "board.inProgress": ("IN HAND", "BERJALAN"),
    "board.claim": ("CLAIM", "KLAIM"),
    "board.close": ("Close", "Tutup"),
    "prompt.board": ("Read the notice board", "Baca papan pengumuman"),
    "toast.contractTaken": ("Contract taken", "Kontrak diambil"),
    "toast.contractPaid": ("Contract paid", "Kontrak dibayar"),
    "hud.board": ("JOBS", "KERJA"),
    "hud.use": ("USE", "PAKAI"),
    "hud.night": ("{0}  ·  {1} still standing", "{0}  ·  {1} masih berdiri"),
    "night.push.1": ("They come out of the treeline", "Mereka keluar dari tepi hutan"),
    "night.push.2": ("The second push", "Gelombang kedua"),
    "night.push.3": ("The third push", "Gelombang ketiga"),
    "night.push.4": ("They keep coming", "Mereka terus berdatangan"),
    "night.push.last": ("The last of them", "Yang terakhir dari mereka"),
    "title.tagline": ("The dead do not sleep in untended ground", "Orang mati tak tidur di tanah yang terlantar"),
    "speaker.brann": ("Brann Hollowell", "Brann Hollowell"),
    "speaker.isolde": ("Queen Isolde", "Ratu Isolde"),
    "speaker.aurel": ("Lord Protector Aurel", "Lord Protektor Aurel"),
    "speaker.kael": ("Kael Voss", "Kael Voss"),
    "speaker.galen": ("Ser Galen", "Ser Galen"),
    "speaker.maren": ("Inspector Maren", "Inspektur Maren"),
    "speaker.dace": ("Corporal Dace", "Kopral Dace"),
    "speaker.oona": ("Oona the Porter", "Oona si Kuli"),
    "speaker.pell": ("Pell the Crier", "Pell si Juru Teriak"),
    "speaker.asta": ("Widow Asta", "Janda Asta"),
    "speaker.vesna": ("Vesna Kell", "Vesna Kell"),
    "speaker.crane": ("The Grey Shepherd", "Sang Gembala Kelabu"),
    "speaker.iskra": ("Iskra Vell", "Iskra Vell"),
    "speaker.tobias": ("Tobias Reed", "Tobias Reed"),
    "speaker.refugee": ("Nessa Aldwin", "Nessa Aldwin"),
    "speaker.surveyor": ("Surveyor Kestrel", "Juru Ukur Kestrel"),
    "speaker.hesketh": ("Hesketh the Cooper", "Hesketh si Tukang Tong"),
    "speaker.winna": ("Old Winna", "Winna Tua"),
    "speaker.sabel": ("Sabel Orr", "Sabel Orr"),
    "speaker.gethin": ("Gethin the Digger", "Gethin si Penggali"),
    "speaker.wick": ("Wick", "Wick"),
    "prompt.trade": ("Trade", "Berdagang"),
    "prompt.travel": ("Take the road", "Tempuh jalan"),
    "prompt.travel.rennfall": ("Take the road back to Rennfall", "Kembali ke Rennfall lewat jalan"),
    "prompt.travel.road": ("Take the trade road east", "Tempuh jalan dagang ke timur"),
    "prompt.travel.greymarch": ("Go on to Greymarch", "Lanjut ke Greymarch"),
    "prompt.travel.valley": ("Take the road back to the valley", "Kembali ke lembah lewat jalan"),
    "prompt.salvage": ("Search the wreck", "Geledah bangkai pedati"),
    "toast.roadClear": ("The road is quiet again", "Jalan kembali sunyi"),
    "event.ambush.banner": ("The road has closed behind you.", "Jalan tertutup di belakang Anda."),
    "event.wild.banner": ("Something has come out of the trees.", "Ada yang keluar dari antara pepohonan."),
    "event.goSpeak": ("Go and speak to them", "Temui dan ajak bicara"),
    "event.goSpeakTimed": ("Go and speak to them — {0} before they leave", "Temui dan ajak bicara — {0} sebelum mereka pergi"),
    "toast.eventMissed": ("They waited as long as they could", "Mereka menunggu selama yang mereka bisa"),
    "offer.heading": ("Work offered", "Pekerjaan ditawarkan"),
    "offer.accept": ("Take it on", "Terima"),
    "offer.later": ("Not now", "Nanti saja"),
    "offer.reward": ("Pays {0} experience and {1} coin", "Upah {0} pengalaman dan {1} koin"),
    "toast.questTaken": ("Taken on: {0}", "Diterima: {0}"),
    "speaker.mira": ("Mira Holt", "Mira Holt"),
    "speaker.rook": ("Rook Varr", "Rook Varr"),
    "boss.rook": ("Rook Varr", "Rook Varr"),
    "boss.banner": ("{0}  ·  {1} / {2}", "{0}  ·  {1} / {2}"),
    "place.Rennfall": ("Rennfall", "Rennfall"),
    "place.TradeRoad": ("The Valley Road", "Jalan Lembah"),
    "place.Greymarch": ("Greymarch", "Greymarch"),
    "place.Hollow": ("Blackthorn Hollow", "Ceruk Duri Hitam"),
    "prompt.travel.hollow": ("Follow the goat track down into the Hollow", "Ikuti jalan setapak turun ke Ceruk"),
    "prompt.travel.hollowOut": ("Climb back up to the trade road", "Naik kembali ke jalan dagang"),
    "prompt.stores": ("Search the raiders' stores", "Geledah perbekalan perampok"),
    "event.hollow.camp": ("The cookfire crew are on their feet.", "Kawanan di api unggun bangkit berdiri."),
    "event.hollow.pass": ("Lookouts drop into the pass.", "Para pengintai melompat turun ke celah."),
    "toast.hollowCamp": ("The camp is broken", "Kamp perampok sudah dihancurkan"),
    "toast.hollowPass": ("The pass is clear", "Celah sudah aman"),
    "toast.rookDown": ("Rook Varr has fallen", "Rook Varr telah tumbang"),
    "speaker.durn": ("Castellan Durn", "Castellan Durn"),
    "speaker.roane": ("Roane", "Roane"),
    "speaker.courier": ("A Voice With No Banner", "Suara Tanpa Panji"),
    "speaker.straggler": ("Line Straggler", "Sisa Barisan"),
    "speaker.reckoner": ("Line Reckoner", "Juru Hitung Barisan"),
    "boss.durn": ("Castellan Durn", "Castellan Durn"),
    "place.Keep": ("The Iron Keep", "Benteng Besi"),
    "prompt.travel.keep": ("Take the overgrown keep road north", "Tempuh jalan benteng yang ditumbuhi ke utara"),
    "prompt.travel.keepOut": ("Take the road back to the valley road", "Kembali ke jalan lembah"),
    "prompt.keepStores": ("Search the quartermaster's stores", "Geledah perbekalan bendahara"),
    "event.keep.watch": ("The vestibule watch turns out.", "Penjaga vestibul keluar."),
    "event.keep.corridor": ("Shields rise in the torch-light.", "Perisai terangkat dalam cahaya obor."),
    "event.keep.vault": ("The dead still keep the vault.", "Orang mati masih menjaga gudang bawah."),
    "toast.keepWatch": ("The vestibule is quiet", "Vestibul sudah sunyi"),
    "toast.keepCorridor": ("The corridor is clear", "Koridor sudah aman"),
    "toast.durnDown": ("Castellan Durn has fallen", "Castellan Durn telah tumbang"),
    "toast.bossDown": ("Their captain has fallen", "Pemimpin mereka telah tumbang"),
    # The Saltpans: the League's brine-works — Hask's count, Collum's pans, Lene's salt track.
    "speaker.hask": ("Factor Hask", "Faktor Hask"),
    "speaker.collum": ("Reeve Collum", "Reeve Collum"),
    "speaker.mirren": ("Old Mirren", "Mirren si Tua"),
    "speaker.tess": ("Tess of the Lane", "Tess dari Lajur"),
    "speaker.lene": ("Gate-warden Lene", "Lene Penjaga Gerbang"),
    "speaker.boru": ("Boru the Porter", "Boru si Porter"),
    "place.Saltpans": ("The Saltpans", "Petak Garam"),
    "prompt.travel.saltpans": ("Take the salt track to the pans", "Tempuh jejak garam ke petak"),
    "prompt.travel.saltpansOut": ("Walk back down the track to the trade road", "Kembali menyusuri jejak ke jalan dagang"),
    "event.salt.pans": ("Salt-thieves squat the pans.", "Pencuri garam menduduki petak-petak."),
    "event.salt.key": ("The key-warden's 'guests' object.", "'Tamu-tamu' penjaga kunci berkeberatan."),
    "toast.saltPans": ("The pans stand clear", "Petak-petak sudah bersih"),
    "hud.level": ("LV {0}", "LV {0}"),
    "hud.xpGain": ("+{0} XP", "+{0} XP"),
    "hud.levelUp": ("LEVEL {0}", "LEVEL {0}"),
    "hud.levelUpGains": ("Max health +{0}   ·   Attack +{1}   ·   Wounds healed", "HP maks +{0}   ·   Serangan +{1}   ·   Luka pulih"),
    "hud.skillUnlocked": ("New skill: {0}", "Skill baru: {0}"),
    "toast.recovered": ("You wake by the light, bruised but breathing", "Anda terbangun di dekat cahaya, memar tapi masih bernapas"),
    "toast.recoveredCoin": ("You wake by the light  ·  {0} coin lost in the fall", "Anda terbangun di dekat cahaya  ·  {0} koin hilang"),
}

# Fire glows and skeletal watchers layered over the prints, in normalized panel coordinates.
PANEL_FX = {
    "p1_valley": {"glows": [{"x": 0.47, "y": 0.10, "radius": 0.30, "intensity": 0.9}], "emberParticles": True},
    "p2_first_testament": {"glows": [{"x": 0.5, "y": 0.42, "radius": 0.34, "intensity": 0.55}]},
    "p3_burning": {"glows": [{"x": 0.5, "y": 0.45, "radius": 0.55, "intensity": 1.0}], "emberParticles": True},
    "p4_cracked_crown": {"glows": [{"x": 0.5, "y": 0.62, "radius": 0.30, "intensity": 0.7}]},
    "p5_trial": {"glows": [{"x": 0.22, "y": 0.66, "radius": 0.16, "intensity": 0.6}, {"x": 0.78, "y": 0.66, "radius": 0.16, "intensity": 0.6}]},
    "p6_road": {
        "glows": [{"x": 0.5, "y": 0.34, "radius": 0.12, "intensity": 0.8}],
        "watchers": [
            {"x": 0.12, "y": 0.30, "scale": 1.5, "appearAt": 1.0},
            {"x": 0.85, "y": 0.27, "scale": 1.7, "appearAt": 2.2},
            {"x": 0.26, "y": 0.40, "scale": 1.1, "appearAt": 3.4},
            {"x": 0.70, "y": 0.44, "scale": 1.0, "appearAt": 4.6},
            {"x": 0.40, "y": 0.52, "scale": 0.8, "appearAt": 6.0},
        ],
    },
}


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def build_cinematic(data: dict) -> dict[str, tuple[str, str]]:
    strings: dict[str, tuple[str, str]] = {}
    panels = []
    for panel in data["panels"]:
        lines = []
        for index, line in enumerate(panel["lines"], start=1):
            key = f"cine.opening.{panel['id']}.{index}"
            strings[key] = (line["en"], line["id"])
            lines.append({
                "speaker": line["speaker"],
                "textKey": key,
                "voiceClip": line.get("voiceClip", f"{panel['id']}_{index}_{line['speaker']}"),
                "hold": 0.7,
            })
        fx = PANEL_FX.get(panel["id"], {})
        panels.append({
            "id": panel["id"],
            "image": panel["id"],
            "lines": lines,
            "zoomFrom": 1.04,
            "zoomTo": 1.13,
            "panFromY": 0.0,
            "panToY": 0.03,
            "glows": fx.get("glows", []),
            "watchers": fx.get("watchers", []),
            "emberParticles": fx.get("emberParticles", False),
        })

        source = ART / f"{panel['id']}.png"
        if source.exists():
            target = RESOURCES / "Cinematic" / f"{panel['id']}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            # Unity refused to import the RGB prints as sprites; RGBA with block-friendly
            # dimensions imports cleanly and compresses to ASTC on Android.
            panel_image = Image.open(source).convert("RGBA")
            width, height = panel_image.size
            trimmed = ((width // 4) * 4, (height // 4) * 4)
            if trimmed != (width, height):
                panel_image = panel_image.resize(trimmed, Image.LANCZOS)
            panel_image.save(target)

    write_json(RESOURCES / "Cinematic" / "opening.json", {
        "id": "opening",
        "musicClip": "opening_theme",
        "nextScene": "Prologue",
        "panels": panels,
    })
    return strings


def build_dialogues() -> dict[str, tuple[str, str]]:
    """Docs/dialogue_<id>.json holds both languages inline; split it into runtime nodes plus string keys."""
    strings: dict[str, tuple[str, str]] = {}
    for source in sorted(DOCS.glob("dialogue_*.json")):
        data = json.loads(source.read_text(encoding="utf-8"))
        script_id = data["id"]
        nodes = []
        for node in data["nodes"]:
            text_key = f"dlg.{script_id}.{node['id']}"
            strings[text_key] = (node["text"]["en"], node["text"]["id"])
            choices = []
            for index, choice in enumerate(node.get("choices", []), start=1):
                choice_key = f"{text_key}.c{index}"
                strings[choice_key] = (choice["text"]["en"], choice["text"]["id"])
                choices.append({
                    "textKey": choice_key,
                    "next": choice.get("next", ""),
                    "tone": choice.get("tone", ""),
                    "requiresFlag": choice.get("requiresFlag", ""),
                    "hiddenIfFlag": choice.get("hiddenIfFlag", ""),
                    "setFlag": choice.get("setFlag", ""),
                    "faction": choice.get("faction", ""),
                    "reputation": choice.get("reputation", 0),
                    "costCoin": choice.get("costCoin", 0),
                    "action": choice.get("action", ""),
                })
            # A line is voiced simply by its audio existing: Tools/make_dialogue_vo.py writes
            # dlg_<script>_<node>.mp3, and the runtime plays whatever clip name is set here.
            clip = node.get("voiceClip", "")
            if not clip and (RESOURCES / "Voice" / f"dlg_{script_id}_{node['id']}.mp3").exists():
                clip = f"dlg_{script_id}_{node['id']}"
            nodes.append({
                "id": node["id"],
                "speaker": node.get("speaker", ""),
                "textKey": text_key,
                "voiceClip": clip,
                "mood": node.get("mood", ""),
                "setFlags": node.get("setFlags", []),
                "giveItems": node.get("giveItems", []),
                "takeItems": node.get("takeItems", []),
                "giveCoin": node.get("giveCoin", 0),
                "next": node.get("next", ""),
                "choices": choices,
            })
        write_json(RESOURCES / "Dialogue" / f"{script_id}.json", {
            "id": script_id,
            "startNode": data.get("startNode", nodes[0]["id"]),
            "nodes": nodes,
        })
    return strings


# Unity's JsonUtility only reads enums as integers, so authoring names are mapped to the C# enum order.
ITEM_KIND = ["Material", "Consumable", "Weapon", "Armor", "Trinket", "Quest", "Relic"]
EQUIP_SLOT = ["None", "MainHand", "OffHand", "Body", "Head", "Trinket", "Neck", "Hands", "Legs", "Feet", "Ring"]
ITEM_RARITY = ["Common", "Fine", "Rare", "Oathbound"]
OBJECTIVE_KIND = ["Flag", "Counter", "Item"]


def enum_index(names: list[str], value, field: str) -> int:
    if isinstance(value, int):
        return value
    if value in names:
        return names.index(value)
    raise ValueError(f"unknown {field}: {value!r}")


def build_items() -> dict[str, tuple[str, str]]:
    """Docs/items.json carries both languages; split it into the runtime table plus string keys."""
    strings: dict[str, tuple[str, str]] = {}
    data = json.loads((DOCS / "items.json").read_text(encoding="utf-8"))
    items = []
    for item in data["items"]:
        strings[f"item.{item['id']}.name"] = (item["name"]["en"], item["name"]["id"])
        strings[f"item.{item['id']}.desc"] = (item["desc"]["en"], item["desc"]["id"])
        runtime = {key: value for key, value in item.items() if key not in ("name", "desc")}
        runtime["kind"] = enum_index(ITEM_KIND, item["kind"], "kind")
        runtime["slot"] = enum_index(EQUIP_SLOT, item.get("slot", "None"), "slot")
        runtime["rarity"] = enum_index(ITEM_RARITY, item.get("rarity", "Common"), "rarity")
        runtime.setdefault("stackSize", 1)
        items.append(runtime)
    write_json(RESOURCES / "Items" / "items.json", {"items": items})
    return strings


def build_quests() -> dict[str, tuple[str, str]]:
    strings: dict[str, tuple[str, str]] = {}
    for source in sorted(DOCS.glob("quests_*.json")):
        chapter = source.stem.replace("quests_", "")
        data = json.loads(source.read_text(encoding="utf-8"))
        quests = []
        for quest in data["quests"]:
            strings[f"quest.{quest['id']}.title"] = (quest["title"]["en"], quest["title"]["id"])
            strings[f"quest.{quest['id']}.summary"] = (quest["summary"]["en"], quest["summary"]["id"])
            stages = []
            for stage in quest["stages"]:
                strings[f"quest.{quest['id']}.{stage['id']}"] = (stage["text"]["en"], stage["text"]["id"])
                objectives = [{
                    "kind": enum_index(OBJECTIVE_KIND, objective["kind"], "objective kind"),
                    "key": objective["key"],
                    "amount": objective.get("amount", 1),
                    "optional": objective.get("optional", False),
                } for objective in stage["objectives"]]
                stages.append({
                    "id": stage["id"],
                    "objectives": objectives,
                    "setsFlag": stage.get("setsFlag", ""),
                })
            quests.append({
                "id": quest["id"],
                "act": quest.get("act", ""),
                "mainQuest": quest.get("mainQuest", True),
                "unlockFlag": quest.get("unlockFlag", ""),
                "giver": quest.get("giver", ""),
                "giverScene": quest.get("giverScene", ""),
                "stages": stages,
                "reward": quest.get("reward", {}),
            })
        write_json(RESOURCES / "Quests" / f"{chapter}.json", {"quests": quests})
    return strings


CONTRACT_KIND = ["Cull", "Supply", "Labour", "Vigil"]
CONTRACT_RANK = ["Copper", "Iron", "Silver", "Oathbound"]


def build_contracts() -> dict[str, tuple[str, str]]:
    """Docs/contracts.json carries both languages; split into the runtime board table plus strings."""
    strings: dict[str, tuple[str, str]] = {}
    data = json.loads((DOCS / "contracts.json").read_text(encoding="utf-8"))
    contracts = []
    for contract in data["contracts"]:
        cid = contract["id"]
        strings[f"contract.{cid}.title"] = (contract["title"]["en"], contract["title"]["id"])
        strings[f"contract.{cid}.body"] = (contract["body"]["en"], contract["body"]["id"])
        strings[f"contract.{cid}.client"] = (contract["client"]["en"], contract["client"]["id"])
        runtime = {key: value for key, value in contract.items() if key not in ("title", "body", "client")}
        runtime["kind"] = enum_index(CONTRACT_KIND, contract["kind"], "contract kind")
        runtime["rank"] = enum_index(CONTRACT_RANK, contract.get("rank", "Copper"), "contract rank")
        contracts.append(runtime)
    write_json(RESOURCES / "Contracts" / "board.json", {"contracts": contracts})
    return strings


WORLD_EVENT_KIND = ["Visitor", "Apparition"]
WORLD_EVENT_PHASE = ["Any", "Day", "Night"]


def build_shops() -> dict[str, tuple[str, str]]:
    """Docs/shops.json -> Resources/Shops/shops.json plus each shop's name."""
    source = DOCS / "shops.json"
    if not source.exists():
        return {}
    data = json.loads(source.read_text(encoding="utf-8"))
    strings: dict[str, tuple[str, str]] = {}
    shops = []
    for shop in data["shops"]:
        key = f"shop.{shop['id']}.name"
        strings[key] = (shop["name"]["en"], shop["name"]["id"])
        shops.append({"id": shop["id"], "nameKey": key, "sellRate": shop.get("sellRate", 0.35), "stock": shop["stock"]})
    write_json(RESOURCES / "Shops" / "shops.json", {"shops": shops})
    return strings


def build_skills() -> dict[str, tuple[str, str]]:
    """Docs/skills.json -> Resources/Skills/skills.json plus each skill's name and description."""
    source = DOCS / "skills.json"
    if not source.exists():
        return {}
    data = json.loads(source.read_text(encoding="utf-8"))
    strings: dict[str, tuple[str, str]] = {}
    rows = []
    for skill in data["skills"]:
        strings[f"skill.{skill['id']}.name"] = (skill["name"]["en"], skill["name"]["id"])
        strings[f"skill.{skill['id']}.desc"] = (skill["desc"]["en"], skill["desc"]["id"])
        rows.append({"id": skill["id"], "level": skill["level"], "ability": skill["ability"]})
    write_json(RESOURCES / "Skills" / "skills.json", {"skills": rows})
    return strings


def build_world_events() -> dict[str, tuple[str, str]]:
    """Docs/world_events.json holds the banner text in both languages; split it off like everything else."""
    strings: dict[str, tuple[str, str]] = {}
    data = json.loads((DOCS / "world_events.json").read_text(encoding="utf-8"))
    events = []
    for event in data["events"]:
        banner_key = f"event.{event['id']}.banner"
        strings[banner_key] = (event["banner"]["en"], event["banner"]["id"])
        runtime = {key: value for key, value in event.items() if key != "banner"}
        runtime["kind"] = enum_index(WORLD_EVENT_KIND, event["kind"], "event kind")
        runtime["phase"] = enum_index(WORLD_EVENT_PHASE, event.get("phase", "Any"), "event phase")
        runtime["bannerKey"] = banner_key
        events.append(runtime)
    write_json(RESOURCES / "Events" / "world.json", {"events": events})
    return strings


def build_encounters() -> None:
    """Wandering-encounter tuning is copied through as-is; only the banner text needs localizing."""
    data = json.loads((DOCS / "encounters.json").read_text(encoding="utf-8"))
    write_json(RESOURCES / "Events" / "encounters.json", data)


def build_quest_guides() -> None:
    """Where the quest arrow points for each objective. Copied through; checked against the quest keys."""
    data = json.loads((DOCS / "quest_guides.json").read_text(encoding="utf-8"))
    data.pop("_comment", None)
    known = set()
    for path in DOCS.glob("quests_*.json"):
        for quest in json.loads(path.read_text(encoding="utf-8"))["quests"]:
            for stage in quest["stages"]:
                known.update(objective["key"] for objective in stage["objectives"])
    stray = [guide["key"] for guide in data["guides"] if guide["key"] not in known]
    if stray:
        raise SystemExit(f"quest_guides.json names objectives no quest has: {stray}")
    write_json(RESOURCES / "Quests" / "guides.json", data)


def build_localization(strings: dict[str, tuple[str, str]]) -> None:
    for index, language in enumerate(("en", "id")):
        entries = [{"key": key, "value": value[index]} for key, value in sorted(strings.items())]
        write_json(RESOURCES / "Localization" / f"{language}.json", {"entries": entries})


def build_fx_sprites() -> None:
    out = PROJECT / "Assets" / "Oathfire" / "Art" / "FX"
    out.mkdir(parents=True, exist_ok=True)

    size = 256
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pixels = glow.load()
    center = size / 2
    for y in range(size):
        for x in range(size):
            distance = ((x - center) ** 2 + (y - center) ** 2) ** 0.5 / center
            if distance >= 1:
                continue
            falloff = (1 - distance) ** 2.4
            pixels[x, y] = (255, 255, 255, int(255 * falloff))
    glow.save(out / "glow.png")

    ember = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    pixels = ember.load()
    for y in range(32):
        for x in range(32):
            distance = ((x - 16) ** 2 + (y - 16) ** 2) ** 0.5 / 16
            if distance < 1:
                pixels[x, y] = (255, 255, 255, int(255 * (1 - distance) ** 1.5))
    ember.save(out / "ember.png")

    # Damage vignette: clear in the middle, soft red bleed at the edges.
    size = 512
    vignette = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pixels = vignette.load()
    centre = size / 2
    for y in range(size):
        for x in range(size):
            distance = max(abs(x - centre), abs(y - centre)) / centre
            edge = max(0.0, (distance - 0.45) / 0.55)
            pixels[x, y] = (255, 255, 255, int(255 * min(1.0, edge ** 1.6)))
    vignette.save(out / "vignette.png")

    build_watcher_silhouette(out / "watcher.png")


def build_watcher_silhouette(target: Path) -> None:
    """Cuts one idle frame from the tileset's skeleton sheet and flattens it to an ink silhouette."""
    meta = SKELETON_SHEET.with_suffix(".png.meta")
    if not SKELETON_SHEET.exists() or not meta.exists():
        print(f"warning: skeleton sheet missing at {SKELETON_SHEET}")
        return
    rects = re.findall(
        r"name: (\S+)\s+rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)",
        meta.read_text(encoding="utf-8", errors="ignore"),
    )
    sheet = Image.open(SKELETON_SHEET).convert("RGBA")
    if not rects:
        frame = sheet
    else:
        _, x, y, w, h = rects[0]
        x, y, w, h = (float(v) for v in (x, y, w, h))
        top = sheet.height - y - h
        frame = sheet.crop((int(x), int(top), int(x + w), int(top + h)))

    silhouette = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    source = frame.load()
    out = silhouette.load()
    for py in range(frame.height):
        for px in range(frame.width):
            alpha = source[px, py][3]
            if alpha > 40:
                out[px, py] = (255, 255, 255, alpha)
    silhouette = silhouette.crop(silhouette.getbbox())
    silhouette.save(target)


def main() -> None:
    data = json.loads((DOCS / "opening_cinematic.json").read_text(encoding="utf-8"))
    strings = build_cinematic(data)
    strings.update(build_dialogues())
    strings.update(build_items())
    strings.update(build_quests())
    strings.update(build_contracts())
    strings.update(build_world_events())
    strings.update(build_skills())
    strings.update(build_shops())
    build_encounters()
    build_quest_guides()
    strings.update(UI_STRINGS)
    build_localization(strings)
    build_fx_sprites()
    print(f"content built: {len(data['panels'])} panels, {len(strings)} strings")


if __name__ == "__main__":
    main()
