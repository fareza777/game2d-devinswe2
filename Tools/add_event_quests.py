"""Side quests that only exist because of how a scripted world event landed.

Each unlocks from a flag the events set, and every objective reads a counter the game already writes,
so nothing here needs bespoke tracking.
"""
import json
from pathlib import Path

DOCS = Path(__file__).resolve().parent.parent / "Docs"


def quest(quest_id, title_en, title_id, summary_en, summary_id, stages, reward, unlock):
    return {
        "id": quest_id, "act": "act1", "mainQuest": False, "unlockFlag": unlock,
        "title": {"en": title_en, "id": title_id},
        "summary": {"en": summary_en, "id": summary_id},
        "stages": stages, "reward": reward,
    }


def stage(stage_id, text_en, text_id, objectives, flag):
    return {"id": stage_id, "text": {"en": text_en, "id": text_id}, "objectives": objectives, "setsFlag": flag}


EVENT_QUESTS = [
    quest("sq_surveyors_eye", "What the Surveyors Will See", "Apa yang Akan Dilihat Juru Ukur",
          "The ledger went east, so men with chains and charts will come west. Let them find a settlement that looks too useful to break up.",
          "Daftar itu pergi ke timur, jadi orang-orang dengan rantai ukur dan peta akan datang ke barat. Biar mereka menemukan pemukiman yang terlihat terlalu berguna untuk dibubarkan.",
          [stage("s1", "Raise six roofs inside the hearth light", "Dirikan enam atap di dalam cahaya perapian",
                 [{"kind": "Counter", "key": "built.shelter", "amount": 6}], "sq.surveyors.built"),
           stage("s2", "Clear four posted contracts so the board reads busy",
                 "Selesaikan empat kontrak agar papan terlihat sibuk",
                 [{"kind": "Counter", "key": "contracts.completed", "amount": 4}], "sq.surveyors.done")],
          {"experience": 480, "coin": 200, "items": ["armor_guard_mail"], "faction": "crown", "reputation": 12},
          unlock="act2.surveyors_coming"),

    quest("sq_what_nessa_carried", "What Nessa Carried", "Apa yang Dibawa Nessa",
          "She arrived with a wound, two coins on a wire, and the names of everyone who did not. Outfit the next ones before they get here.",
          "Ia datang dengan luka, dua koin berkawat, dan nama semua orang yang tak sampai. Siapkan perlengkapan untuk yang berikutnya sebelum mereka tiba.",
          [stage("s1", "Stock oiled cloth for bandages and roofing",
                 "Sediakan kain berminyak untuk perban dan atap",
                 [{"kind": "Item", "key": "mat_cloth", "amount": 6}], "sq.nessa.cloth"),
           stage("s2", "Put salt meat in the stores for the road-worn",
                 "Isi gudang dengan daging asin untuk mereka yang lelah di jalan",
                 [{"kind": "Item", "key": "food_salt_meat", "amount": 4}], "sq.nessa.done")],
          {"experience": 300, "coin": 110, "items": ["trinket_road_charm"], "faction": "hearth", "reputation": 14},
          unlock="act1.refugee_sheltered"),

    quest("sq_shepherds_distance", "The Distance He Keeps", "Jarak yang Ia Jaga",
          "It stood at the treeline and did not come in. Whatever decides that is worth making nervous.",
          "Ia berdiri di tepi hutan dan tak melangkah masuk. Apa pun yang memutuskan itu pantas dibuat gelisah.",
          [stage("s1", "Put down three of the ones that lead", "Tumbangkan tiga yang memimpin mereka",
                 [{"kind": "Counter", "key": "kills.elite", "amount": 3}], "sq.shepherd.elites"),
           stage("s2", "Hold the valley two nights more", "Pertahankan lembah dua malam lagi",
                 [{"kind": "Counter", "key": "nights.survived", "amount": 3}], "sq.shepherd.done")],
          {"experience": 600, "coin": 240, "items": ["trinket_ashcharm"], "faction": "hearth", "reputation": 10},
          unlock="event.shepherd_sighting.done"),
]


def main() -> None:
    path = DOCS / "quests_chapter1.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    existing = {entry["id"] for entry in data["quests"]}
    added = [entry for entry in EVENT_QUESTS if entry["id"] not in existing]
    data["quests"].extend(added)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    stages = sum(len(entry["stages"]) for entry in data["quests"])
    print(f"added {len(added)} event quests; chapter now has {len(data['quests'])} quests / {stages} stages")


if __name__ == "__main__":
    main()
