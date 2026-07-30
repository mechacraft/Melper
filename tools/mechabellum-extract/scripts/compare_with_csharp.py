"""Diff Melper.Core/Data/UnitsCollection.cs against the extracted game data.

Joined on Unit.Id, which is the game's own MechData.id -- names are display text
and change with localisation, ids do not.

Reports three things:
  * fields that drifted (the game was patched, the C# file was not);
  * units in the game roster that the C# file is missing, as a ready-to-paste
    block using the Chinese name as a placeholder;
  * C# entries whose id no longer exists in the game.
"""
import json, os, re, io, sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CS = os.path.join(ROOT, "Melper.Core", "Data", "UnitsCollection.cs")

# C# property  ->  column in units.json
FIELDS = [("Cost", "cost"), ("Health", "life"), ("Damage", "damage"),
          ("ReloadTime", "attackInterval"), ("Range", "attackRange"),
          ("Speed", "moveSpeed"), ("Splash", "splashRange"),
          ("CountInPack", "countInPack"), ("IsAir", "isFly"),
          # CanAttackAir means innate anti-air only; six units get it from a
          # purchasable technology instead, which is CanAttackAirWithTech.
          ("CanAttackAir", "vsAir"), ("CanAttackAirWithTech", "vsAirTech")]
BOOL_PROPS = {"CanAttackAir", "CanAttackAirWithTech", "IsAir"}


def parse_cs(text):
    out = []
    for block in re.findall(r"new\(\)\s*\{(.*?)\n\s*\}", text, re.S):
        rec = {}
        for prop, _ in FIELDS + [("Id", None), ("Name", None)]:
            m = re.search(rf"\b{prop}\s*=\s*([^,\n]+)", block)
            if not m:
                continue
            v = m.group(1).strip().rstrip(",").split("//")[0].strip()
            if prop == "ReloadTime":
                m2 = re.search(r"FromSeconds\(([\d.]+)\)", v)
                rec[prop] = float(m2.group(1)) if m2 else None
            elif prop == "Id":
                rec[prop] = int(v)
            elif prop == "Name":
                rec[prop] = v.strip('"')
            elif v in ("true", "false"):
                rec[prop] = v == "true"
            else:
                try:
                    rec[prop] = float(v.rstrip("m"))
                except ValueError:
                    rec[prop] = None
        if "Id" in rec:
            out.append(rec)
    return out


cs = parse_cs(open(CS, encoding="utf-8-sig").read())
game = {g["id"]: g for g in json.load(open(os.path.join(DATA, "units.json"), encoding="utf-8"))}
by_id = {c["Id"]: c for c in cs}
print(f"C# units: {len(cs)} | game units: {len(game)} "
      f"(main roster: {sum(1 for g in game.values() if g['roster'] == 'main')})\n")

dupes = [i for i in by_id if [c["Id"] for c in cs].count(i) > 1]
if dupes:
    print(f"WARNING duplicate ids in C#: {dupes}\n")

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
            # An omitted bool in C# is false, so treat missing as false on both
            # sides -- otherwise a flag the game sets but C# never mentions
            # would silently pass.
            cv, gv = bool(cv), bool(gv)
            if cv != gv:
                diffs.append(f"{prop}: {cv} -> {gv}")
            continue
        if cv is None or gv is None:
            continue
        if abs(float(cv) - float(gv)) > 0.011:
            # melee is Range 0 in C# but a small real radius in the game
            if prop == "Range" and float(cv) == 0 and float(gv) <= 6:
                continue
            diffs.append(f"{prop}: {cv:g} -> {gv:g}")
    if diffs:
        drift += 1
        print(f"  id={uid:<5}{c.get('Name'):<15} " + "; ".join(diffs))

print(f"\n{drift} of {len(by_id)} units differ from the game (C# value -> game value)")

missing = [g for uid, g in sorted(game.items())
           if g["roster"] == "main" and uid not in by_id]
if missing:
    print(f"\n=== {len(missing)} roster unit(s) missing from UnitsCollection.cs ===")
    print("paste into the list, then rename from the Chinese placeholder:\n")
    for g in missing:
        lines = [f'            Id = {g["id"]},',
                 f'            Name = "{g["name_cn"]}",',
                 f'            Cost = {g["cost"]},',
                 f'            Damage = {g["damage"]},',
                 f'            Health = {g["life"]},',
                 f'            ReloadTime = TimeSpan.FromSeconds({g["attackInterval"]:g}),',
                 f'            CountInPack = {g["countInPack"]},',
                 f'            Splash = {g["splashRange"]:g},',
                 f'            Range = {g["attackRange"]:g},',
                 f'            Speed = {g["moveSpeed"]},']
        if g["vsAir"]:
            lines.append("            CanAttackAir = true,")
        if g.get("vsAirTech"):
            lines.append("            CanAttackAirWithTech = true,")
        if g["isFly"]:
            lines.append("            IsAir = true,")
        lines[-1] = lines[-1].rstrip(",")
        print("        new()\n        {\n" + "\n".join(lines) + "\n        },")
else:
    print("\nno roster units missing from UnitsCollection.cs")
