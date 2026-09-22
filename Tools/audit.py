#!/usr/bin/env python3
"""Oathfire mega-audit: strings, charset, flags, assets, walkability, guides.

Run from repo root:  python3 Tools/audit.py
Each section prints PASS or lists concrete problems; exit code = number of failing sections.
"""
import json, re, sys, glob
from pathlib import Path
from collections import deque

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "Docs"
RES = ROOT / "Assets" / "Oathfire" / "Resources"
ART = ROOT / "Assets" / "Oathfire" / "Art"
SCENES = ROOT / "Assets" / "Oathfire" / "Scenes"
PACK = ROOT / "Assets" / "SmallScaleInt"

# Font atlas covers ASCII plus this set (see AGENTS.md).
EXTRA_CHARS = set("·—–‘’“”…×–—…«»°†‡‹›")
ACCENTED = "àáâäèéêëìíîïòóôöùúûüçñÿÀÁÂÄÈÉÊËÌÍÎÏÒÓÔÖÙÚÛÜÇÑßœŒæÆ"
ALLOWED = set(chr(c) for c in range(32, 127)) | EXTRA_CHARS | set(ACCENTED) | {"\n", "\t"}

fail_sections = []

def section(name, problems, infos=None):
    if problems:
        fail_sections.append(name)
        print(f"\n### {name}: {len(problems)} problem(s)")
        for p in problems[:40]:
            print(f"  - {p}")
        if len(problems) > 40:
            print(f"  ... +{len(problems)-40} more")
    else:
        print(f"### {name}: PASS")
    for i in (infos or []):
        print(f"    (info) {i}")

def load(path):
    try:
        return json.load(open(path, encoding="utf-8"))
    except Exception as e:
        return {"__error__": str(e)}

def walk_strings(obj, prefix=""):
    if isinstance(obj, dict):
        if set(obj.keys()) == {"id", "en"}:
            yield prefix, obj
        else:
            for k, v in obj.items():
                yield from walk_strings(v, f"{prefix}.{k}" if prefix else k)
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            yield from walk_strings(v, f"{prefix}[{i}]")

# ---------- 1. localization parity + charset + empties ----------
def entries(path):
    d = load(path)
    if isinstance(d, dict) and "entries" in d:
        return {e["key"]: e["value"] for e in d["entries"] if isinstance(e, dict) and "key" in e}
    return d if isinstance(d, dict) else {}

en = entries(RES / "Localization" / "en.json")
idn = entries(RES / "Localization" / "id.json")
problems = []
if en and idn:
    ek, ik = set(en), set(idn)
    for k in sorted(ek - ik):
        problems.append(f"key in en but not id: {k}")
    for k in sorted(ik - ek):
        problems.append(f"key in id but not en: {k}")
    for k in sorted(ek & ik):
        ev, iv = str(en[k]), str(idn[k])
        if not ev.strip() or not iv.strip():
            problems.append(f"empty string: {k}")
            continue
        ph_en = sorted(set(re.findall(r"\{(\d+)\}", ev)))
        ph_id = sorted(set(re.findall(r"\{(\d+)\}", iv)))
        if ph_en != ph_id:
            problems.append(f"placeholder mismatch {k}: en{ph_en} vs id{ph_id}")
        bad = sorted(c for c in set(ev + iv) if c not in ALLOWED)
        if bad:
            problems.append(f"off-atlas char(s) {bad!r} in {k}")
section("localization parity/charset", problems)

# ---------- 2. docs json bilingual pairs ----------
problems = []
for f in sorted(DOCS.glob("*.json")):
    data = load(f)
    if "__error__" in data:
        problems.append(f"{f.name}: unparseable {data['__error__']}")
        continue
    for path, pair in walk_strings(data):
        ev, iv = str(pair.get("en", "")), str(pair.get("id", ""))
        if not ev.strip() or not iv.strip():
            problems.append(f"{f.name}:{path} empty side")
        bad = sorted(c for c in set(ev + iv) if c not in ALLOWED)
        if bad:
            problems.append(f"{f.name}:{path} off-atlas {bad!r}")
section("docs bilingual", problems)

# ---------- 3. C# Get()/Format() keys exist ----------
problems = []
cs_keys = set()
for cs in (ROOT / "Assets").rglob("*.cs"):
    if "Library" in cs.parts or "Temp" in cs.parts:
        continue
    text = cs.read_text(encoding="utf-8", errors="ignore")
    for m in re.finditer(r"Localization\.(?:Get|Format)\(\s*\"([a-zA-Z0-9_.]+)\"", text):
        cs_keys.add(m.group(1))
problems = [f"code asks for missing key: {k}" for k in sorted(cs_keys - set(en))]
section("code->strings", problems)

# ---------- 4. flag producers vs consumers ----------
producers, consumers = set(), {}
def add_consumer(flag, where):
    consumers.setdefault(flag, []).append(where)

for f in sorted(DOCS.glob("dialogue_*.json")):
    d = load(f)
    for n in d.get("nodes", []):
        where = f"{f.name}:{n.get('id')}"
        for s in n.get("setFlags") or []:
            producers.add(s)
        if n.get("setFlag"):
            producers.add(n["setFlag"])
        if n.get("requiresFlag"):
            add_consumer(n["requiresFlag"], where)
        for c in n.get("choices", []):
            if c.get("requiresFlag"):
                add_consumer(c["requiresFlag"], where)
            if c.get("setFlag"):
                producers.add(c["setFlag"])

for f in sorted(DOCS.glob("*_quests.json")) + [DOCS / "quests.json"] + sorted((RES / "Quests").glob("*.json")):
    if not f.exists():
        continue
    d = load(f)
    quests = d.get("quests", d if isinstance(d, list) else [])
    for q in quests:
        qid = q.get("id", "?")
        producers.update({f"quest.{qid}.done", f"quest.{qid}.ready", f"quest.{qid}.taken"})
        if q.get("unlockFlag"):
            add_consumer(q["unlockFlag"], f"{f.name}:{qid}.unlockFlag")
        for st in q.get("stages", []):
            if st.get("setsFlag"):
                producers.add(st["setsFlag"])

# serialized flag producers in scenes + C# literal/field defaults
FLAG_RE = re.compile(r"[a-zA-Z0-9_]+\.[a-zA-Z0-9_.]+")
for u in SCENES.glob("*.unity"):
    for line in u.read_text(encoding="utf-8", errors="ignore").splitlines():
        m = re.match(r"\s*([a-zA-Z]*[Ff]lag|defeatedFlag|completeFlag|setsFlag|onFlag|doneFlag|lightFlag)\s*:\s*([a-zA-Z0-9_.]+)", line)
        if m and "requires" not in m.group(1).lower():
            producers.add(m.group(2))
        elif m and "requires" in m.group(1).lower():
            add_consumer(m.group(2), f"{u.name}")
for cs in (ROOT / "Assets" / "Oathfire" / "Scripts").rglob("*.cs"):
    text = cs.read_text(encoding="utf-8", errors="ignore")
    for m in re.finditer(r"SetFlag\(\s*\"([a-zA-Z0-9_.]+)\"", text):
        producers.add(m.group(1))
    for m in re.finditer(r"=\s*\"(act[0-9]\.[a-zA-Z0-9_.]+|prologue\.[a-zA-Z0-9_.]+)\"", text):
        producers.add(m.group(1))
    for m in re.finditer(r"requiresFlag[^;]*?\"([a-zA-Z0-9_.]+)\"", text):
        add_consumer(m.group(1), cs.name)

for f in sorted(DOCS.glob("*event*.json")) + [DOCS / "world_events.json"]:
    if not f.exists():
        continue
    d = load(f)
    events = d.get("events", d if isinstance(d, list) else [])
    for e in (events if isinstance(events, list) else []):
        if not isinstance(e, dict):
            continue
        eid = e.get("id", "?")
        producers.add(f"event.{eid}.done")
        if e.get("requiresFlag"):
            add_consumer(e["requiresFlag"], f"{f.name}:{eid}")
        if e.get("setsFlag"):
            producers.add(e["setsFlag"])

problems = []
for flag, wheres in sorted(consumers.items()):
    if flag not in producers:
        problems.append(f"flag never produced: {flag} (needed by {', '.join(wheres[:3])})")
section("flags", problems)

# ---------- 5. asset references ----------
problems = []
voice_dir = RES / "Voice"
icons_dir = ART / "Icons"
portraits_dir = RES / "Portraits"
music_dir = RES / "Music"
amb_dir = RES / "Ambience"

def clip_exists(base, name, exts=(".mp3", ".wav", ".ogg", ".asset")):
    return any((base / f"{name}{x}").exists() for x in exts)

# pack sprite names (LootIcons_101 etc.) live inside the atlas meta
pack_sprites = set()
for meta in PACK.rglob("*.png.meta"):
    for m in re.finditer(r"name:\s*([A-Za-z_]+\d+)", meta.read_text(errors="ignore")):
        pack_sprites.add(m.group(1))

def icon_ok(icon):
    if (icons_dir / f"{icon}.png").exists() or (icons_dir / f"{icon}.asset").exists():
        return True
    return icon in pack_sprites

for f in sorted(DOCS.glob("dialogue_*.json")):
    d = load(f)
    for n in d.get("nodes", []):
        vc = n.get("voiceClip")
        if vc and not clip_exists(voice_dir, vc):
            problems.append(f"{f.name}:{n.get('id')} missing VO {vc}")
        sp = n.get("speaker")
        if sp and sp not in ("", "warden", "narrator") \
                and not (portraits_dir / f"{sp}.png").exists() \
                and not (portraits_dir / f"{sp}.asset").exists():
            problems.append(f"{f.name}:{n.get('id')} speaker {sp} missing portrait")

for f in sorted(DOCS.glob("*item*.json")) + [DOCS / "items.json"]:
    if not f.exists():
        continue
    d = load(f)
    items = d.get("items", d if isinstance(d, list) else [])
    for it in (items if isinstance(items, list) else []):
        if not isinstance(it, dict):
            continue
        icon = it.get("icon")
        if icon and not icon_ok(icon):
            problems.append(f"{f.name}:{it.get('id','?')} missing icon {icon}")

for f in sorted(DOCS.glob("*cinematic*.json")):
    d = load(f)
    art_dir = ART / "Cinematic"
    for i, p in enumerate(d.get("panels", [])):
        img = p.get("image")
        if img and not any((art_dir / sub / img).exists() for sub in art_dir.glob("*") if sub.is_dir()):
            problems.append(f"{f.name} panel {i} missing image {img}")
        vc = p.get("voiceClip")
        if vc and not clip_exists(voice_dir, vc):
            problems.append(f"{f.name} panel {i} missing VO {vc}")
section("asset refs", problems)

# ---------- 6. walkability: every anchor reachable from spawn ----------
problems, infos = [], []
for f in sorted(DOCS.glob("*_map.json")):
    d = load(f)
    cells = {tuple(c[:2]) for c in (d.get("walkable") or [])}
    anchors = d.get("anchors", {})
    start = anchors.get("player") or anchors.get("spawn")
    if not start or not cells:
        problems.append(f"{f.name}: no player anchor or walkable set")
        continue
    start = (start["x"], start["y"])
    if start not in cells:
        problems.append(f"{f.name}: player anchor {start} not walkable")
        continue
    seen = {start}
    dq = deque([start])
    while dq:
        x, y = dq.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            c = (x + dx, y + dy)
            if c in cells and c not in seen:
                seen.add(c)
                dq.append(c)

    def reach(a):
        return any((a["x"] + dx, a["y"] + dy) in seen for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)))

    for name, a in anchors.items():
        vals = a if isinstance(a, list) else [a]
        for i, sub in enumerate(vals):
            if isinstance(sub, dict) and "x" in sub and not reach(sub):
                label = f"{name}[{i}]" if isinstance(a, list) else name
                problems.append(f"{f.name}: anchor {label} ({sub['x']},{sub['y']}) unreachable")
    un = len(cells) - len(seen)
    if un:
        infos.append(f"{f.name}: {un} flagged-walkable cells lie off the walk (usually decorative/interior)")
section("walkability", problems, infos)

# ---------- 7. quest guides sanity ----------
# Guide targets are scene objects (QuestSpot/SceneExit names live in the .unity files),
# not map-json anchors; validate against the built scene's text.
problems = []
g = load(DOCS / "quest_guides.json")
if "__error__" not in g:
    scene_texts = {u.stem: u.read_text(encoding="utf-8", errors="ignore") for u in SCENES.glob("*.unity")}
    maps = {f.stem.replace("_map", "").lower(): load(f) for f in DOCS.glob("*_map.json")}
    scene_aliases = {s.lower(): s for s in scene_texts}
    scene_aliases.update({s.lower().replace("_", ""): s for s in scene_texts})
    for f in DOCS.glob("*_map.json"):
        base = f.stem.replace("_map", "")
        if base.lower().replace("_", "") in scene_aliases and base not in scene_aliases.values():
            pass
    speakers = set()
    for f in DOCS.glob("dialogue_*.json"):
        dd = load(f)
        speakers.add(dd.get("id"))
        for n in dd.get("nodes", []):
            if n.get("speaker"):
                speakers.add(n["speaker"])
    guides = g.get("guides", [])
    for gd in guides:
        if not isinstance(gd, dict):
            continue
        key, scene, target, tid = gd.get("key"), gd.get("scene") or "", gd.get("target"), gd.get("id")
        scene_name = scene_aliases.get(scene.lower()) or scene_aliases.get(scene.lower().replace("_", ""))
        if scene and not scene_name:
            problems.append(f"guide {key}: unknown scene {scene}")
            continue
        if target == "npc" and tid:
            sp = tid.replace("speaker.", "")
            if sp not in speakers:
                problems.append(f"guide {key}: npc {tid} has no dialogue")
            continue
        if target in ("spot", "gather", "hearth", "board", "exit") and tid and scene_name:
            text = scene_texts.get(scene_name, "")
            # spot ids and exit/spot object names appear verbatim in the scene yaml
            if tid not in text:
                problems.append(f"guide {key}: target {tid} not found in {scene_name} scene")
section("quest guides", problems)

print("\n" + ("AUDIT CLEAN" if not fail_sections else f"AUDIT FAILED sections: {fail_sections}"))
sys.exit(len(fail_sections))
