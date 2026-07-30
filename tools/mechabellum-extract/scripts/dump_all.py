"""Extract Mechabellum balance tables from the game client into JSON.

The game is Unity 2022.3 / IL2CPP with TypeTrees stripped from the build, so:
  1. the field layout is regenerated from GameAssembly.dll + global-metadata.dat;
  2. the raw MonoBehaviour bytes are walked by hand (UnityPy's own reader cannot
     cope with how the generator names container nodes).

Target objects are located by MonoScript class name, NOT by hard-coded path_id,
so this keeps working after a game patch renumbers the objects.

Output: ../data/config_raw.json
"""
import UnityPy, os, json, struct, sys, io
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

GAME = r"D:\SteamLibrary\steamapps\common\Mechabellum"
UNITY_VERSION = "2022.3.62f3"
SCENE = "level0"          # every config container lives in the boot scene
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "data", "config_raw.json")

# Located by class name; path_ids are discovered at runtime.
TARGET_CLASSES = [
    "ConfigDataContainer",        # 58 tables: units, cards, buffs, officers, shop
    "MechSkillGroupData",         # weapons: range, attack interval, splash
    "TechnologyGroupData",        # unit upgrades
    "EquipmentGroupData",
    "CommanderSkillGroupData",
    "ContraptionGroupData",
]

ALIGN = 0x4000
FP_ONE = 4294967296.0  # FPoint is Q32.32 fixed point
PRIM = {"int": ("<i", 4), "unsigned int": ("<I", 4), "SInt32": ("<i", 4), "UInt32": ("<I", 4),
        "float": ("<f", 4), "SInt64": ("<q", 8), "UInt64": ("<Q", 8), "double": ("<d", 8),
        "SInt16": ("<h", 2), "UInt16": ("<H", 2), "UInt8": ("<B", 1), "SInt8": ("<b", 1),
        "bool": ("<?", 1), "char": ("<B", 1)}


class Node:
    __slots__ = ("t", "n", "lvl", "mf", "kids")

    def __init__(s, d):
        s.t, s.n, s.lvl, s.mf = d["m_Type"], d["m_Name"], d["m_Level"], d["m_MetaFlag"]
        # GOTCHA 1: the generator synthesises the MonoBehaviour header without the
        # align flag on m_Enabled; real Unity typetrees always carry it. Without
        # this, every field after the header is read 3 bytes off.
        if s.n == "m_Enabled" and s.lvl == 1:
            s.mf |= ALIGN
        s.kids = []


def build_tree(raw):
    nodes = [Node(d) for d in raw]
    stack = [nodes[0]]
    for nd in nodes[1:]:
        while len(stack) > nd.lvl:
            stack.pop()
        stack[-1].kids.append(nd)
        stack.append(nd)
    return nodes[0]


class Reader:
    def __init__(s, buf):
        s.buf, s.pos = buf, 0

    def align(s):
        s.pos = (s.pos + 3) & ~3

    def read(s, node):
        buf = s.buf
        if node.t in PRIM:
            fmt, sz = PRIM[node.t]
            v = struct.unpack_from(fmt, buf, s.pos)[0]
            s.pos += sz
            if node.mf & ALIGN:
                s.align()
            return v

        is_arr = len(node.kids) == 1 and node.kids[0].n == "Array"
        elem = node.kids[0].kids[1] if is_arr else None

        # GOTCHA 2+3: a real string is m_Type "string" that is either childless
        # (the synthesised header field) or an array of char. List<string> carries
        # the very same m_Type but has string elements, so it must fall through to
        # the array branch -- reading it as a string desyncs everything after it.
        if node.t == "string" and (not is_arr or elem.t == "char"):
            n = struct.unpack_from("<i", buf, s.pos)[0]
            s.pos += 4
            v = buf[s.pos:s.pos + n].decode("utf-8", "replace")
            s.pos += n
            s.align()
            return v

        if is_arr:
            cnt = struct.unpack_from("<i", buf, s.pos)[0]
            s.pos += 4
            if cnt < 0 or cnt > 2_000_000:
                raise ValueError(f"insane count {cnt} at {node.n} pos {s.pos - 4}")
            out = [s.read(elem) for _ in range(cnt)]
            if node.kids[0].mf & ALIGN:
                s.align()
            return out

        d = {k.n: s.read(k) for k in node.kids}
        if node.mf & ALIGN:
            s.align()
        if len(d) == 1 and "m_rawValue" in d:
            # FPoint -> real number. Rounding hides the fixed-point residue that
            # would otherwise show 0.2 as 0.19999999995343387.
            return round(d["m_rawValue"] / FP_ONE, 6)
        return d


def discover_scripts(data_dir):
    """path_id -> (class, namespace, assembly) for every MonoScript in the build."""
    env = UnityPy.load(os.path.join(data_dir, "globalgamemanagers.assets"))
    idx = {}
    for o in env.objects:
        if o.type.name != "MonoScript":
            continue
        try:
            d = o.read_typetree()
        except Exception:
            continue
        idx[o.path_id] = (d.get("m_ClassName", ""), d.get("m_Namespace", ""),
                          d.get("m_AssemblyName", "").replace(".dll", ""))
    return idx


def main():
    data_dir = next(os.path.join(GAME, f) for f in os.listdir(GAME) if f.endswith("_Data"))
    scripts = discover_scripts(data_dir)
    print(f"indexed {len(scripts)} MonoScripts")

    env = UnityPy.load(os.path.join(data_dir, SCENE))
    found = {}
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            pid = o.read(check_read=False).m_Script.path_id
        except Exception:
            continue
        info = scripts.get(pid)
        if info and info[0] in TARGET_CLASSES and info[0] not in found:
            found[info[0]] = (o, info)

    missing = [c for c in TARGET_CLASSES if c not in found]
    if missing:
        print(f"WARNING: not found in {SCENE}: {missing}")

    gen = TypeTreeGenerator(UNITY_VERSION)
    gen.load_local_game(GAME)

    result, failures = {}, []
    for cls, (obj, (name, ns, asm)) in found.items():
        fq = f"{ns}.{name}" if ns else name
        try:
            raw = json.loads(gen.get_nodes_as_json(asm, fq))
        except Exception as e:
            failures.append(f"{cls}: node generation failed ({e})")
            continue
        r = Reader(obj.get_raw_data())
        try:
            tree = {k.n: r.read(k) for k in build_tree(raw).kids}
        except Exception as e:
            failures.append(f"{cls}: parse failed ({e})")
            continue
        # Consuming every byte is the correctness check: a wrong layout desyncs
        # and leaves a remainder behind (or overruns the buffer).
        if r.pos != len(r.buf):
            failures.append(f"{cls}: consumed {r.pos}/{len(r.buf)} - layout mismatch")
            continue
        result[cls] = tree
        tables = sum(1 for v in tree.values() if isinstance(v, list) and v)
        print(f"  {cls:<26} path_id={obj.path_id:<5} {len(r.buf):>8} bytes OK, {tables} tables")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=1)
    print(f"\nwrote {os.path.normpath(OUT)}  ({len(result)}/{len(TARGET_CLASSES)} containers)")
    for msg in failures:
        print("  FAIL", msg)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
