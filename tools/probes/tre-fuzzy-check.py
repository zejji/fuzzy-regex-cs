"""Asks TRE, the second FUZZY engine, about the fuzzy-CORE rows of a wave or a rows file.

S52c scope item 5. On a fuzzy row upstream is the only engine on Windows that does approximate
matching at all (`docs/plan/OPERATIONS.md`), so amendment 16's "a second engine agrees" cannot be
satisfied there by PCRE2, Perl or .NET - every one of them answers `{e<=n}` with a syntax error.
TRE 0.8.0 in WSL is the one engine that does: it is the library mrab-regex's fuzzy feature was
modelled on, and its `Fuzzyness(maxerr=, maxsub=, maxins=, maxdel=)` maps onto `{e,s,i,d<=n}`.

ONE WSL PROCESS PER BATCH, never one per row: a `wsl` start is about 200 ms. This file is both
halves - the Windows side classifies and batches, then re-invokes ITSELF inside WSL with
`--in-wsl`, which is the only part that imports `tre`.

WHAT IT CAN AND CANNOT ANSWER, stated before the numbers because the gate is most of this file:

    can     does a match exist within the budget, and what is the CHEAPEST cost of one
    cannot  anything outside the fuzzy core - TRE's dialect is POSIX ERE, so no `\\p{...}`, no
            backreference, no lookaround, no verb, no `\\K`, no named list, no group call; and TRE
            has no BESTMATCH, no ENHANCEMATCH, no per-section budget, no `fuzzy_changes` (it
            reports only a total `cost`), and no partial matching
    cannot  an ANCHORED question. TRE offers a search and nothing else, and what anchoring does
            INSIDE a fuzzy region is not measured here, so a `match` or `fullmatch` row is reported
            OUT OF DIALECT rather than answered by a guess. Widening this needs a measurement, not
            a code change.

A row the gate rejects is reported OUT OF DIALECT with the reason, never silently dropped: a
second engine that quietly answers nothing looks exactly like a second engine that agrees.

Run:
    python tools/probes/tre-fuzzy-check.py --self-test
    python tools/probes/tre-fuzzy-check.py TestResults/oracle/wave-inv-7.jsonl
    python tools/probes/tre-fuzzy-check.py --rows .scratch/violations.jsonl
"""

import json
import re
import subprocess
import sys
from pathlib import Path

# The venv the orchestrator built the binding into; see docs/plan/OPERATIONS.md, "TRE is the second
# FUZZY engine". `wsl` runs its command through bash, so the tilde is expanded on the WSL side.
WSL_PYTHON = "~/.venvs/tre/bin/python"

# One fuzzy budget applied to the WHOLE pattern, which is the only shape TRE can be asked: a single
# `Fuzzyness` governs a whole match, so a pattern with a budget on one section and not another is a
# different question. `(?:BODY){e<=1,s<=0}` and `(BODY){e<=2}`.
_WHOLE_PATTERN_BUDGET = re.compile(r"^\((?:\?:)?(?P<body>.*)\)\{(?P<budget>[eisd0-9<=, ]+)\}$")
_BUDGET_TERM = re.compile(r"^(?P<kind>[eisd])<=(?P<limit>\d+)$")

# The POSIX ERE core, and nothing else. No backslash at all: an escape means either a class shorthand
# TRE does not have (`\d`, `\p{L}`), a backreference ERE does not have, or a literal whose meaning
# would have to be re-derived - and a second engine asked a slightly different question is worse than
# no second engine.
_CORE_BODY = re.compile(r"^[A-Za-z0-9 \[\]()|.^$*+?:,_-]*$")

# The ONE inline flag TRE has an equivalent for. `(?i)` is `tre.ICASE`; `(?f)` is full case folding,
# which it does not have, and `(?b)`, `(?e)`, `(?r)`, `(?p)` are flags with no TRE concept at all.
_LEADING_ICASE = re.compile(r"^\(\?i\)")

# A lazy or possessive quantifier. POSIX ERE has neither, and the gate's FIRST version let them
# through: TRE answered `Error:` on `(?:a*?){s<=1,i<=1,d<=1}` and on six other rows of the seed-7
# wave, which is the engine refusing a question the gate should never have asked. It was the empty
# TRE error message that gave it away, not the gate.
_NOT_ERE_QUANTIFIER = re.compile(r"[*+?][?+]")

# Any group that is not a plain `(...)` or `(?:...)`. The gate's first version tested for this on the
# BODY, after the budget regex had already stripped a leading `(?i)`'s own parenthesis and left
# `?i)(?:fo` behind as the "body" - which passed the character-class test, because `?`, `i` and `)`
# are all ordinary ERE characters. Testing the WHOLE pattern is what closes it.
_EXOTIC_GROUP = re.compile(r"\((?!\?:)\?")


def _dialect(row: dict) -> tuple[str, str] | str:
    """The TRE pattern and budget for a row, or the reason it is out of dialect."""
    if row.get("operation") != "search":
        return f"operation {row.get('operation')!r} is anchored; TRE offers a search only"
    if int(row.get("flags", 0)) != 0:
        return f"flags {row.get('flags')} have no TRE equivalent"
    if row.get("partial"):
        return "partial matching, which TRE does not have"
    if row.get("namedLists"):
        return "a named list, which TRE does not have"
    if row.get("codepointSlice") or row.get("pos") is not None:
        return "a slice; TRE searches a whole string"

    pattern = row["pattern"]

    # The one inline flag with a TRE equivalent comes off the front first; every other `(?...)`
    # construct in the pattern - a flag group, a lookaround, a call, a verb - puts the row out of
    # dialect wherever it sits.
    icase = bool(_LEADING_ICASE.match(pattern))
    pattern = _LEADING_ICASE.sub("", pattern, count=1)
    if _EXOTIC_GROUP.search(pattern):
        return "an inline flag, lookaround or other group TRE has no form for"

    whole = _WHOLE_PATTERN_BUDGET.match(pattern)
    if whole is None:
        return "no single whole-pattern fuzzy budget"
    body = whole.group("body")
    if not _CORE_BODY.match(body):
        return "the body leaves the POSIX ERE core"
    if "{" in body or "}" in body:
        return "a second budget or a bounded repeat inside the body"
    if _NOT_ERE_QUANTIFIER.search(body):
        return "a lazy or possessive quantifier, which POSIX ERE has neither of"

    # THE PARENTHESES MUST NEST, not merely BALANCE BY COUNT. `_WHOLE_PATTERN_BUDGET`'s greedy `.*`
    # turns `(a)(b){e<=1}` into the body `a)(b`, which has one of each and passed the count test -
    # and the budget there governs only `(b)`, so TRE would be asked a different question about a
    # different pattern. Found by S52c's blind review; the same shape as the `?i)(?:fo` hole above,
    # and the same lesson, which is that a count is not a parse.
    depth = 0
    for character in body:
        depth += (character == "(") - (character == ")")
        if depth < 0:
            return "the budget does not govern the whole pattern"
    if depth != 0:
        return "the budget does not govern the whole pattern"

    # `.`, `^` and `$` MEAN DIFFERENT THINGS TO THE TWO ENGINES ONCE A NEWLINE IS IN THE SUBJECT,
    # so a row carrying both is one where TRE would answer a DIFFERENT QUESTION and its agreement
    # would be worthless. Measured 2026-09-16, both engines, on the two shapes that differ:
    #
    #   `(?:a.c){e<=1}` over 'a\ncc'   upstream (0, 3) cost 1 - `.` cannot take the newline, so it
    #                                  spends a substitution; TRE (0, 3) cost 0. A silent FALSE
    #                                  CONFIRMATION, which is the worst outcome a second engine has.
    #   `(?:abc$){e<=0}` over 'abc\n'  upstream matches - `$` matches before a trailing newline;
    #                                  TRE does not. A false alarm, which is merely expensive.
    #
    # Gated on the SUBJECT as well as the pattern, because with no newline anywhere the two agree
    # and there is no reason to throw the row away. Found by S52c's blind review, which also
    # measured that no row of the three waves reaches it - so this is a gate that was wrong rather
    # than one that had answered wrongly.
    if "\n" in row["subject"] and any(character in body for character in ".^$"):
        return "`.`, `^` or `$` with a newline in the subject, where the two engines differ"

    budget = {}
    for term in whole.group("budget").split(","):
        parsed = _BUDGET_TERM.match(term.strip())
        if parsed is None:
            return f"budget term {term.strip()!r} is not a plain <= bound"
        budget[parsed.group("kind")] = int(parsed.group("limit"))
    if not budget:
        return "an empty budget"
    if icase:
        budget["icase"] = 1
    return body, json.dumps(budget)


def _ask_tre(jobs: list[dict]) -> list[dict]:
    """One WSL process for the whole batch."""
    if not jobs:
        return []
    here = Path(__file__).resolve()
    # C:\a\b -> /mnt/c/a/b, which is how WSL sees this repository.
    inside = "/mnt/" + here.drive[0].lower() + here.as_posix()[2:]
    done = subprocess.run(
        ["wsl", "-d", "Ubuntu", "--", WSL_PYTHON, inside, "--in-wsl"],
        input=json.dumps(jobs),
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )
    if done.returncode != 0:
        raise SystemExit(f"the WSL side failed ({done.returncode}):\n{done.stderr}")
    return json.loads(done.stdout)


def _in_wsl() -> int:
    """The half that runs inside WSL, and the only half that imports ``tre``."""
    import tre  # noqa: PLC0415 - unavailable on the Windows side by design

    answers = []
    for job in json.load(sys.stdin):
        budget = json.loads(job["budget"])
        icase = budget.pop("icase", 0)

        # AN UNNAMED KIND IS FORBIDDEN, NOT UNBOUNDED, and getting that backwards is what the first
        # version of this probe did: it defaulted the unnamed kinds to 255 and reported
        # `(?i)(?:fo){i<=2}` over 'F' as TRE matching where upstream did not. Upstream's own
        # documentation is flat about it (`upstream/README.rst:561`):
        #
        #     "If a certain type of error is specified, then any type not specified will **not** be
        #      permitted."
        #
        # and its example at :570 spells out that even `e` does not re-open them - "{i<=2,d<=2,e<=3}
        # permit at most 2 insertions, at most 2 deletions, at most 3 errors in total, but no
        # substitutions". Measured to match, 2026-09-16: `(?:abc){i<=1}` fullmatches 'abxc' and NOT
        # 'ab', and `(?:abc){e<=2,s<=1}` does not fullmatch 'ac' though one deletion is within its
        # total. TRE agrees with upstream on which edit is an insertion and which a deletion -
        # `maxdel=2` matches 'fo' against 'F' at cost 1 and `maxins=2` does not - so the sense needed
        # no translation and only the defaults were wrong.
        kinds = {"s": "maxsub", "i": "maxins", "d": "maxdel"}
        named = [kind for kind in kinds if kind in budget]
        limits = {
            argument: budget.get(kind, 0 if named else 255) for kind, argument in kinds.items()
        }
        fuzzyness = tre.Fuzzyness(maxerr=budget.get("e", sum(limits.values())), **limits)
        flags = tre.EXTENDED | (tre.ICASE if icase else 0)
        try:
            match = tre.compile(job["body"], flags).search(job["subject"], fuzzyness)
        except Exception as e:  # noqa: BLE001 - an engine that refuses the pattern is an answer
            answers.append({"number": job["number"], "error": f"{type(e).__name__}: {e}"})
            continue
        answers.append({
            "number": job["number"],
            "matched": match is not None,
            # Codepoint offsets, proven on a non-ASCII subject (OPERATIONS.md).
            "span": None if match is None else list(match.groups()[0]),
            "cost": None if match is None else match.cost,
        })
    print(json.dumps(answers))
    return 0


def _upstream(row: dict) -> tuple[bool, int | None]:
    """Whether upstream matched this row, and what it said the match cost."""
    outcome = row["outcome"]
    if outcome["kind"] != "match":
        return False, None
    counts = outcome.get("fuzzyCounts")
    return True, 0 if counts is None else sum(counts)


def main(argv: list[str]) -> int:
    if "--in-wsl" in argv:
        return _in_wsl()

    paths = [arg for arg in argv if not arg.startswith("--")]
    if "--self-test" in argv:
        return _self_test()
    if not paths:
        print(__doc__.strip().splitlines()[-3].strip(), file=sys.stderr)
        return 2

    rows: list[dict] = []
    for path in paths:
        for line in Path(path).read_text(encoding="ascii").splitlines():
            row = json.loads(line)
            if row.get("kind") != "header":
                rows.append(row)

    jobs, rejected = [], []
    for number, row in enumerate(rows):
        verdict = _dialect(row)
        if isinstance(verdict, str):
            rejected.append((number, row, verdict))
            continue
        body, budget = verdict
        jobs.append({"number": number, "body": body, "budget": budget, "subject": row["subject"]})

    answers = {answer["number"]: answer for answer in _ask_tre(jobs)}

    confirmed = different = errored = 0
    for job in jobs:
        row = rows[job["number"]]
        answer = answers[job["number"]]
        matched, cost = _upstream(row)
        if "error" in answer:
            errored += 1
            verdict = f"TRE REFUSED  {answer['error']}"
        elif answer["matched"] != matched:
            different += 1
            verdict = (
                f"DIFFERENT    upstream {'matched' if matched else 'did not match'}, "
                f"TRE {'matched' if answer['matched'] else 'did not'}"
            )
        elif matched and cost is not None and answer["cost"] > cost:
            # TRE reports the CHEAPEST match it can find, and upstream without BESTMATCH reports the
            # FIRST acceptable one - so upstream costing more than TRE is ordinary, and TRE costing
            # more than upstream is not: it would mean upstream found an edit script TRE's own
            # minimum says is impossible.
            different += 1
            verdict = f"DIFFERENT    TRE's cheapest cost {answer['cost']} exceeds upstream's {cost}"
        else:
            confirmed += 1
            verdict = (
                f"CONFIRMED    both {'matched' if matched else 'did not match'}"
                + ("" if not matched else f", TRE cost {answer['cost']} <= upstream {cost}")
            )
        print(f"  row {job['number']:>6} {verdict}")
        if not verdict.startswith("CONFIRMED"):
            print(f"         pattern {ascii(row['pattern'])} on {ascii(row['subject'])}")

    print()
    print(f"{len(rows)} rows: {len(jobs)} in the fuzzy core, {len(rejected)} out of dialect")
    print(f"  of the {len(jobs)} asked: {confirmed} CONFIRMED, {different} DIFFERENT, "
          f"{errored} refused by TRE")

    reasons: dict[str, int] = {}
    for _, _, reason in rejected:
        reasons[reason] = reasons.get(reason, 0) + 1
    print("  out of dialect, by reason:")
    for reason, count in sorted(reasons.items(), key=lambda item: -item[1]):
        print(f"    {count:>6}  {reason}")
    return 1 if different or errored else 0


def _self_test() -> int:
    """Proves the instrument answers, so a run with no comparable row is readable.

    A second engine that is never asked anything and a second engine that agrees print the same
    thing at the bottom of a report. These four cases say which this is.
    """
    cases = [
        # body, budget, subject, whether a match should exist, the cheapest cost if it does
        ("abc", {"e": 1}, "xxabdyy", True, 1),
        ("abc", {"e": 0}, "xxabdyy", False, None),
        ("abc", {"e": 2}, "xxaqdyy", True, 2),
        ("[ab]+c", {"d": 1}, "zzabbzz", True, 1),
    ]
    jobs = [
        {"number": number, "body": body, "budget": json.dumps(budget), "subject": subject}
        for number, (body, budget, subject, _, _) in enumerate(cases)
    ]
    answers = {answer["number"]: answer for answer in _ask_tre(jobs)}

    failures = []
    for number, (body, budget, subject, should_match, cheapest) in enumerate(cases):
        answer = answers[number]
        print(f"  {body!r} {budget} on {subject!r} -> {answer}")
        if "error" in answer:
            failures.append(f"{body!r} on {subject!r}: TRE refused it - {answer['error']}")
        elif answer["matched"] != should_match:
            failures.append(f"{body!r} on {subject!r}: matched={answer['matched']}, expected {should_match}")
        elif should_match and answer["cost"] != cheapest:
            failures.append(f"{body!r} on {subject!r}: cost {answer['cost']}, expected {cheapest}")

    for failure in failures:
        print("self-test: " + failure, file=sys.stderr)
    if failures:
        return 1
    print("self-test: TRE answers existence and cheapest cost on all four cases")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
