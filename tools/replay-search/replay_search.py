"""Search Mechabellum replays (*.grbr) for army compositions.

A .grbr file is a .NET BinaryFormatter stream that carries the whole match as a
single XML <BattleRecord> string, plus a DateTime right after it. Both are read
here without any dependencies -- stdlib only, no venv.

  python replay_search.py --player MakTpaxep --unit Fortress --min 4 --last 15
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
UNITS_JSON = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "Melper.Core", "Data", "units.json",
)

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


# --- main -------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default=REPLAY_DIR, help="replay folder (default: %(default)s)")
    ap.add_argument("--player", required=True, help="whose side to look at")
    ap.add_argument("--unit", required=True, help="unit name or id, e.g. Fortress or 1")
    ap.add_argument("--min", type=int, default=1, help="report armies with at least this many (default: %(default)s)")
    ap.add_argument("--last", type=int, metavar="N", help="only the N most recent matches")
    ap.add_argument("--round", choices=("last", "any"), default="last",
                    help="which round to count in (default: %(default)s)")
    ap.add_argument("-v", "--verbose", action="store_true", help="print the full army of every hit")
    args = ap.parse_args()

    names = unit_names()
    unit_id = str(resolve_unit(args.unit, names))
    unit_label = names.get(int(unit_id), unit_id)

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
        rounds = players.get(args.player, [])
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
                  f"{unit_label} {field[unit_id]}{note}  {replay.name}")
            if args.verbose:
                _print_army(field, names)

    for name, exc in skipped:
        print(f"SKIP {name}: {exc}", file=sys.stderr)
    if args.player not in seen_players:
        sys.exit(f"player {args.player!r} appears in none of the {len(scanned)} replays scanned")
    print(f"\n{hits} match(es) in {len(scanned)} replay(s)"
          f"{f', {len(skipped)} skipped' if skipped else ''}")


def _print_army(field, names):
    for uid, count in sorted(field.items(), key=lambda kv: -kv[1]):
        print(f"      {count:>3} x {names.get(int(uid), uid)}")


if __name__ == "__main__":
    main()
