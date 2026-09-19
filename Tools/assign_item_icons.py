"""Gives every item an icon from the pack's LootIcons sheet.

The sheet has 106 painted icons whose sprite names are just numbers. They were read off a labelled contact
sheet and each item is matched by what it actually is — a falchion gets a blade, a sallet a helm, bone dust
a pale heap — with the material tier chosen by rarity where the sheet offers wood, iron and gold versions.

Writes the `icon` field into Docs/items.json. Run build_content.py afterwards.
"""
import json
from pathlib import Path

DOCS = Path(__file__).resolve().parent.parent / "Docs"

# Sheet indices, grouped by what they depict.
ICONS = {
    # weapons, by material tier: wood/common, iron/fine, gold/rare-and-above
    "sword": {"Common": 101, "Fine": 80, "Rare": 86, "Oathbound": 86},
    "axe": {"Common": 100, "Fine": 81, "Rare": 87, "Oathbound": 87},
    "wooden_sword": 97,
    "spear": 3,          # a long shaft; the sheet has no polearm, and the branch reads as a haft
    "bow": 44,           # the pale curved branch
    "staff": 3,
    "dagger": 94,
    # armour, by tier: leather, iron, gold
    "chest": {"Common": 91, "Fine": 77, "Rare": 104, "Oathbound": 83},
    "helm": {"Common": 92, "Fine": 78, "Rare": 98, "Oathbound": 84},
    "hood": 60,          # folded cloth
    "hat": 92,
    "shield": {"Common": 89, "Fine": 88, "Rare": 82, "Oathbound": 90},
    "cloak": 103,
    # trinkets and relics
    "charm": 32,         # small blue crystal
    "ring": 45,          # pale gem
    "seal": 19,          # green gem
    "coal": 11,
    "crown_shard": 12,   # bright crystal
    "pin": 41,
    "knot": 4,           # ember-red crystal
    "tally": 44,
    "shovel": 100,
    "lantern": 0,        # a pale crystal reads as light; the sheet has no lantern
    # consumables
    "health_potion": 4,  # red crystal: the pack has no flasks, and red reads as healing
    "mana_potion": 12,   # blue crystal
    "tonic": 19,
    "bread": 24,
    "stew": 33,
    "meat": 24,
    "bandage": 60,
    "flask": 31,
    "whetstone": 40,
    "torch": 25,
    # materials
    "timber": 30,
    "stone": 16,
    "iron": 10,
    "bone_dust": 61,
    "wax": 74,
    "rope": 1,
    "nails": 8,
    "pitch": 34,
    "hide": 20,
    "salt": 49,
    "cloth": 60,
    # documents
    "scroll": 28,
    "writ": 66,
}


def tiered(key: str, rarity: str) -> int:
    value = ICONS[key]
    return value[rarity] if isinstance(value, dict) else value


def icon_for(item: dict) -> int | None:
    """Matches on the id, which says what the thing is, before falling back to its kind."""
    item_id, rarity = item["id"], item.get("rarity", "Common")
    rules = [
        (("sword_",), lambda: tiered("sword", rarity)),
        (("axe_",), lambda: tiered("axe", rarity)),
        (("spear_",), lambda: ICONS["spear"]),
        (("bow_",), lambda: ICONS["bow"]),
        (("staff_",), lambda: ICONS["staff"]),
        (("dagger_",), lambda: ICONS["dagger"]),
        (("armor_",), lambda: tiered("chest", rarity)),
        (("helm_hood", "helm_marshal"), lambda: ICONS["hood"]),
        (("helm_warden",), lambda: ICONS["hat"]),
        (("helm_",), lambda: tiered("helm", rarity)),
        (("shield_",), lambda: tiered("shield", rarity)),
        (("trinket_ashcharm", "trinket_road_charm"), lambda: ICONS["charm"]),
        (("trinket_root_ring",), lambda: ICONS["ring"]),
        (("trinket_queens_seal", "trinket_salt_seal", "trinket_crane_seal"), lambda: ICONS["seal"]),
        (("trinket_lion_pin",), lambda: ICONS["pin"]),
        (("trinket_ember_knot",), lambda: ICONS["knot"]),
        (("trinket_tally",), lambda: ICONS["tally"]),
        (("tool_shovel",), lambda: ICONS["shovel"]),
        (("tool_lantern",), lambda: ICONS["lantern"]),
        (("relic_hearth_coal",), lambda: ICONS["coal"]),
        (("relic_root_crown",), lambda: ICONS["crown_shard"]),
        (("potion_mana",), lambda: ICONS["mana_potion"]),
        (("potion_stoneskin", "potion_nightsight"), lambda: ICONS["tonic"]),
        (("potion_",), lambda: ICONS["health_potion"]),
        (("food_bread",), lambda: ICONS["bread"]),
        (("food_stew",), lambda: ICONS["stew"]),
        (("food_salt_meat",), lambda: ICONS["meat"]),
        (("bandage",), lambda: ICONS["bandage"]),
        (("oil_flask",), lambda: ICONS["flask"]),
        (("whetstone",), lambda: ICONS["whetstone"]),
        (("torch",), lambda: ICONS["torch"]),
        (("mat_timber",), lambda: ICONS["timber"]),
        (("mat_stone",), lambda: ICONS["stone"]),
        (("mat_iron",), lambda: ICONS["iron"]),
        (("mat_bone_dust",), lambda: ICONS["bone_dust"]),
        (("mat_grave_wax",), lambda: ICONS["wax"]),
        (("mat_rope",), lambda: ICONS["rope"]),
        (("mat_nails",), lambda: ICONS["nails"]),
        (("mat_pitch",), lambda: ICONS["pitch"]),
        (("mat_hide",), lambda: ICONS["hide"]),
        (("mat_saltbrick",), lambda: ICONS["salt"]),
        (("mat_cloth",), lambda: ICONS["cloth"]),
        (("quest_testament", "quest_crane_journal", "quest_harrow_ledger", "quest_burn_ledger"), lambda: ICONS["scroll"]),
        (("quest_",), lambda: ICONS["writ"]),
    ]
    for prefixes, pick in rules:
        if item_id.startswith(prefixes):
            return pick()
    return None


def main() -> None:
    path = DOCS / "items.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    unmatched = []
    for item in data["items"]:
        index = icon_for(item)
        if index is None:
            unmatched.append(item["id"])
            continue
        item["icon"] = f"LootIcons_{index}"
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    matched = len(data["items"]) - len(unmatched)
    print(f"icons assigned: {matched}/{len(data['items'])}")
    if unmatched:
        print("no icon for:", unmatched)


if __name__ == "__main__":
    main()
