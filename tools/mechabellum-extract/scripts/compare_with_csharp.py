"""Diff Melper.Core/Data/units.json against the extracted game data.

Joined on Unit.Id, which is the game's own MechData.id -- names are display text
and change with localisation, ids do not.

Reports three things:
  * fields that drifted (the game was patched, the roster file was not);
  * units in the game roster that the roster file is missing, as a ready-to-paste
    JSON block using the Chinese name as a placeholder;
  * roster entries whose id no longer exists in the game.
"""
import datetime, json, os, io, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
MELPER = os.path.join(ROOT, "Melper.Core", "Data", "units.json")

# roster field  ->  column in the extracted units.json
FIELDS = [("Cost", "cost"), ("UnlockCost", "unlockCost"),
          ("Health", "life"), ("Damage", "damage"),
          ("ReloadTime", "attackInterval"), ("Range", "attackRange"),
          ("Speed", "moveSpeed"), ("Splash", "splashRange"),
          ("CountInPack", "countInPack"), ("IsAir", "isFly"),
          # CanAttackAir means innate anti-air only; six units get it from a
          # purchasable technology instead, which is CanAttackAirWithTech.
          ("CanAttackAir", "vsAir"), ("CanAttackAirWithTech", "vsAirTech")]
BOOL_PROPS = {"CanAttackAir", "CanAttackAirWithTech", "IsAir"}

melper = json.load(open(MELPER, encoding="utf-8-sig"))
roster = melper["Units"]
game = {g["id"]: g for g in json.load(open(os.path.join(DATA, "units.json"), encoding="utf-8"))}
by_id = {c["Id"]: c for c in roster}
print(f"roster units: {len(roster)} (checked against the game on {melper['AsOf']}) | "
      f"game units: {len(game)} "
      f"(main roster: {sum(1 for g in game.values() if g['roster'] == 'main')})\n")

dupes = [i for i in by_id if [c["Id"] for c in roster].count(i) > 1]
if dupes:
    print(f"WARNING duplicate ids in the roster: {dupes}\n")

drift = 0
for uid, c in sorted(by_id.items()):
    g = game.get(uid)
    if g is None:
        print(f"  id={uid} {c.get('Name')}: NOT IN GAME DATA ANY MORE")
        continue
    diffs = []
    for prop, col in FIELDS:
        cv, gv = c.get(prop), g.get(col)
        if prop in BOOL_PROPS:
            # An omitted bool in the roster file is false, so treat missing as false
            # on both sides -- otherwise a flag the game sets but the roster never
            # mentions would silently pass.
            cv, gv = bool(cv), bool(gv)
            if cv != gv:
                diffs.append(f"{prop}: {cv} -> {gv}")
            continue
        # Same rule for the numbers: an omitted one is the type's default, zero.
        cv = 0 if cv is None else cv
        if gv is None:
            continue
        if abs(float(cv) - float(gv)) > 0.011:
            # melee is Range 0 in the roster but a small real radius in the game
            if prop == "Range" and float(cv) == 0 and float(gv) <= 6:
                continue
            diffs.append(f"{prop}: {cv:g} -> {gv:g}")
    if diffs:
        drift += 1
        print(f"  id={uid:<5}{c.get('Name'):<15} " + "; ".join(diffs))

print(f"\n{drift} of {len(by_id)} units differ from the game (roster value -> game value)")

missing = [g for uid, g in sorted(game.items())
           if g["roster"] == "main" and uid not in by_id]
if missing:
    print(f"\n=== {len(missing)} roster unit(s) missing from units.json ===")
    print("paste into the list, then rename from the Chinese placeholder:\n")
    for g in missing:
        entry = {
            "Id": g["id"],
            "Name": g["name_cn"],
            "Cost": g["cost"],
            "UnlockCost": g["unlockCost"],
            "CountInPack": g["countInPack"],
            "Damage": g["damage"],
            "ReloadTime": g["attackInterval"],
            "Health": g["life"],
            "Range": g["attackRange"],
            "Speed": g["moveSpeed"],
            "Splash": g["splashRange"],
        }
        if g["vsAir"]:
            entry["CanAttackAir"] = True
        if g.get("vsAirTech"):
            entry["CanAttackAirWithTech"] = True
        if g["isFly"]:
            entry["IsAir"] = True
        # Same shape the C# writer produces: two-space indent, no zero-valued fields.
        entry = {k: v for k, v in entry.items() if v or k in ("Name", "CountInPack", "ReloadTime", "Speed")}
        body = json.dumps(entry, ensure_ascii=False, indent=2)
        print("  " + body.replace("\n", "\n  ") + ",")
else:
    print("\nno roster units missing from units.json")

# The Data page in the web app shows this date and starts warning once it goes stale,
# so it has to move with the numbers.
print(f'\nafter transferring anything above, set "AsOf": "{datetime.date.today()}" in units.json')
