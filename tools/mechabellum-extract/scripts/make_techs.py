"""Join every unit's technology list with TechnologyGroupData and emit
technologies.csv / technologies.json — one row per (unit, technology)."""
import json, csv, os, io, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
ROSTER = os.path.join(HERE, "..", "..", "..", "Melper.Core", "Data", "units.json")
data = json.load(open(os.path.join(DATA, "config_raw.json"), encoding="utf-8"))

cfg = data["ConfigDataContainer"]
SPECIAL = {0: "main", 1: "special", 2: "survive", 3: "summon"}  # see make_units.py

# Every list in TechnologyGroupData is a TechnologyData subclass, so — exactly like
# skills in make_units.py — they all index into one id space. The list a tech came
# from IS its effect kind (airAttackTechnologyDatas, buffTechnologies, ...), which is
# the only machine-readable classification the data offers; keep it as `category`.
techs, category = {}, {}
for key, lst in data["TechnologyGroupData"].items():
    if not isinstance(lst, list):
        continue
    for t in lst:
        if isinstance(t, dict) and "id" in t:
            assert t["id"] not in techs, f"tech id {t['id']} in both {category[t['id']]} and {key}"
            techs[t["id"]], category[t["id"]] = t, key
print(f"indexed {len(techs)} technologies from "
      f"{sum(1 for v in data['TechnologyGroupData'].values() if isinstance(v, list))} lists")

# English names live only in the C# roster, keyed by Id (the game data is Chinese).
try:
    names_en = {u["Id"]: u["Name"] for u in json.load(open(ROSTER, encoding="utf-8"))["Units"]}
except OSError:
    names_en = {}


def render(desc, params):
    """Fill {0}-style placeholders from the ';'-separated descParams.

    Indexed, not sequential: descriptions use their params out of order
    ("...{0}米...{1}%...{3}秒...{2}米..."), and a trailing ';' yields an empty one.
    A placeholder with no param is left alone rather than shifting the rest.
    """
    parts = str(params).split(";")
    out = str(desc)
    for i, p in enumerate(parts):
        if p != "":
            out = out.replace("{" + str(i) + "}", p)
    return out


rows = []
for card in sorted(cfg["cardDatas"], key=lambda c: c.get("mechID", 0)):
    ids = card.get("technologies") or []
    default = card.get("defaultTechnologies") or []
    # defaultTechnologies is always a subset of technologies (4 or 6 of them), but
    # NOT the first N — Sandworm, Phantom Ray, War Factory, Abyss and 试验级霸主 skip
    # around the list. What the game does with the distinction is undecoded, so the
    # column is passed through as "is it in that list" and nothing is derived from it.
    assert set(default) <= set(ids), f"card {card['id']}: defaults not a subset"
    for order, tid in enumerate(ids):
        t = techs.get(tid)
        assert t is not None, f"card {card['id']} references unknown tech {tid}"
        # Three 超级* techs are flagged as test data, all on 试验级* survive-mode units.
        # Nothing on the normal roster is, and that is worth failing on.
        assert not (t.get("isTestData") and card.get("specialUnit") == 0), \
            f"tech {tid} on main-roster unit {card['id']} is test data"
        rows.append({
            "unitId": card.get("mechID"),
            "unitNameCn": card.get("name"),
            "unitName": names_en.get(card.get("mechID"), ""),
            "roster": SPECIAL.get(card.get("specialUnit"), card.get("specialUnit")),
            "order": order,
            "inDefaultList": tid in default,
            "techId": tid,
            "name": t.get("name"),
            "category": category[tid],
            "supply": t.get("supply"),
            "unlockCost": t.get("unlockCost"),
            "previousTechID": t.get("previousTechID"),
            "activeLevel": t.get("activeLevel"),
            "isTestData": bool(t.get("isTestData")),
            "iconName": t.get("iconName"),
            "text": render(t.get("description"), t.get("descParams")),
            "description": t.get("description"),
            "descParams": t.get("descParams"),
        })

# Within the normal roster a technology belongs to exactly one unit — its numbers
# (supply above all) are per-unit, so one effect exists as several ids (1802 and 1806
# are both 电磁弹, at 250 and 100 supply). If a patch ever shares an id between two
# roster units, these flat rows would silently double-count it. Survive-mode 试验级*
# copies do reuse their originals' ids, so they are excluded from the check.
owner = {}
for r in rows:
    if r["roster"] != "main":
        continue
    assert r["techId"] not in owner, f"tech {r['techId']} on units {owner[r['techId']]} and {r['unitId']}"
    owner[r["techId"]] = r["unitId"]

cols = list(rows[0].keys())
with open(os.path.join(DATA, "technologies.csv"), "w", newline="", encoding="utf-8-sig") as f:
    w = csv.DictWriter(f, fieldnames=cols)
    w.writeheader()
    w.writerows(rows)
with open(os.path.join(DATA, "technologies.json"), "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)

main = [r for r in rows if r["roster"] == "main"]
units_main = len({r["unitId"] for r in main})
print(f"technologies: {len(rows)} on {len({r['unitId'] for r in rows})} units | "
      f"main roster: {len(main)} on {units_main} units | "
      f"unused by any card: {len(techs) - len({r['techId'] for r in rows})}\n")
hdr = f"{'unit':<12} {'id':>7} {'name':<10} {'supply':>6}  {'category'}"
print(hdr); print("-" * len(hdr))
for r in main[:20]:
    print(f"{(r['unitName'] or r['unitNameCn']):<12} {r['techId']:>7} {r['name']:<10} "
          f"{str(r['supply']):>6}  {r['category']}")
