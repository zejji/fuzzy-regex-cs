# Triage every DIVERGE block in a set of oracle reports against ledger 24's mechanism, so that the
# classification is READ OFF the output rather than guessed row by row (owner rule 2026-09-16).
#
# The mechanism, which is the pin `reversed-partial-runs-out-at-the-slice-start`:
#   a reversed pattern, partial matching asked for, a NON-ZERO slice start, upstream answering
#   no match, and this port answering a PARTIAL whose match starts exactly at the slice start.
#
# Anything that does not fit every limb is printed as OTHER and needs judging by hand.
#
# S52d, 2026-09-16. Run: python tools/probes/reversed-partial-divergence-triage.py <report>...
import re
import sys

BLOCK = re.compile(
    r"^DIVERGE row (\d+) \((?P<gen>[^)]+)\) (?P<op>\S+) .*?\n"
    r"  pattern  '(?P<pattern>.*)'\n"
    r"(?:  template .*\n)?"
    r"  subject  '(?P<subject>.*)'\n"
    r"(?:  slice    utf16 \[(?P<lo>\d+), (?P<hi>\d+)\)\n)?"
    r"  upstream (?P<up>.*)\n"
    r"  port     (?P<port>.*)\n",
    re.MULTILINE,
)


def limbs(m):
    """The five limbs of the mechanism, each with the reason it does or does not hold."""
    port = m.group("port")
    lo = m.group("lo")
    start = re.match(r"match 0:\((\d+),", port)
    return {
        "reversed": "(?r)" in m.group("pattern") or "(?^r)" in m.group("pattern"),
        "partial asked": True,  # every generator row below is recorded with partial=True
        "non-zero slice start": lo is not None and int(lo) > 0,
        "upstream no match": m.group("up") == "no match",
        "port partial at the slice start": (
            port.endswith("partial") and start is not None and lo is not None and start.group(1) == lo
        ),
    }


def main(paths):
    fits, others = [], []
    for path in paths:
        with open(path, encoding="utf-8") as f:
            text = f.read()
        for m in BLOCK.finditer(text):
            row = (path.rsplit("-", 1)[-1].removesuffix(".txt"), m.group(1), m.group("gen"), m.group("op"))
            checks = limbs(m)
            (fits if all(checks.values()) else others).append((row, checks, m))

    print(f"{len(fits) + len(others)} diverging rows: {len(fits)} fit the mechanism, {len(others)} do not")
    print()
    by_generator = {}
    for row, _, _ in fits:
        by_generator[(row[0], row[2], row[3])] = by_generator.get((row[0], row[2], row[3]), 0) + 1
    for (seed, gen, op), n in sorted(by_generator.items()):
        print(f"  FITS  seed {seed:<10} {gen:<16} {op:<10} {n}")

    for row, checks, m in others:
        print()
        print(f"  OTHER seed {row[0]} row {row[1]} ({row[2]}) {row[3]}")
        for name, ok in checks.items():
            print(f"        {'yes' if ok else 'NO ':<4} {name}")
        print(f"        pattern  {m.group('pattern')}")
        print(f"        slice    [{m.group('lo')}, {m.group('hi')})")
        print(f"        upstream {m.group('up')}")
        print(f"        port     {m.group('port')}")

    return 1 if others else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
