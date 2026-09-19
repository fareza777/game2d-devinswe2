"""Authoring pass that adds Chapter 1's side quests to Docs/quests_chapter1.json.

Each one is written to pay off a character or a piece of the world's rules, not to pad a checklist:
objectives reuse the counters the game already writes (kills by type, buildings raised, nights survived,
contracts completed, materials gathered), so nothing needs bespoke tracking.
"""
import json
from pathlib import Path

DOCS = Path(__file__).resolve().parent.parent / "Docs"


def quest(quest_id, title_en, title_id, summary_en, summary_id, stages, reward, unlock=None):
    return {
        "id": quest_id, "act": "act1", "mainQuest": False, "unlockFlag": unlock or "",
        "title": {"en": title_en, "id": title_id},
        "summary": {"en": summary_en, "id": summary_id},
        "stages": stages, "reward": reward,
    }


def stage(stage_id, text_en, text_id, objectives, flag):
    return {"id": stage_id, "text": {"en": text_en, "id": text_id}, "objectives": objectives, "setsFlag": flag}


SIDE_QUESTS = [
    quest("sq_tally", "Sixty Years of Names", "Enam Puluh Tahun Nama",
          "Brann has kept a tally stick for every burial in Rennfall. He wants the last twelve names carved before he forgets them.",
          "Brann menyimpan kayu hitungan untuk setiap penguburan di Rennfall. Ia ingin dua belas nama terakhir diukir sebelum ia melupakannya.",
          [stage("s1", "Put down twelve of the risen so Brann can name them",
                 "Tumbangkan dua belas mayat bangkit agar Brann bisa menamai mereka",
                 [{"kind": "Counter", "key": "kills.skeleton", "amount": 12}], "sq.tally.killed"),
           stage("s2", "Bring Brann the tally stick", "Bawakan Brann kayu hitungannya",
                 [{"kind": "Item", "key": "trinket_tally", "amount": 1}], "sq.tally.done")],
          {"experience": 220, "coin": 40, "items": ["tool_shovel"], "faction": "hearth", "reputation": 8},
          unlock="act1.first_fire_done"),

    quest("sq_stew", "Something Hot", "Sesuatu yang Hangat",
          "Nobody in the settlement has eaten a hot meal since the road. Stock the cookpot and the refugees will remember it longer than the walls.",
          "Tak seorang pun di pemukiman makan makanan hangat sejak di perjalanan. Isi panci masak, dan para pengungsi akan mengingatnya lebih lama daripada dinding.",
          [stage("s1", "Deliver hearth stew to the cookpot", "Antarkan sup perapian ke panci masak",
                 [{"kind": "Item", "key": "food_stew", "amount": 5}], "sq.stew.done")],
          {"experience": 120, "coin": 30, "faction": "hearth", "reputation": 6},
          unlock="act1.first_fire_done"),

    quest("sq_roofline", "Before the Rain", "Sebelum Hujan",
          "The sky has that grey weight to it. Every roof raised before the storm is a family that does not sleep in mud.",
          "Langit terasa kelabu dan berat. Setiap atap yang berdiri sebelum badai berarti satu keluarga yang tak tidur di lumpur.",
          [stage("s1", "Raise four buildings inside the hearth light", "Bangun empat bangunan di dalam cahaya perapian",
                 [{"kind": "Counter", "key": "built.shelter", "amount": 4}], "sq.roofline.built"),
           stage("s2", "Stock the stores with pitch and rope", "Isi gudang dengan getah dan tali",
                 [{"kind": "Item", "key": "mat_pitch", "amount": 4}, {"kind": "Item", "key": "mat_rope", "amount": 6}],
                 "sq.roofline.done")],
          {"experience": 260, "coin": 70, "items": ["armor_refugee"], "faction": "hearth", "reputation": 10},
          unlock="act1.first_fire_done"),

    quest("sq_whisperers", "The Ones That Chant", "Yang Merapal",
          "Inspector Maren wants the chanting dead counted and burned. Iskra wants one kept whole. You cannot do both.",
          "Inspektur Maren ingin mayat perapal dihitung dan dibakar. Iskra ingin satu disisakan utuh. Kau tak bisa melakukan keduanya.",
          [stage("s1", "Put down six of the chanting dead", "Tumbangkan enam mayat perapal",
                 [{"kind": "Counter", "key": "kills.mage", "amount": 6}], "sq.whisperers.killed"),
           stage("s2", "Decide what happens to the last one", "Tentukan nasib yang terakhir",
                 [{"kind": "Flag", "key": "sq.whisperers.choice"}], "sq.whisperers.done")],
          {"experience": 340, "coin": 90, "items": ["potion_mana", "mat_bone_dust"]},
          unlock="act1.maren_arrived"),

    quest("sq_unmarked_boots", "Unmarked Boots", "Sepatu Tanpa Lambang",
          "The bandits on the trade road wear boots stamped by a Greymarch cobbler. Vesna will pay for proof, and Harrow will pay more for silence.",
          "Bandit di jalan dagang memakai sepatu bercap tukang sepatu Greymarch. Vesna membayar untuk bukti, dan Harrow membayar lebih untuk diam.",
          [stage("s1", "Recover the Greymarch supply ledger", "Temukan buku perbekalan Greymarch",
                 [{"kind": "Item", "key": "quest_harrow_ledger", "amount": 1}], "sq.boots.ledger"),
           stage("s2", "Decide who receives the evidence", "Tentukan siapa yang menerima buktinya",
                 [{"kind": "Flag", "key": "sq.boots.delivered"}], "sq.boots.done")],
          {"experience": 400, "coin": 180, "items": ["trinket_salt_seal"]},
          unlock="act2.bandits_hinted"),

    quest("sq_iron_seam", "The Seam Nobody Works", "Urat Besi yang Tak Digarap",
          "There is good iron under the north slope, and a reason the old villagers stopped digging it. Find out which is worth more.",
          "Ada besi bagus di lereng utara, dan ada alasan warga lama berhenti menggalinya. Cari tahu mana yang lebih berharga.",
          [stage("s1", "Mine iron from the north seam", "Tambang besi dari urat utara",
                 [{"kind": "Item", "key": "mat_iron", "amount": 10}], "sq.seam.mined"),
           stage("s2", "Clear whatever the digging woke", "Bersihkan apa pun yang terbangun oleh galian",
                 [{"kind": "Counter", "key": "kills.knight", "amount": 4}], "sq.seam.done")],
          {"experience": 380, "coin": 140, "items": ["axe_grave"], "faction": "salt", "reputation": 8},
          unlock="act1.first_night_done"),

    quest("sq_flawless_vigil", "A Night Without a Wound", "Semalam Tanpa Luka",
          "Brann claims two wardens ever held a night untouched. He is not saying it to flatter you.",
          "Brann bilang hanya dua warden yang pernah bertahan semalam tanpa tergores. Ia tidak sedang memujimu.",
          [stage("s1", "Hold a night without taking a single wound", "Bertahan semalam tanpa terluka sekali pun",
                 [{"kind": "Counter", "key": "night.flawless", "amount": 1}], "sq.vigil.done")],
          {"experience": 450, "coin": 160, "items": ["trinket_ember_knot"], "faction": "hearth", "reputation": 12},
          unlock="act1.night1_cleared"),

    quest("sq_reputation_board", "The Warden's Word", "Ucapan Sang Warden",
          "A settlement is judged by whether its posted work gets done. Clear the board and word travels further than any banner.",
          "Sebuah pemukiman dinilai dari apakah pekerjaan yang ditempel benar-benar selesai. Bersihkan papan, dan kabarnya menyebar lebih jauh daripada panji mana pun.",
          [stage("s1", "Complete five posted contracts", "Selesaikan lima kontrak yang ditempel",
                 [{"kind": "Counter", "key": "contracts.completed", "amount": 5}], "sq.board.five"),
           stage("s2", "Survive three nights as Warden of Rennfall", "Bertahan tiga malam sebagai Warden Rennfall",
                 [{"kind": "Counter", "key": "nights.survived", "amount": 3}], "sq.board.done")],
          {"experience": 520, "coin": 220, "items": ["armor_warden_coat", "helm_warden"], "faction": "hearth", "reputation": 15},
          unlock="act1.first_night_done"),
]


def main() -> None:
    path = DOCS / "quests_chapter1.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    existing = {entry["id"] for entry in data["quests"]}
    added = [entry for entry in SIDE_QUESTS if entry["id"] not in existing]
    data["quests"].extend(added)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    stages = sum(len(entry["stages"]) for entry in data["quests"])
    print(f"added {len(added)} side quests; chapter now has {len(data['quests'])} quests / {stages} stages")


if __name__ == "__main__":
    main()
