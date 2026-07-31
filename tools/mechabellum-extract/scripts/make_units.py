"""Join MechData with its SkillData and emit units.csv / units.json."""
import json, csv, os, io, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
data = json.load(open(os.path.join(DATA, "config_raw.json"), encoding="utf-8"))

cfg = data["ConfigDataContainer"]
skills_src = data["MechSkillGroupData"]

UNIT_TYPE = {0: "Small", 1: "Medium", 2: "Huge"}  # GameRiver.UnitType

# cardDatas holds the shop side of a unit: price, pack size, and the flags that
# say whether it belongs to the normal game at all.
cards = {c["mechID"]: c for c in cfg["cardDatas"] if "mechID" in c}

# specialUnit == 0 selects exactly the 32 units of the normal roster. Non-zero
# values are: 1 = titan-class specials, 2 = survive-mode / 试验级 variants,
# 3 = summoned props (larva, spider mine).
SPECIAL = {0: "main", 1: "special", 2: "survive", 3: "summon"}

# Anti-air is often not innate but bought as a technology (防空弹药 etc). Detect the
# techs that actually GRANT it, so base capability and upgraded capability stay
# distinguishable. Markers chosen to skip techs that merely buff existing AA
# (防空专精 = "AA specialisation" reads "more damage vs air", not "can hit air").
AA_GRANT = ("可以攻击空中", "可对空攻击", "只对空中单位造成伤害")
techs = {}
for _lst in data["TechnologyGroupData"].values():
    if isinstance(_lst, list):
        for _t in _lst:
            if isinstance(_t, dict) and "id" in _t:
                techs[_t["id"]] = _t


def grants_air(card):
    ids = (card.get("technologies") or []) + (card.get("defaultTechnologies") or [])
    for tid in ids:
        desc = str(techs.get(tid, {}).get("description", ""))
        if any(mark in desc for mark in AA_GRANT):
            return True
    return False

# every list in MechSkillGroupData is a SkillData subclass -> one id index
skills = {}
for key, lst in skills_src.items():
    if not isinstance(lst, list):
        continue
    for s in lst:
        if isinstance(s, dict) and "id" in s:
            skills[s["id"]] = (key, s)
print(f"indexed {len(skills)} skills from {sum(1 for v in skills_src.values() if isinstance(v, list))} lists")

rows = []
for m in sorted(cfg["mechDatas"], key=lambda x: x["id"]):
    kind, sk = skills.get(m.get("mainSkillID"), (None, {}))
    dmg_list = sk.get("damage") or []
    card = cards.get(m["id"], {})
    rows.append({
        "id": m["id"],
        "name_cn": m["name"],
        "roster": SPECIAL.get(card.get("specialUnit"), card.get("specialUnit")),
        "tier": card.get("group"),
        "cost": card.get("baseMoney"),
        "unlockCost": card.get("unlockPrice"),
        "countInPack": card.get("mechCount"),
        "prefab": m.get("prefabName"),
        "size": UNIT_TYPE.get(m.get("mechType"), m.get("mechType")),
        "life": m.get("life"),
        "damage": m.get("damage"),
        "attackStrength": m.get("attackStrength"),
        "moveSpeed": m.get("moveSpeed"),
        "isFly": bool(m.get("isFly")),
        "radius": m.get("radius"),
        "skillId": m.get("mainSkillID"),
        "skillKind": kind,
        "skillDamage": dmg_list[0] if dmg_list else None,
        "attackRange": sk.get("attackRange"),
        "minAttackRange": sk.get("minAttackRange"),
        "attackInterval": sk.get("attackDuration"),
        "splashRange": sk.get("splashRange"),
        "vsGround": bool(sk.get("canAttackGround")) if sk else None,
        "vsAir": bool(sk.get("canAttackAir")) if sk else None,
        "vsAirTech": grants_air(card),
    })

cols = list(rows[0].keys())
with open(os.path.join(DATA, "units.csv"), "w", newline="", encoding="utf-8-sig") as f:
    w = csv.DictWriter(f, fieldnames=cols)
    w.writeheader()
    w.writerows(rows)
with open(os.path.join(DATA, "units.json"), "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)

matched = sum(1 for r in rows if r["attackRange"] is not None)
main = sum(1 for r in rows if r["roster"] == "main")
print(f"units: {len(rows)} | main roster: {main} | joined to a skill: {matched}\n")
hdr = f"{'id':>3} {'name':<10} {'size':<7} {'life':>7} {'dmg':>6} {'rng':>7} {'intvl':>6} {'splash':>7} {'air':>5}"
print(hdr); print("-" * len(hdr))
for r in rows[:20]:
    rng = f"{r['attackRange']:.1f}" if isinstance(r["attackRange"], (int, float)) else "-"
    itv = f"{r['attackInterval']:.2f}" if isinstance(r["attackInterval"], (int, float)) else "-"
    spl = f"{r['splashRange']:.2f}" if isinstance(r["splashRange"], (int, float)) else "-"
    print(f"{r['id']:>3} {r['name_cn']:<10} {str(r['size']):<7} {r['life']:>7} {r['damage']:>6} "
          f"{rng:>7} {itv:>6} {spl:>7} {str(r['vsAir']):>5}")
