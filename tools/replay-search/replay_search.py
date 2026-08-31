"""Search Mechabellum replays (*.grbr) for army compositions.

A .grbr file is a .NET BinaryFormatter stream that carries the whole match as a
single XML <BattleRecord> string, plus a DateTime right after it. Both are read
here without any dependencies -- stdlib only, no venv.

  python replay_search.py --player MakTpaxep --unit Fortress --min 4 --last 15
  python replay_search.py --player MakTpaxep --tech "Heavy Missile"
  python replay_search.py --player MakTpaxep --side opponent --spell Storm --spell Ion
"""

import argparse
import collections
import datetime
import glob
import json
import os
import struct
import sys
import xml.etree.ElementTree as ET

GAME = r"D:\SteamLibrary\steamapps\common\Mechabellum"
REPLAY_DIR = os.path.join(GAME, "ProjectDatas", "Replay")
HERE = os.path.dirname(os.path.abspath(__file__))
UNITS_JSON = os.path.join(HERE, "..", "..", "Melper.Core", "Data", "units.json")
TECHS_JSON = os.path.join(HERE, "..", "mechabellum-extract", "data", "technologies.json")
CONFIG_RAW = os.path.join(HERE, "..", "mechabellum-extract", "data", "config_raw.json")

XSI_TYPE = "{http://www.w3.org/2001/XMLSchema-instance}type"
END_TAG = b"</BattleRecord>"


# --- replay file ------------------------------------------------------------

class Replay:
    def __init__(self, path):
        self.path = path
        self.name = os.path.basename(path)
        raw = open(path, "rb").read()

        start = raw.find(b"<?xml")
        end = raw.rfind(END_TAG)
        if start < 0 or end < 0:
            raise ValueError("no <BattleRecord> in file")
        end += len(END_TAG)
        self.root = ET.fromstring(raw[start:end].decode("utf-8", "replace"))
        self.date = _create_time(raw, end) or datetime.datetime.fromtimestamp(
            os.path.getmtime(path)
        )

    def players(self):
        """{player name: round records, in play order}."""
        out = {}
        for player in self.root.find("playerRecords"):
            rounds = sorted(player.find("playerRoundRecords") or [],
                            key=lambda r: int(r.findtext("round")))
            if rounds:
                out[player.findtext("name")] = rounds
        return out


def _create_time(raw, offset):
    """CreateTime is a .NET DateTime serialized straight after the XML string.

    Ticks since 0001-01-01 in the low 62 bits, DateTimeKind in the top two.
    Survives file copies, unlike mtime -- but fall back to mtime if it is junk.
    """
    if offset + 8 > len(raw):
        return None
    ticks = struct.unpack("<q", raw[offset:offset + 8])[0] & 0x3FFFFFFFFFFFFFFF
    try:
        when = datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=ticks // 10)
    except OverflowError:
        return None
    return when if 2015 <= when.year <= 2100 else None


# --- army composition -------------------------------------------------------

def army(round_record):
    """Units the player brought into this round, as {unit id: count}.

    This is the snapshot the game itself stored, so it is exact.
    """
    units = round_record.find("playerData/units")
    return collections.Counter(u.findtext("id") for u in (units if units is not None else []))


def bought(round_record):
    """Units purchased during this round, as {unit id: count}.

    A LIFO undo stack is applied: PAD_Undo carries no target, it just reverts
    the previous action. Measured against the next round's snapshot over every
    replay in the directory, this over-counts in ~1% of rounds (vs ~12% when
    undos are ignored). It still *under*-counts, because reinforcement cards
    hand out free units that no action records -- see README.
    """
    actions = list(round_record.find("actionRecords") or [])
    stack = []
    for action in actions:
        if action.get(XSI_TYPE) == "PAD_Undo":
            if stack:
                stack.pop()
        else:
            stack.append(action)
    return collections.Counter(
        a.findtext("UID") for a in stack if a.get(XSI_TYPE) == "PAD_BuyUnit"
    )


def fought_with(rounds, i):
    """What the player had on the field when round i was fought.

    (counter, exact). For any round but the last the next round's snapshot
    answers this outright, free reinforcement units included. Only the final
    round has to be estimated as snapshot + purchases, which is a lower bound.
    """
    if i + 1 < len(rounds):
        return army(rounds[i + 1]), True
    return army(rounds[i]) + bought(rounds[i]), False


# --- unit names -------------------------------------------------------------

def unit_names():
    """{id: English name} from units.json -- the project's own roster."""
    with open(UNITS_JSON, encoding="utf-8") as fh:
        return {u["Id"]: u["Name"] for u in json.load(fh)["Units"] if u.get("Id")}


def resolve_unit(query, names):
    if query.isdigit():
        return int(query)
    matches = [uid for uid, name in names.items() if name.lower() == query.lower()]
    if not matches:
        matches = [uid for uid, name in names.items() if query.lower() in name.lower()]
    if len(matches) == 1:
        return matches[0]
    if not matches:
        sys.exit(f"unknown unit {query!r}. Known: {', '.join(sorted(names.values()))}")
    sys.exit(f"ambiguous unit {query!r}: {', '.join(names[m] for m in matches)}")


# --- technologies -----------------------------------------------------------

def technologies(required=True):
    """Rows from mechabellum-extract's dump: the only place tech names live.

    A numeric TechID needs no names, so there the dump is a nicety (it labels
    the hit) and its absence is not fatal.
    """
    try:
        with open(TECHS_JSON, encoding="utf-8") as fh:
            return json.load(fh)
    except FileNotFoundError:
        if not required:
            return []
        sys.exit(f"no {TECHS_JSON}. Run tools/mechabellum-extract/run.ps1 to make "
                 f"it, or pass --tech as a numeric TechID.")


def tech_label(entry):
    """'Heavy Missile' out of iconName 'UT_Heavy_Missile' -- the only Latin name."""
    return entry["iconName"].removeprefix("UT_").replace("_", " ")


def tech_ids(entry):
    """Every TechID this tech is written as in a replay.

    Old replays store the plain id from the game data. Since client 2276 unit
    ids carry a +5000 offset, and a TechID is <prefix><unit id>, so does the
    tail of the TechID: Vortex' 180931 is written 18095031. For the 81 techs
    whose id does not end in their unit id there is nothing to shift, so only
    the plain form is matched.
    """
    tech, unit = str(entry["techId"]), str(entry["unitId"])
    ids = {tech}
    if tech.endswith(unit):
        ids.add(tech[:-len(unit)] + str(5000 + entry["unitId"]))
    return ids


def resolve_tech(query, techs):
    """(label, {TechID: unit name}).

    One name usually covers several units -- Attack Range Increase is 48 rows,
    one per unit -- so all of their ids count, and the unit is reported per hit
    rather than crammed into the label.
    """
    if query.isdigit():
        hits = [t for t in techs if str(t["techId"]) == query]
        if not hits:
            return query, {query: "?"}
        return tech_label(hits[0]), _owners(hits)

    def pick(test):
        return [t for t in techs if test(t)]

    wanted = query.lower()
    hits = pick(lambda t: tech_label(t).lower() == wanted or t["name"] == query)
    if not hits:
        hits = pick(lambda t: wanted in tech_label(t).lower() or query in t["name"])
    if not hits:
        known = sorted({tech_label(t) for t in techs})
        sys.exit(f"unknown technology {query!r}. Known: {', '.join(known)}")

    labels = sorted({tech_label(t) for t in hits})
    if len(labels) > 1:
        sys.exit(f"ambiguous technology {query!r}: {', '.join(labels)}")
    return labels[0], _owners(hits)


def _owners(hits):
    return {tech: hit["unitName"] for hit in hits for tech in tech_ids(hit)}


def bought_tech(rounds, ids):
    """[(round number, TechID)] over the whole match, in play order.

    Techs are permanent, so unlike a unit count this is a property of the match
    and not of one round -- --round and --min do not apply to it.
    """
    out = []
    for record in rounds:
        for action in (record.find("actionRecords") or []):
            if action.get(XSI_TYPE) != "PAD_UpgradeTechnology":
                continue
            tech = action.findtext("TechID")
            if tech in ids:
                out.append((record.findtext("round"), tech))
    return out


# --- commander skills (spells) ----------------------------------------------

def commander_skills(required=True):
    """{skill id: {"label", "name"}} straight out of the client dump.

    There is no derived spells.json to read the way --tech reads
    technologies.json: config_raw.json is committed, so --spell works with no
    extractor run, while a new make_spells.py would gate it behind uv and an
    installed game. Every list in CommanderSkillGroupData is a
    CommanderSkillData subclass over one shared id space -- the same shape
    make_techs.py leans on for technologies.
    """
    try:
        with open(CONFIG_RAW, encoding="utf-8") as fh:
            groups = json.load(fh)["CommanderSkillGroupData"]
    except FileNotFoundError:
        if not required:
            return {}
        sys.exit(f"no {CONFIG_RAW}. Run tools/mechabellum-extract/run.ps1 to "
                 f"make it, or pass --spell as a numeric skill id.")

    out = {}
    for group in groups.values():
        if not isinstance(group, list):
            continue
        for skill in group:
            if isinstance(skill, dict) and "id" in skill:
                out[str(skill["id"])] = {"label": spell_label(skill),
                                         "name": skill.get("name")}
    return out


def spell_label(skill):
    """'Thunder Storm' out of iconName 'SW_Thunder_Storm' -- the only Latin name.

    It is the internal name and not always the in-game one (SW_Ion is Ion
    Bombardment), which is why the match below is a substring one.
    """
    return skill["iconName"].removeprefix("SW_").replace("_", " ")


def resolve_spell(query, skills):
    """(label, {skill id}).

    One spell is several ids -- a tier or a rework gets its own entry, so
    Lightning Storm is both 300005 and 300009 -- and all of them count, exactly
    as one technology name covers one id per unit.
    """
    if query.isdigit():
        known = skills.get(query)
        return (known["label"] if known else query), {query}

    wanted = query.lower()
    ids = {i for i, s in skills.items()
           if s["label"].lower() == wanted or s["name"] == query}
    if not ids:
        ids = {i for i, s in skills.items()
               if wanted in s["label"].lower() or query in (s["name"] or "")}
    if not ids:
        known = sorted({s["label"] for s in skills.values()})
        sys.exit(f"unknown spell {query!r}. Known: {', '.join(known)}")

    labels = sorted({skills[i]["label"] for i in ids})
    if len(labels) > 1:
        sys.exit(f"ambiguous spell {query!r}: {', '.join(labels)}")
    return labels[0], ids


def released(rounds):
    """[(round number, skill id or None)] -- every spell this side cast.

    Two quirks of the format, and ignoring either one turns an opponent's whole
    spell log into silence rather than into an error:

    * <ID> is filled in only for the side that recorded the replay. Everyone
      else's actions carry <ID>0</ID>, and only <SkillIndex> means anything --
      an opponent's spell has to be looked up in their commanderSkills snapshot.
    * That snapshot lags a round: a skill bought in round N first appears in the
      list in round N+1. So a cast in round N is read against the slots known by
      round N+1 and no later, which is all the game itself had recorded by then.

    Looking further ahead would resolve a few more casts and get some of them
    wrong: in replays whose snapshot only starts appearing mid-match, a late
    slot list does not describe the early rounds. Measured against the 889 casts
    that do carry an <ID> of their own, the round N+1 rule agrees 842 times,
    disagrees 0 and leaves 47 unnamed; borrowing later snapshots turns 4 of
    those into wrong answers. Unnamed casts are reported, never dropped.

    Skill ids are not touched by the +5000 unit id offset of client 2276+:
    over every replay in the directory, none of them is 5-prefixed.
    """
    known, by_round = {}, []
    for record in rounds:
        for skill in (record.find("playerData/commanderSkills") or []):
            known[skill.findtext("index")] = skill.findtext("id")
        by_round.append(dict(known))

    out = []
    for i, record in enumerate(rounds):
        slots = by_round[min(i + 1, len(by_round) - 1)]
        for action in (record.find("actionRecords") or []):
            if action.get(XSI_TYPE) != "PAD_ReleaseCommanderSkill":
                continue
            skill = action.findtext("ID")
            if skill == "0":
                skill = slots.get(action.findtext("SkillIndex"))
            out.append((record.findtext("round"), skill))
    return out


def sides(players, player, side):
    """[(name, rounds)] the search applies to: one player's, or their enemies'.

    --player always says which matches to look at; --side says whose army,
    technologies and spells to look at inside them.
    """
    if player not in players:
        return []
    if side == "player":
        return [(player, players[player])]
    return [(name, rounds) for name, rounds in players.items() if name != player]


# --- main -------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default=REPLAY_DIR, help="replay folder (default: %(default)s)")
    ap.add_argument("--player", required=True, help="whose side to look at")
    ap.add_argument("--unit", help="unit name or id, e.g. Fortress or 1")
    ap.add_argument("--tech", help="technology name or TechID, e.g. \"Heavy Missile\" or 10912")
    ap.add_argument("--spell", action="append", metavar="NAME", default=[],
                    help="commander skill name or id, e.g. Ion or 300006; repeat to "
                         "require every one of them in the same match")
    ap.add_argument("--side", choices=("player", "opponent"), default="player",
                    help="whose army/techs/spells to match -- --player's own, or "
                         "everyone else's in the same match (default: %(default)s)")
    ap.add_argument("--min", type=int, default=1, help="report armies with at least this many (default: %(default)s)")
    ap.add_argument("--last", type=int, metavar="N", help="only the N most recent matches")
    ap.add_argument("--round", choices=("last", "any"), default="last",
                    help="which round to count in (default: %(default)s)")
    ap.add_argument("-v", "--verbose", action="store_true", help="print the full army of every hit")
    args = ap.parse_args()
    if not args.unit and not args.tech and not args.spell:
        ap.error("nothing to look for: pass --unit, --tech or --spell")

    names = unit_names()
    unit_id = unit_label = None
    if args.unit:
        unit_id = str(resolve_unit(args.unit, names))
        unit_label = names.get(int(unit_id), unit_id)
    tech_label_, tech_owners = (None, None)
    if args.tech:
        tech_label_, tech_owners = resolve_tech(
            args.tech, technologies(required=not args.tech.isdigit()))
    skills = commander_skills(
        required=not all(s.isdigit() for s in args.spell)) if args.spell else {}
    wanted_spells = [resolve_spell(s, skills) for s in args.spell]

    paths = glob.glob(os.path.join(args.dir, "*.grbr"))
    if not paths:
        sys.exit(f"no .grbr files in {args.dir}")

    replays, skipped = [], []
    for path in paths:
        try:
            replays.append(Replay(path))
        except Exception as exc:
            skipped.append((os.path.basename(path), exc))

    replays.sort(key=lambda r: r.date, reverse=True)
    scanned = replays[:args.last] if args.last else replays

    seen_players = set()
    hits = 0
    for replay in scanned:
        players = replay.players()
        seen_players.update(players)

        for side, rounds in sides(players, args.player, args.side):
            whose = f"{side}: " if args.side == "opponent" else ""

            techs = bought_tech(rounds, tech_owners) if tech_owners else []
            if tech_owners and not techs:
                continue
            bought_by = ", ".join(f"{tech_owners[tech]} round {rnd}" for rnd, tech in techs)
            tech_note = f"  [{tech_label_}: {bought_by}]" if techs else ""

            casts = released(rounds) if wanted_spells else []
            cast_note = _cast_note(casts, wanted_spells)
            if wanted_spells and cast_note is None:
                continue

            if unit_id is None:
                hits += 1
                summary = " ".join(part for part in (
                    f"{tech_label_} bought by {bought_by}" if techs else "",
                    (cast_note or "").strip()) if part)
                print(f"{replay.date:%Y-%m-%d %H:%M}  {whose}{summary}  {replay.name}")
                if args.verbose:
                    _print_army(fought_with(rounds, len(rounds) - 1)[0], names)
                continue

            wanted = range(len(rounds)) if args.round == "any" else range(len(rounds) - 1, len(rounds))
            for i in wanted:
                if i < 0:
                    continue
                record = rounds[i]
                field, exact = fought_with(rounds, i)
                if field[unit_id] < args.min:
                    continue
                hits += 1
                note = "" if exact else " (at least; final round)"
                if not list(record.find("actionRecords") or []):
                    note += " (round has no actions -- recorded but never played?)"
                print(f"{replay.date:%Y-%m-%d %H:%M}  round {record.findtext('round'):>2}  "
                      f"{whose}{unit_label} {field[unit_id]}{note}  {replay.name}"
                      f"{tech_note}{cast_note or ''}")
                if args.verbose:
                    _print_army(field, names)

    for name, exc in skipped:
        print(f"SKIP {name}: {exc}", file=sys.stderr)
    if args.player not in seen_players:
        sys.exit(f"player {args.player!r} appears in none of the {len(scanned)} replays scanned")
    print(f"\n{hits} match(es) in {len(scanned)} replay(s)"
          f"{f', {len(skipped)} skipped' if skipped else ''}")


def _cast_note(casts, wanted):
    """'[Storm round 8; Ion round 4,8]' -- or None if a wanted spell is missing.

    Spells are counted over the whole match, like technologies and unlike a
    unit count, so --round and --min do not apply to them. Casts that released
    could not name are reported rather than dropped -- see its docstring for
    when that happens.
    """
    if not wanted:
        return ""
    parts = []
    for label, ids in wanted:
        rnds = [rnd for rnd, skill in casts if skill in ids]
        if not rnds:
            return None
        parts.append(f"{label} round {','.join(rnds)}")
    unnamed = sum(1 for _, skill in casts if skill is None)
    if unnamed:
        parts.append(f"+{unnamed} cast(s) no snapshot names yet")
    return f"  [{'; '.join(parts)}]"


def _print_army(field, names):
    for uid, count in sorted(field.items(), key=lambda kv: -kv[1]):
        print(f"      {count:>3} x {names.get(int(uid), uid)}")


if __name__ == "__main__":
    main()
