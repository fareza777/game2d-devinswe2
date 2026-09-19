"""Adds the items the scripted world events hand over.

Each one exists because a scene gives it: the Marshal's writ, the crate Vesna sells off her manifest,
and what a refugee still has in her pockets after four days on the road.
"""
import json
from pathlib import Path

DOCS = Path(__file__).resolve().parent.parent / "Docs"

NEW_ITEMS = [
    {
        "id": "quest_marshal_writ", "kind": "Quest", "rarity": "Rare", "value": 0,
        "setsFlag": "quest.marshal_writ",
        "name": {"en": "Writ of Compliance", "id": "Surat Kepatuhan"},
        "desc": {
            "en": "Stamped twice and signed once. It says Rennfall cooperates, which is a shield until it is a receipt.",
            "id": "Dicap dua kali, ditandatangani sekali. Isinya: Rennfall bekerja sama — sebuah perisai, sampai ia jadi tanda terima.",
        },
    },
    {
        "id": "trinket_road_charm", "kind": "Trinket", "rarity": "Common", "value": 55,
        "slot": "Trinket", "health": 8, "armor": 1,
        "name": {"en": "Road Charm", "id": "Jimat Jalan"},
        "desc": {
            "en": "Two coins wired together, carried four days out of Greymarch by someone who arrived and someone who did not.",
            "id": "Dua keping koin diikat kawat, dibawa empat hari dari Greymarch oleh satu orang yang sampai dan satu yang tidak.",
        },
    },
    {
        "id": "food_salt_meat", "kind": "Consumable", "rarity": "Common", "value": 18,
        "stackSize": 10, "healAmount": 60,
        "name": {"en": "Salt Meat", "id": "Daging Asin"},
        "desc": {
            "en": "League-cured and League-priced. Chews like rope, keeps a watchman upright until dawn.",
            "id": "Diawetkan Serikat dan dihargai Serikat. Alot seperti tali, tapi menjaga penjaga tetap tegak sampai fajar.",
        },
    },
    {
        "id": "mat_cloth", "kind": "Material", "rarity": "Common", "value": 11, "stackSize": 99,
        "name": {"en": "Oiled Cloth", "id": "Kain Berminyak"},
        "desc": {
            "en": "Sheds rain off a roof or off a wound. The settlement never has enough of either use.",
            "id": "Menahan hujan di atap atau di luka. Pemukiman tak pernah punya cukup untuk keduanya.",
        },
    },
]


def main() -> None:
    path = DOCS / "items.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    existing = {item["id"] for item in data["items"]}
    added = [item for item in NEW_ITEMS if item["id"] not in existing]
    data["items"].extend(added)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"added {len(added)} items; catalogue now holds {len(data['items'])}")


if __name__ == "__main__":
    main()
