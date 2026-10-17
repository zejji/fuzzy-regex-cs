"""Upstream's answer to one row, in the descriptor tools/battery/abl.cs writes for the port.

    python tools/battery/upstream_row.py ROW.json        # prints: id, operation, pattern, subject, answer

One row per process, so screen.py can cap each one: a single row took regex 2026.9.10 to 19 GB in
battery run 20261016-2's triage (a `(?b)(?r)` pattern with nested fuzzy sections and its verbs
deleted), and rows that crash `regex` outright are ledger entries 9 and 56.

The descriptor: P for a partial match, the span in UTF-16 units, then for a match with errors the
counts and the change lists ([substitutions;insertions;deletions]), then every group's capture list.
"""

import json
import sys

POSIX = 0x10000


def u16(s: str, i: int) -> int:
    # A deletion can lie past the end of the subject (upstream shifts each by the deletions before
    # it), so the overhang counts as single units instead of being clamped at len(s).
    return sum(2 if ord(ch) > 0xFFFF else 1 for ch in s[:i]) + max(0, i - len(s))


def cp(s: str, unit: int) -> int:
    """A UTF-16 offset as a codepoint index."""
    i = u = 0
    while i < len(s) and u < unit:
        u += 2 if ord(s[i]) > 0xFFFF else 1
        i += 1
    return i


def describe(m, s: str) -> str:
    if m is None:
        return "None"
    a, b = m.span()
    fuzzy = ""
    counts = tuple(getattr(m, "fuzzy_counts", (0, 0, 0)))
    if sum(counts) and m.re.flags & POSIX:
        # Reading a POSIX match's changes crashes regex 2026.9.10 (ledger entry 9), and it keeps
        # none worth reading: the counts alone, marked so no port answer can equal it.
        fuzzy = " (%d,%d,%d) [posix]" % counts
    elif sum(counts):
        changes = m.fuzzy_changes
        fuzzy = " (%d,%d,%d)" % counts + " [" + ";".join(",".join(str(u16(s, x)) for x in c) for c in changes) + "]"
    groups = "".join(" g%d=" % i + ",".join("(%d,%d)" % (u16(s, x), u16(s, y)) for x, y in m.spans(i))
                     for i in range(1, m.re.groups + 1))
    return "%s(%d,%d)%s%s" % ("P" if m.partial else "", u16(s, a), u16(s, b), fuzzy, groups)


def quote(s: str) -> str:
    return json.dumps(s, ensure_ascii=True)


def answer(r: dict) -> str:
    import regex
    pattern, s, op = r["pattern"], r["subject"], r.get("operation", "search")
    kw = {"partial": True} if r.get("partial") else {}
    span = []
    if r.get("pos") is not None or r.get("endpos") is not None:
        span = [cp(s, r.get("pos") or 0), cp(s, r["endpos"]) if r.get("endpos") is not None else len(s)]
    lists = r.get("namedLists") or {}
    try:
        rx = regex.compile(pattern, int(r.get("flags") or 0), **({"ignore_unused": True} if lists else {}), **lists)
        if op in ("search", "match", "fullmatch"):
            return describe(getattr(rx, op)(s, *span, **kw), s)
        if op in ("finditer", "finditer-overlapped"):
            if op == "finditer-overlapped":
                kw["overlapped"] = True
            return "[" + " | ".join(describe(m, s) for m in rx.finditer(s, *span, **kw)) + "]"
        if op in ("sub", "subf"):
            out, k = (rx.subn if op == "sub" else rx.subfn)(r["template"], s, r.get("count", 0), *span)
            return "sub %d %s" % (k, quote(out))
        if op == "split":
            return "split " + " ".join("<null>" if x is None else quote(x) for x in rx.split(s, r.get("count", 0)))
        return "unknown op"
    except Exception as e:  # noqa: BLE001 - upstream's refusal is its answer
        return "%s: %s" % (type(e).__name__, str(e).replace("\t", " ").replace("\n", " "))


def main(path: str) -> int:
    r = json.loads(open(path, encoding="utf-8").read())
    a = answer(r)
    sys.stdout.write("%s\t%s\t%s\t%s\t%s\n" % (r["id"], r.get("operation"), quote(r["pattern"]), quote(r["subject"]), a))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
