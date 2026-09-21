r"""Ask upstream each of the ten extra-wave rows again with one flag at a time deleted.

`gate-divergence-doors.py` puts every judged family's control to a row, but three of its doors -
`(?b)`, `(?e)` and `(?p)` - are flags, and on some rows more than one of them restores this port's
answer. A door that opens says the flag is INVOLVED; it does not say which flag is responsible. This
probe deletes each of the three singly and then in pairs, so the responsible one is read off the
output rather than guessed from the row's shape::

    python tools/probes/s57b-extra-wave-flag-ablations.py tools/probes/s57b-extra-wave-rows.jsonl

Two details that a hand-written ablation gets wrong:

* A FLAG IS SPELT TWO WAYS. `(?p)` inline and `regex.POSIX` in the flag word are the same flag, and
  a row can carry either - seed 99991 row 9720 has POSIX only in `flags=0x10000`, seed 57057 row
  9182 only as `(?p)`. Deleting the inline spelling alone leaves the flag set on half the rows.
* EVERY ANSWER HERE IS IN CODEPOINTS, which is what upstream counts in. The recorded
  `bestmatchFreeOutcome` and `posixFreeOutcome` fields on each row are in UTF-16, because
  `tools/record-oracle.py` converts them for this port. Compare an ablation with another ablation
  here; compare with this port through those recorded fields, never by eye across the two units.

Written by S57b sitting 6 against regex 2026.9.10, 2026-09-21.
"""

import json
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

# The three flags whose deletion these rows are judged on, each with its inline spelling and the bit
# the same flag occupies in a row's flag word (`regex.BESTMATCH`, `.ENHANCEMATCH`, `.POSIX`).
FLAGS = {"(?b)": 0x1000, "(?e)": 0x8000, "(?p)": 0x10000}
POSIX_FLAG = 0x10000
CALL_TIMEOUT = 5.0


def without(row: dict, *names: str) -> tuple[str, int]:
    """The row's pattern and flag word with each named flag deleted in both of its spellings."""
    pattern, flags = row["pattern"], row.get("flags", 0)
    for name in names:
        pattern = pattern.replace(name, "", 1)
        flags &= ~FLAGS[name]
    return pattern, flags


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"{m.span()}"]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    if m.partial:
        bits.append("PARTIAL")
    if any(m.fuzzy_counts):
        # Never `fuzzy_changes` on a POSIX match that spent an error: ledger entry 9, an access
        # violation that takes the whole process rather than raising. Same guard, same reason, as
        # `gate-divergence-doors.py:137` and `record-oracle.py:1338`.
        changes = ("unavailable (POSIX)" if m.re.flags & POSIX_FLAG else str(m.fuzzy_changes))
        bits.append(f"counts={m.fuzzy_counts} changes={changes}")
    return " ".join(bits)


def answer(row: dict, pattern: str, flags: int) -> str:
    try:
        compiled = regex.compile(pattern, flags, cache_pattern=False, **(row.get("namedLists") or {}))
        subject, operation = row["subject"], row["operation"]
        if operation in ("match", "search", "fullmatch"):
            call = getattr(compiled, operation)
            if row.get("partial"):
                return describe(call(subject, partial=True, timeout=CALL_TIMEOUT))
            return describe(call(subject, timeout=CALL_TIMEOUT))
        if operation in ("finditer", "finditer-overlapped"):
            found = list(compiled.finditer(subject, overlapped=operation.endswith("overlapped"),
                                           timeout=CALL_TIMEOUT))
            return f"{len(found)} | " + " || ".join(describe(m) for m in found)
        if operation == "split":
            parts = compiled.split(subject, row.get("count", 0) or 0, timeout=CALL_TIMEOUT)
            return f"{len(parts)} | " + " ".join(repr(p) for p in parts)
        if operation == "sub":
            return repr(compiled.subn(row["template"], subject, count=row.get("count", 0) or 0,
                                      timeout=CALL_TIMEOUT))
        return f"unhandled operation {operation}"
    except Exception as e:  # noqa: BLE001 - an exception IS upstream's answer on some rows
        return f"{type(e).__name__}: {e}"


def present(row: dict) -> list[str]:
    """The flags this row actually carries, in either spelling."""
    return [name for name, bit in FLAGS.items()
            if name in row["pattern"] or row.get("flags", 0) & bit]


def main(path: Path) -> int:
    print("regex", regex.__version__)
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    for row in rows:
        carried = present(row)
        print(f"\n--- seed {row['s57bSeed']} row {row['s57bRow']}  {row['generator']}"
              f"  {row['operation']}{' partial' if row.get('partial') else ''}"
              f"  flags={row.get('flags', 0):#x}  carries {' '.join(carried) or '(none of the three)'}")
        print("    pattern       " + ascii(row["pattern"]))
        print("    subject       " + ascii(row["subject"]) + f"   {len(row['subject'])} codepoints")
        if row.get("namedLists"):
            print("    lists         " + ascii(str(row["namedLists"])))
        if row.get("template") is not None:
            print("    template      " + ascii(row["template"]) + f"  count={row.get('count', 0)}")
        print("    as drawn      " + ascii(answer(row, row["pattern"], row.get("flags", 0))))
        for name in carried:
            print(f"    without {name}   " + ascii(answer(row, *without(row, name))))
        for i, first in enumerate(carried):
            for second in carried[i + 1:]:
                print(f"    without {first}{second} "
                      + ascii(answer(row, *without(row, first, second))))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(Path(sys.argv[1] if len(sys.argv) > 1
                               else "tools/probes/s57b-extra-wave-rows.jsonl")))
