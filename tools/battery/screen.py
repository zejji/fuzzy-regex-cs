"""The battery's second screen: rows upstream disagrees with that a decided rule still explains.

triage.py calls screen() on every row whose only failing reference is upstream (C1). The first
screen, ExpectedDivergences, keys each entry on its own; battery run 20261016-2 left about 400 rows
UNJUDGED that three judges then traced to known causes by hand. This screen does what they did, by
the same controls, and claims a row only on one of these proofs:

  ablation   one oracle-only switch, or a pair of them together, turns this port's fixes off and
             gives upstream's answer exactly: span, partial mark, counts, change positions and
             every capture list (tools/battery/abl.cs). Each switch keys an ExpectedDivergences
             entry (SWITCHES below). A pair is what rows with two mechanisms need (run 2: ledger 64
             with 65 on 36 pair rows, 44 with 42 or H' on 5).
  twin       a twin of the row, changed in the one construct a known upstream defect needs, gets
             the same answer from both engines, and that answer is this port's answer to the row
             itself. Three twins (TWINS below): (*SKIP) made (*PRUNE) for ledger 5's partial door,
             lazy repeats made greedy for ledger 2/27's phantom partial, and \\K removed for ledger
             83 (D86) under BESTMATCH or ENHANCEMATCH, where this port's answer is the twin's with
             only the start moved (the end, reversed).

What it never claims: a row an open register row explains. OPEN lists them with the shape that
gives them away; a hit that matches one is reported, not claimed (D96: rule H' reads the start a
\\K moved, so the H'-off switch reproduces upstream on exactly those rows).

Upstream runs one row per process, killed above 2 GB or 20 s (one row took it to 19 GB on
2026-10-17). The port runs in one Release process over every row, with a per-row budget for the
pair search.
"""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import psutil

HERE = Path(__file__).resolve().parent
UPSTREAM_CAP_BYTES = 2 * 1024**3
UPSTREAM_CAP_SECONDS = 20.0

# Each switch abl.cs knows, and the ExpectedDivergences entry (with its ledger entry or decision)
# that keys on it. A claim names these.
SWITCHES = {
    "SkipMovesTheSliceWhenItRuns": "skip-acts-when-backtracked-onto",
    "VerbsAreConfinedToTheInnermostGroup": "verb-unwinds-through-unfinished-groups (ledger 45)",
    "KeepOutlivesALookaround": "keep-undone-past-a-lookaround (ledger 86)",
    "KeepOutlivesAnAtomicGroup": "keep-undone-past-an-atomic-group",
    "SkipsALazyRunDespiteAVerb": "lazy-start-skip-passes-a-verb-cut-run (ledger 76)",
    "StepsOnFromTheKeptStart": "keep-before-a-verb-skips-no-start (D92)",
    "BansAnEmptyMatchAtEveryCandidate": "bestmatch-skips-after-an-empty-match (ledger 77)",
    "AnchorsBackslashGAtEveryCandidate": "bestmatch-search-anchor-moves-per-candidate (ledger 78)",
    "KeepSectionOpenAfterAVerb": "fuzzy-section-cut-by-a-verb-is-closed",
    "MatchMayEndInsideTheWholePatternCall": "fuzzy-whole-pattern-call-returns-to-its-caller",
    "KeepDiscardedTotals": "fuzzy-total-drops-a-discarded-section",
    "DropNegativeTestCaptures": "negative-condition-keeps-its-test-captures (ledger 65)",
    "SkipRetriedFoldSteps": "full-fold-backreference-retry",
    "DoubleCountTrailingInsertions": "bestmatch-loses-a-candidate (ledger 12)",
    "UpstreamDefaultBoundary": "default-word-boundary-follows-uax29",
    "SecondPassAsBeforeD11": "partial-edge-read (D11)",
    "TailRunOutAsBeforeD67": "partial-group-before-a-repeat-tail (ledger 74)",
    "RunOutAsBeforeD81": "D81 run-out checks the open sections' limits",
    "SkipExactDeletionRetry": "fuzzy-exact-item-offered-as-a-deletion (ledger 42)",
    "SkipLookaroundInsertion": "fuzzy-insertion-before-a-failing-lookaround",
    "KeepCallCaptures": "group-call-discards-its-captures-on-return (ledger 63)",
    "PartialRestoresFrameByFrame": "group-call-discards-its-captures-on-return, partial (ledger 63)",
    "CheckMinimumBeforeTrailingInsertions": "fuzzy-minimum-met-by-a-trailing-insertion (ledger 51)",
    "UpstreamEmptyIterations": "fuzzy-empty-iteration-needed-rule (ledger 44)",
    "ChargeUntouchedFoldings": "full-fold-fix-behind-an-innermost-count",
    "SkipGroupFoldLeftovers": "full-fold-backreference-leftovers",
    "SkipLeftoverTakeBack": "full-fold-leftover-take-back",
    "SkipWholeFoldedCharEdits": "full-fold-run-edits-an-expanding-subject-character-whole (ledger 49)",
    "CallGuardComparesAcrossSections": "call-reentry-opening-a-section-is-refused (D93)",
    "upstreamReverseGrapheme": "reverse-grapheme-takes-the-whole-cluster",
    "upstreamFoldedRuns": "full-fold-run-edits-an-expanding-character-whole",
    "upstreamWholePatternCall": "fuzzy-whole-pattern-call-returns-to-its-caller",
    "upstreamCallFeatures": "group-call-runs-with-its-call-sites-features",
    "upstreamFuzzyConditionTest": "conditional-test-is-matched-exactly (ledger 64)",
    "upstreamCallCopies": "group-call-copy-is-the-group-written-out",
    "noLift": "fuzzy-insertion-at-a-pinned-anchor (rule H', D61)",
    "d85": "keyed-tail-guard (ledger 84)",
}

_BESTMATCH, _ENHANCEMATCH = 0x1000, 0x8000
_INLINE_BE = re.compile(r"\(\?[a-zA-Z0-9]*[be][a-zA-Z0-9]*[:)]")


def has_keep(p: str) -> bool:
    return "\\K" in p


def best_or_enhance(row: dict) -> bool:
    return bool(int(row.get("flags") or 0) & (_BESTMATCH | _ENHANCEMATCH)) or bool(_INLINE_BE.search(row["pattern"]))


def reversed_row(row: dict) -> bool:
    return bool(int(row.get("flags") or 0) & 0x400) or bool(re.search(r"\(\?[a-zA-Z0-9]*r[a-zA-Z0-9]*[:)]", row["pattern"]))


# Open register rows that a switch reproduces: (switch, test(row), register row). A hit on one is
# the open defect, not the decided rule the switch keys.
OPEN = [
    ("noLift", lambda r: has_keep(r["pattern"]), "D96 (open): rule H' reads the start a \\K moved"),
]


def greedy(p: str) -> str | None:
    """The pattern with every lazy quantifier made greedy, or None if it has none. Escapes and sets
    are copied as they are; a `?` is dropped only where it follows a quantifier."""
    out, i, changed, after_quantifier = [], 0, False, False
    while i < len(p):
        ch = p[i]
        if ch == "\\":
            out.append(p[i:i + 2])
            i += 2
            after_quantifier = False
            continue
        if ch == "[":
            j, depth = i + 1, 1
            if j < len(p) and p[j] == "^":
                j += 1
            if j < len(p) and p[j] == "]":
                j += 1
            while j < len(p) and depth:
                if p[j] == "\\":
                    j += 2
                    continue
                if p[j] == "[":
                    depth += 1
                elif p[j] == "]":
                    depth -= 1
                j += 1
            out.append(p[i:j])
            i = j
            after_quantifier = False
            continue
        if ch == "?" and after_quantifier:
            changed = True
            i += 1
            after_quantifier = False
            continue
        if ch in "*+?" and not (ch == "?" and out and out[-1].endswith("(")):
            out.append(ch)
            i += 1
            after_quantifier = True
            continue
        if ch == "{":
            m = re.match(r"\{\d*(,\d*)?\}", p[i:])
            if m and m.group(0) != "{}":
                out.append(m.group(0))
                i += len(m.group(0))
                after_quantifier = True
                continue
        out.append(ch)
        i += 1
        after_quantifier = False
    return "".join(out) if changed else None


_SCANS = ("sub", "subf", "split")


def as_scan(r: dict) -> dict:
    """A sub or split row asked as the finditer that finds its matches: those are what a \\K moves."""
    return {**r, "operation": "finditer"} if r["operation"] in _SCANS else r


def first(answer: str | None, count: int) -> str | None:
    """A finditer answer cut to its first `count` matches (a sub or split with a count makes only
    those); the answer itself for a count of 0."""
    if not count or answer is None or not answer.startswith("["):
        return answer
    found = [] if answer == "[]" else answer[1:-1].split(" | ")
    return "[" + " | ".join(found[:count]) + "]"


def _keep_twin(r: dict) -> dict | None:
    if not (has_keep(r["pattern"]) and best_or_enhance(r)):
        return None
    return {**as_scan(r), "pattern": r["pattern"].replace("\\K", "")}


def _pattern_twin(change):
    def make(r: dict) -> dict | None:
        p = change(r)
        return {**r, "pattern": p} if p is not None and p != r["pattern"] else None
    return make


# The twins: (name, make(row) -> the twin row or None, how the port's answer must relate, label).
# "same": the port's answer to the row is the twin's. "moved": the port's answer to the row (asked
# as its finditer, for sub and split) is the twin's with only the starts moved.
TWINS = [
    ("prune", _pattern_twin(lambda r: r["pattern"].replace("(*SKIP)", "(*PRUNE)") if r.get("partial") else None),
     "same", "ledger 5: a partial pass runs on the slice a (*SKIP) moved (the (*PRUNE) twin agrees)"),
    ("greedy", _pattern_twin(lambda r: greedy(r["pattern"]) if r.get("partial") else None),
     "same", "ledger 2/27: a lazy repeat reports a partial no text can complete (the greedy twin agrees)"),
    ("nok", _keep_twin,
     "moved", "ledger 83 (D86): BESTMATCH or ENHANCEMATCH with a \\K start (the \\K-free twin agrees, start moved)"),
]


_MATCH = re.compile(r"^(P?)\((\d+),(\d+)\)(.*)$")


def matches(answer: str):
    """The matches of an answer as (partial, start, end, rest), or None if it is not a match answer."""
    if answer == "None":
        return []
    parts = [answer]
    if answer.startswith("["):
        parts = [] if answer == "[]" else answer[1:-1].split(" | ")
    out = []
    for p in parts:
        m = _MATCH.match(p)
        if not m:
            return None
        out.append((m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)))
    return out


def is_answer(a: str | None) -> bool:
    return a is not None and (a == "None" or a.startswith(("(", "P(", "[")))


def is_reply(a: str | None) -> bool:
    """A match answer, or a sub's or split's result: an answer, not an error or a killed run."""
    return is_answer(a) or (a is not None and a.startswith(("sub ", "split ")))


def start_moved_only(port: str, twin: str, reverse: bool) -> bool:
    """Whether the port's answer is the twin's with only each match's start moved on (its end moved
    back, reversed): the same matches, ends, counts, change positions and captures."""
    a, b = matches(port), matches(twin)
    if a is None or b is None or len(a) != len(b):
        return False
    for (pa, sa, ea, ra), (pb, sb, eb, rb) in zip(a, b):
        if pa != pb or ra != rb:
            return False
        if reverse and not (sa == sb and ea <= eb):
            return False
        if not reverse and not (ea == eb and sa >= sb):
            return False
    return True


_COUNTS = re.compile(r"^\((\d+),(\d+),(\d+)\) \[([\d,]*);([\d,]*);([\d,]*)\]")


def contradicts_its_counts(answer: str) -> bool:
    """Whether some match lists a different number of changes of a kind than its counts say."""
    for _p, _s, _e, rest in matches(answer) or []:
        m = _COUNTS.match(rest.strip())
        if m and any(int(m.group(i)) != len([x for x in m.group(i + 3).split(",") if x]) for i in (1, 2, 3)):
            return True
    return False


def classify(row: dict, up: str, port: str, hits: list[str], twin_up: dict, twin_port: dict) -> tuple[str, str] | None:
    """(claim, detail) for a row this screen explains, ("open", register row) for one an open
    register row explains, or None."""
    if not (is_reply(up) and is_reply(port)) or up == port:
        return None
    opened = []
    good = []
    # A hit that differs from upstream only in change positions counts where upstream's own list
    # contradicts its own counts, which no single path can give (ledger entry 11).
    leaked = contradicts_its_counts(up)
    if hits == ["~base"]:
        if not leaked:
            return None
        return ("leak", "ledger 11: only the change positions differ, and upstream's list contradicts its own counts")
    for h in hits:
        if h == "cut" or (h.startswith("~") and not leaked):
            continue
        names = h.lstrip("~").split("+")
        hit_open = [why for s, test, why in OPEN if s in names and test(row)]
        if hit_open:
            opened += hit_open
        else:
            good.append((h.startswith("~"), len(names), names))
    if good:
        loose, _n, best = min(good)
        detail = " + ".join(SWITCHES.get(n, n) for n in best)
        return ("ablation", detail + (", upstream's change list contradicting its counts (ledger 11)" if loose else ""))
    if opened:
        return ("open", opened[0])
    n = int(row.get("count") or 0) if row["operation"] in _SCANS else 0
    for name, _make, relation, label in TWINS:
        tu, tp = first(twin_up.get(name), n), first(twin_port.get(name), n)
        if not (is_answer(tu) and is_answer(tp)):
            continue
        if tu != tp and not (without_positions(tu) == without_positions(tp) and contradicts_its_counts(tu)):
            continue
        if relation == "same" and port == tp:
            return ("twin", label)
        if relation == "moved":
            # A sub or split is judged by the matches it is made of: its finditer form must differ
            # from upstream's too, or the divergence is somewhere else.
            mine, theirs = (twin_port.get("scan"), twin_up.get("scan")) if row["operation"] in _SCANS else (port, up)
            mine, theirs = first(mine, n), first(theirs, n)
            if is_answer(mine) and mine != theirs and start_moved_only(mine, tp, reversed_row(row)):
                return ("twin", label)
    return None


def without_positions(answer: str) -> str:
    return re.sub(r" \[[\d,]*;[\d,]*;[\d,]*\]", "", answer)


# ----------------------------------------------------------------------------------------------
# Running the engines


def upstream_answers(rows: list[dict], work: Path, log=print) -> dict:
    """{id: upstream's answer}, one row per process, each killed above the memory or time cap."""
    work.mkdir(parents=True, exist_ok=True)
    out = {}
    for r in rows:
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8", dir=work) as t:
            t.write(json.dumps(r, ensure_ascii=True))
        child = subprocess.Popen([sys.executable, str(HERE / "upstream_row.py"), t.name], stdout=subprocess.PIPE,
                                 stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL, cwd=work,
                                 env={**os.environ, "PYTHONUTF8": "1", "PYTHONDONTWRITEBYTECODE": "1"})
        proc, start, why = psutil.Process(child.pid), time.time(), None
        while child.poll() is None:
            try:
                mem = sum(p.memory_info().rss for p in [proc] + proc.children(recursive=True))
            except psutil.Error:
                mem = 0
            if mem > UPSTREAM_CAP_BYTES:
                why = "RESOURCE-KILLED"
            elif time.time() - start > UPSTREAM_CAP_SECONDS:
                why = "TIMEOUT-KILLED"
            if why:
                for p in proc.children(recursive=True) + [proc]:
                    try:
                        p.kill()
                    except psutil.Error:
                        pass
                break
            time.sleep(0.02)
        text = child.stdout.read().decode("utf-8", "replace").strip()
        child.wait()
        os.unlink(t.name)
        parts = text.split("\t")
        out[r["id"]] = parts[4] if not why and len(parts) >= 5 else (why or f"CRASH exit {child.returncode}")
        if why:
            log(f"screen: upstream {why} on row {r['id']}")
    return out


def port_answers(rows: list[dict], up: dict, work: Path, sut: Path) -> dict:
    """{id: (answer, [hits])} from abl.cs, built and run in the worktree of the commit under test."""
    work.mkdir(parents=True, exist_ok=True)
    d = sut / ".battery"
    d.mkdir(exist_ok=True)
    shutil.copy2(HERE / "abl.cs", d / "abl.cs")
    src, ups, dst = work / "port-in.jsonl", work / "port-up.tsv", work / "port-out.tsv"
    with open(src, "w", encoding="utf-8", newline="\n") as f:
        for r in rows:
            f.write(json.dumps(r, ensure_ascii=True) + "\n")
    with open(ups, "w", encoding="utf-8", newline="\n") as f:
        for k, a in up.items():
            f.write(f"{k}\t-\t-\t-\t{a}\n")
    env = {**os.environ, "DOTNET_GCHeapHardLimit": "0xC0000000",
           "PATH": r"C:\Program Files\dotnet" + os.pathsep + os.environ.get("PATH", "")}
    with open(work / "abl.log", "wb") as lf:
        subprocess.run(["dotnet", "run", "-c", "Release", str(d / "abl.cs"), "--", str(src), str(ups), str(dst)],
                       cwd=sut, env=env, stdin=subprocess.DEVNULL, stdout=lf, stderr=subprocess.STDOUT)
    out = {}
    if dst.exists():
        for line in open(dst, encoding="utf-8"):
            p = line.rstrip("\n").split("\t")
            if len(p) == 3:
                out[p[0]] = (p[1], [] if p[2] == "-" else p[2].split(","))
    return out


# ----------------------------------------------------------------------------------------------
# POSIX (ledger entry 9): upstream may charge a POSIX match more than its own POSIX-free engine
# charges the same span, because restore_best_match keeps a losing candidate's total
# (_regex.c:11564-11576). POSIX chooses which match, never what a given match costs, so the control
# is the POSIX-free fullmatch over each of the port's spans. ExpectedDivergences reads the recorded
# POSIX-free answer to the same question, which can be a different, shorter match (battery run
# 20261016-2, A1: POSIX (0,5), POSIX-free search (0,4)), so it missed A1, A8 and A29.

_POSIX = 0x10000
_INLINE_FLAGS = re.compile(r"\(\?([a-zA-Z0-9]*)\)")


def is_posix(row: dict) -> bool:
    return bool(int(row.get("flags") or 0) & _POSIX) or any("p" in m.group(1) for m in _INLINE_FLAGS.finditer(row["pattern"]))


def without_posix(row: dict) -> dict:
    def drop(m):
        rest = m.group(1).replace("p", "")
        return f"(?{rest})" if rest else ""
    return {**row, "pattern": _INLINE_FLAGS.sub(drop, row["pattern"]), "flags": int(row.get("flags") or 0) & ~_POSIX}


def posix_span_questions(row: dict, port: str | None) -> list[dict]:
    """The POSIX-free fullmatch over each of the port's match spans."""
    found = matches(port) if port else None
    if (not is_posix(row) or row.get("partial") or not found
            or row["operation"] not in ("search", "match", "fullmatch", "finditer")):
        return []
    free = without_posix(row)
    q = {k: free[k] for k in QUESTION if k in free}
    return [{**q, "operation": "fullmatch", "pos": s, "endpos": e, "plain": True} for _p, s, e, _r in found]


def _errors(rest: str) -> int:
    m = re.match(r"\s*\((\d+),(\d+),(\d+)\)", rest)
    return sum(int(x) for x in m.groups()) if m else 0


def posix_overcharge(up: str | None, port: str | None, free_up: list, free_port: list) -> bool:
    """Whether upstream's POSIX answer has the port's spans at a higher cost, and both engines'
    POSIX-free fullmatch over each span is the port's match."""
    mu, mp = matches(up) if up else None, matches(port) if port else None
    if not mu or not mp or len(mu) != len(mp) or len(free_up) != len(mp) or len(free_port) != len(mp):
        return False
    dearer = False
    for (pu, su, eu, ru), (pp, sp, ep, rp), fu, fp in zip(mu, mp, free_up, free_port):
        if (pu, su, eu) != (pp, sp, ep) or not is_answer(fu) or fu != fp:
            return False
        if without_positions(f"{pp}({sp},{ep}){rp}") != without_positions(fp):
            return False
        if _errors(ru) < _errors(rp):
            return False
        dearer |= _errors(ru) > _errors(rp)
    return dearer


QUESTION = ("pattern", "flags", "subject", "operation", "partial", "pos", "endpos", "namedLists", "template", "count")


def screen(rows: list[dict], work: Path, sut: Path, log=print) -> dict:
    """{row id: (claim, detail)} for the rows this screen explains or an open register row does.
    Every row needs an `id`; the rest is the question, as the oracle and run.py record it."""
    asked, twins = [], {}
    for r in rows:
        q = {k: r[k] for k in QUESTION if k in r}
        asked.append({"id": str(r["id"]), **q})
        made = [(name, make(q)) for name, make, _rel, _label in TWINS]
        if any(name == "nok" and t for name, t in made) and r["operation"] in _SCANS:
            made.append(("scan", as_scan(q)))
        for name, t in made:
            if t is not None:
                asked.append({"id": f"{r['id']}~{name}", **t, "plain": True})
                twins.setdefault(str(r["id"]), []).append(name)
    if not asked:
        return {}
    t = time.time()
    up = upstream_answers(asked, work / "up", log)
    log(f"screen: upstream answered {len(asked)} questions in {time.time() - t:.0f} s")
    t = time.time()
    port = port_answers(asked, {k: v for k, v in up.items() if "~" not in k}, work, sut)
    log(f"screen: the port answered {len(port)} of {len(asked)} in {time.time() - t:.0f} s")
    # A second round for POSIX rows, whose questions depend on the port's spans.
    second = {}
    for r in rows:
        k = str(r["id"])
        second[k] = [{"id": f"{k}~posix{n}", **q} for n, q in enumerate(posix_span_questions(r, port.get(k, (None, []))[0]))]
    flat = [q for qs in second.values() for q in qs]
    if flat:
        up.update(upstream_answers(flat, work / "up2", log))
        port.update(port_answers(flat, {}, work / "posix", sut))
    out = {}
    with open(work / "screen.jsonl", "w", encoding="utf-8", newline="\n") as f:
        for r in rows:
            k = str(r["id"])
            pa, hits = port.get(k, (None, []))
            tu = {n: up.get(f"{k}~{n}") for n in twins.get(k, [])}
            tp = {n: port.get(f"{k}~{n}", (None, []))[0] for n in twins.get(k, [])}
            verdict = classify(r, up.get(k), pa, hits, tu, tp)
            if second.get(k):
                tu["posix"] = [up.get(q["id"]) for q in second[k]]
                tp["posix"] = [port.get(q["id"], (None, []))[0] for q in second[k]]
                if verdict is None and posix_overcharge(up.get(k), pa, tu["posix"], tp["posix"]):
                    verdict = ("twin", "ledger 9: POSIX charges more for the same span (the POSIX-free fullmatch "
                                       "over each span is the port's match on both engines)")
            f.write(json.dumps({"id": k, "up": up.get(k), "port": pa, "hits": hits, "twinUp": tu, "twinPort": tp,
                                "verdict": verdict}, ensure_ascii=True) + "\n")
            if verdict:
                out[k] = verdict
    return out
