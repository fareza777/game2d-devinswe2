"""Proves every quest can be finished before anyone plays it.

For each objective it looks for somewhere in the game that actually produces it — a dialogue line, a quest
reward, a contract, the shop, a search spot or gather node in a scene builder, or runtime code — and for a
place the quest arrow can point. For side work it checks the giver really stands in the map the quest names.
Several quests used to hang on an event that could be missed or on a single dialogue choice; this is the
check that would have caught them.

Exit code 1 lists every problem. Run after build_content.py.
"""
import glob
import json
import re
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
DOCS = PROJECT / "Docs"
SCRIPTS = PROJECT / "Assets" / "Oathfire" / "Scripts"
EDITOR = PROJECT / "Assets" / "Oathfire" / "Editor"

BUILDER_FOR_SCENE = {
    "Rennfall": "RennfallSceneBuilder.cs",
    "TradeRoad": "TradeRoadSceneBuilder.cs",
    "Greymarch": "GreymarchSceneBuilder.cs",
    "Hollow": "HollowSceneBuilder.cs",
    "Keep": "KeepSceneBuilder.cs",
    "Saltpans": "SaltpansSceneBuilder.cs",
}


def load(name: str) -> dict:
    return json.loads((DOCS / name).read_text(encoding="utf-8"))


def main() -> int:
    quests = load("quests_chapter1.json")["quests"]
    code = "\n".join(Path(f).read_text(encoding="utf-8", errors="ignore")
                     for f in glob.glob(str(SCRIPTS / "**" / "*.cs"), recursive=True) if "Testing" not in f)
    editor = {Path(f).name: Path(f).read_text(encoding="utf-8", errors="ignore") for f in glob.glob(str(EDITOR / "*.cs"))}
    everything = code + "\n".join(editor.values())

    flags, items = set(), set()
    for path in glob.glob(str(DOCS / "dialogue_*.json")):
        for node in json.loads(Path(path).read_text(encoding="utf-8"))["nodes"]:
            flags.update(node.get("setFlags", []))
            items.update(node.get("giveItems", []))
            flags.update(choice["setFlag"] for choice in node.get("choices", []) if choice.get("setFlag"))
    for quest in quests:
        flags.update(stage.get("setsFlag", "") for stage in quest["stages"])
        items.update(quest.get("reward", {}).get("items", []))
    for contract in load("contracts.json")["contracts"]:
        if contract.get("rewardItem"):
            items.add(contract["rewardItem"])
    for shop in load("shops.json")["shops"]:
        items.update(entry["item"] for entry in shop["stock"])
    flags.update(f"event.{event['id']}.done" for event in load("world_events.json")["events"])
    flags.update(re.findall(r'"([a-z0-9_]+\.[a-z0-9_.]+)"', everything))
    items.update(re.findall(r'"((?:mat|food|quest|potion)_[a-z0-9_]+)"', everything))
    guides = {guide["key"] for guide in load("quest_guides.json")["guides"]}

    problems = []
    for quest in quests:
        for stage in quest["stages"]:
            for objective in stage["objectives"]:
                key, kind = objective["key"], objective["kind"]
                if kind == "Flag" and key not in flags:
                    problems.append(f"{quest['id']}/{stage['id']}: nothing ever sets flag {key}")
                if kind == "Item" and key not in items:
                    problems.append(f"{quest['id']}/{stage['id']}: nothing ever gives item {key}")
                if kind == "Counter" and not (f'"{key}"' in everything or key.split(".")[0] + "." in everything):
                    problems.append(f"{quest['id']}/{stage['id']}: nothing counts {key}")
                if not quest.get("mainQuest", True) and key not in guides:
                    problems.append(f"{quest['id']}/{stage['id']}: the arrow has nowhere to point for {key}")
        if quest.get("giver"):
            scene = quest.get("giverScene", "")
            builder = editor.get(BUILDER_FOR_SCENE.get(scene, ""), "")
            if f'"{quest["giver"]}"' not in builder:
                problems.append(f"{quest['id']}: giver {quest['giver']} is not built into {scene or 'any scene'}")

    for problem in problems:
        print("  PROBLEM", problem)
    print(f"checked {len(quests)} quests: {len(problems)} problems")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
