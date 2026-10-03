"""Check C7: the port against PCRE2 and Perl, row by row, through the answer key's survey tooling.

    python tools/matrix/c7.py CHUNK.jsonl OUT.jsonl

C1 judges a row against upstream only. C7 asks two independent engines, PCRE2 10.47 and Perl
5.42.3, through tools/matrix/survey.py's translations (copied from matrix/answer-key; its header
lists the changes), and records node, .NET, python `re` and Boost.Regex too where they can express
the row. It writes one line per row: the engines' normalised answers, the n/a reasons, and the flags dropped
as inert. run.py's judge turns that into C7's verdict with `verdict()` below, so a judging change
needs no engine rerun. Never reads stdin.

The verdict, per row:
  pass / fail       PCRE2 and Perl both answer and agree, and the port agrees / differs;
  engines-disagree  both answer and disagree: a question for the answer key, not a port failure;
  single-agree / single-disagree
                    only one of them can answer (a partial row is PCRE2 only; a refusal, error or
                    timeout counts as not answering);
  open              the port differs where the answer key has an OPEN question the owner has not
                    decided (OPEN-2, OPEN-3), so it is neither a pass nor a failure;
  n/a               neither engine can express the row, or the port did not answer it, or a flag
                    dropped as inert changed upstream's own answer.

RULES lists every translation and exclusion with its justification; summary.md prints it.
"""

from __future__ import annotations

import json
import os
import re
import shutil
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import survey  # noqa: E402

ENGINES = ("regex", "pcre2", "perl", "node", "dotnet", "re", "boost")
JUDGES = ("pcre2", "perl")

BESTMATCH, ENHANCEMATCH, FULLCASE, WORD, VERSION1, IGNORECASE = 4096, 32768, 16384, 2048, 256, 2
# pcre2pattern's own example proves the value: /(*COMMIT)abc/ over 'xyzabc' is (3,6) by default and
# No match with 0x10000, as with (*NO_START_OPT) (measured through survey_worker.py, 2026-09-30).
PCRE2_NO_START_OPTIMIZE = 0x10000
_FUZZY = re.compile(survey._REGEX_ONLY_SYNTAX[0][1])
_BOUNDARY = re.compile(r"(?<!\\)(?:\\\\)*\\[bB]")
_WORD_SEPARATORS = set("\r\x0b\x0c\x85  ")

RULES = [
    ("fuzzy constraint, named list, \\m \\M \\X \\p \\N, POSIX flag", "n/a",
     "no PCRE2 or Perl equivalent (survey.py _REGEX_ONLY_SYNTAX and _REGEX_ONLY_BITS)"),
    ("REVERSE flag or (?r)", "n/a for PCRE2 and Perl",
     "neither has right-to-left matching; .NET RightToLeft is recorded, not judged"),
    ("partial=True", "PCRE2 only (PARTIAL_SOFT), so at most single-engine",
     "Perl has no partial matching; PCRE2 declines a partial before a character is inspected "
     "(pcre2partial, answer key A4 group 1), a narrower contract, so it never makes a failure"),
    ("BESTMATCH / ENHANCEMATCH on a pattern with no fuzzy constraint", "flag dropped",
     "inert: both engines dispatch on is-fuzzy first (upstream _regex.c:18113 do_match_2, port "
     "Matcher.cs:15888 DoMatch2), and every exact match costs 0; upstream guard below"),
    ("FULLCASE when pattern and subject are ASCII", "flag dropped",
     "inert: full folding differs from simple folding only for the 104 codepoints whose fold is "
     "several characters, none ASCII (str.casefold over every codepoint, Unicode 16.0, "
     "measured 2026-09-30); upstream guard below"),
    ("WORD with no \\b or \\B and no \\r \\v \\f \\x85 \\u2028 \\u2029 in the subject", "flag dropped",
     "inert: WORD changes only \\b/\\B and the line separators (upstream README 'Default Unicode "
     "word boundary', 'Unicode line separators'); upstream guard below"),
    ("VERSION1 with no nested set or set operator, and IGNORECASE only on ASCII", "flag dropped",
     "inert: V1 changes set syntax, full case folding, inline-flag scope and split/sub's zero-width "
     "rule (upstream README 'Old vs new behaviour'); no row has a scoped inline flag; upstream guard below"),
    ("upstream guard", "n/a when it fires",
     "a row with a dropped flag is run by upstream both ways; if upstream's answer changes, the flag "
     "was not inert there and the row is n/a (upstream can only make a row n/a, never a failure)"),
    ("pos / endpos", "translated for PCRE2, Perl, re; n/a for node and .NET",
     "upstream: 'as if the string is endpos characters long', and pos is not slicing (^ does not match "
     "there, lookbehind sees before it). PCRE2: subject cut at endpos, start_offset=pos; Perl: cut "
     "subject, pos() set, scalar //g; .NET's beginning/length hides the text before it from lookbehind"),
    ("match / fullmatch", "PCRE2 ANCHORED / ANCHORED|ENDANCHORED; Perl \\G(?:...) / \\G(?:...)\\z",
     "a whole-pattern call ((?R), (?0)) would recurse into a wrapper: fullmatch n/a for Perl, "
     "match keeps 'first match from pos must start at pos' and is n/a with \\K"),
    ("finditer", "compared as the list of spans",
     "Python's rule (an empty match may be followed by a non-empty one at the same place) is what "
     "PCRE2's pcre2demo loop and Perl's //g do (answer key A2 op-finditer)"),
    ("backreference and name spellings, \\Z", "rewritten",
     "\\g<n> -> \\g{n}, \\g<name> -> \\k<name>, (?(name) -> (?(<name>) for Perl, \\Z -> \\z (survey.py)"),
    ("what is compared", "span, each group's value, and the last entry of each group's capture history",
     "PCRE2 and Perl report the last capture on the successful path, and .NET's Group contract makes "
     "that the last Captures entry (an unset group has none), so both the port's Value and its "
     "history must match the engines' group; the rest of the history and fuzzy counts have no "
     "PCRE2/Perl equivalent; a partial answer's groups are unsettled (answer key A9), so only its "
     "span is compared"),
    ("captures made inside a call (D51)", "judged as the engines answer",
     "owner ruling D51 (a): a return discards them, which is what PCRE2 and Perl do (answer key A3 "
     "capture + call), so a port difference is a real failure (inherited from upstream)"),
    ("verb inside a called group (OPEN-1)", "no special case",
     "PCRE2 confines the verb to the call and Perl does not, so where it matters they disagree and "
     "the row is engines-disagree; where they agree both options give that answer"),
    ("captures from a failed negative condition test (OPEN-2)", "open, when the port differs",
     "PCRE2 and Perl keep them, upstream drops them, the owner has not ruled: a row with a capture "
     "inside a (?(?! or (?(?<! test that the port answers differently is 'open', not a failure"),
    ("a name at different positions in a branch reset (OPEN-3)", "open, when the port differs",
     "PCRE2 refuses the pattern ('same name'); Perl numbers by position; the port keeps the owner's "
     "option 3. Detected by PCRE2's refusal on a (?| pattern"),
    ("PCRE2 start-of-match optimisations", "off (PCRE2_NO_START_OPTIMIZE)",
     "an optimisation must not change an answer, and pcre2pattern says it can with verbs and "
     "partial matching; over the 3,318 rows PCRE2 can ask with their flags as written, turning it off changed 3, all partial "
     "(single-engine) rows (measured 2026-09-30). Perl's optimisations cannot be turned off"),
    ("Boost.Regex", "recorded, not a judge",
     "a third backtracking Perl-syntax engine, asked on ASCII rows with no slice and no partial matching "
     "(no ICU, so non-ASCII text is n/a); it refuses variable-length lookbehind, which counts as not "
     "answering. It shows in a failure's 'port agrees with' list, beside node, .NET and re"),
    ("an engine refuses, errors or times out", "that engine does not answer",
     "counted in the n/a or single-engine reasons, never as a disagreement"),
    ("the port times out, hits its step cap, asserts or throws", "n/a",
     "unanswered (ASSERT and EXC are C3's failures); a port compile rejection IS an answer, and "
     "differs from any engine answer"),
]


# ---------------------------------------------------------------- inert flags
def _has_set_operation(pattern: str) -> bool:
    """A nested set or a set operator (VERSION1 syntax), found by scanning the sets."""
    k, depth = 0, 0
    while k < len(pattern):
        c = pattern[k]
        if c == "\\":
            k += 2
            continue
        if depth:
            if c == "[" and k + 1 < len(pattern):
                return True
            if pattern[k:k + 2] in ("&&", "||", "--", "~~"):
                return True
            if c == "]":
                depth = 0
        elif c == "[":
            depth = 1
            k += 1
            if k < len(pattern) and pattern[k] == "^":
                k += 1
            if k < len(pattern) and pattern[k] == "]":
                k += 1  # a leading ] is a literal
            continue
        k += 1
    return False


def inert_flags(row: dict) -> list[str]:
    """Upstream-only flags this row can drop without changing its meaning (RULES)."""
    bits, pattern, subject = int(row.get("flags") or 0), row["pattern"], row["subject"]
    ascii_only = (pattern + subject).isascii()
    fuzzy = bool(_FUZZY.search(pattern))
    out = []
    if bits & BESTMATCH and not fuzzy:
        out.append("BESTMATCH")
    if bits & ENHANCEMATCH and not fuzzy:
        out.append("ENHANCEMATCH")
    if bits & FULLCASE and ascii_only:
        out.append("FULLCASE")
    if bits & WORD and not _BOUNDARY.search(pattern) and not _WORD_SEPARATORS & set(subject):
        out.append("WORD")
    if bits & VERSION1 and not _has_set_operation(pattern) and (ascii_only or not bits & IGNORECASE):
        out.append("VERSION1")
    return out


_BIT = {"BESTMATCH": BESTMATCH, "ENHANCEMATCH": ENHANCEMATCH, "FULLCASE": FULLCASE, "WORD": WORD,
        "VERSION1": VERSION1}


# ---------------------------------------------------------------- running the engines
def _question(row: dict, flags: int) -> dict:
    q = {"pattern": row["pattern"], "flags": flags, "subject": row["subject"],
         "operation": row["operation"], "partial": bool(row.get("partial")),
         "namedLists": row.get("namedLists") or {}}
    for k in ("pos", "endpos"):
        if row.get(k) is not None:
            q[k] = row[k]
    return q


def _compact(res: dict) -> dict:
    return {k: res[k] for k in ("status", "span", "groups", "matches", "captures", "error", "reason")
            if k in res and res[k] is not None}


def ask(rows: list[dict], work: Path) -> list[dict]:
    """Every engine's answer for each row, with the inert flags dropped for the other engines."""
    work.mkdir(parents=True, exist_ok=True)
    os.environ["SURVEY_PCRE2_COPTS"] = hex(PCRE2_NO_START_OPTIMIZE)
    records, jobs = [], {e: [] for e in ENGINES}
    for k, row in enumerate(rows):
        dropped = inert_flags(row)
        flags = int(row.get("flags") or 0)
        for name in dropped:
            flags &= ~_BIT[name]
        rec = {"id": row["id"], "dropped": dropped, "results": {}}
        records.append(rec)
        original, shadow = _question(row, int(row.get("flags") or 0)), _question(row, flags)
        try:
            info = survey.analyse(shadow)
            survey.analyse(original)
        except Exception as e:  # noqa: BLE001 - upstream refuses the pattern: nothing to translate
            why = f"survey.analyse could not compile it in upstream: {e}"[:200]
            for engine in ENGINES:
                rec["results"][engine] = {"status": "n/a", "reason": why}
            continue
        jobs["regex"].append(dict(survey.translate(original, survey.analyse(original), "regex"), i=2 * k))
        if dropped:
            jobs["regex"].append(dict(survey.translate(shadow, info, "regex"), i=2 * k + 1))
        for engine in ENGINES[1:]:
            t = survey.translate(shadow, info, engine)
            if isinstance(t, str):
                rec["results"][engine] = {"status": "n/a", "reason": t}
            else:
                jobs[engine].append(dict(t, i=2 * k))
    for engine in ENGINES:
        answers = survey.run_engine(engine, jobs[engine], work, 3.0, 1500)
        for i, res in answers.items():
            k, variant = divmod(i, 2)
            res = survey.normalise(res, rows[k]["subject"])
            name = engine if variant == 0 else "regex-dropped"
            records[k]["results"][name] = _compact(res)
    shutil.rmtree(work, ignore_errors=True)
    return records


# ---------------------------------------------------------------- judging
_GROUP = re.compile(r"\((\d+),(\d+)\)|\(-\)")


def port_key(answer: str | None):
    """port-runner.cs's answer as survey.key's tuple, or None when the port did not answer."""
    if answer is None:
        return None
    if answer == "None":
        return ("none",)
    if answer.startswith("REJECT"):
        return ("reject",)
    if answer.startswith("["):
        spans = []
        for part in (p for p in answer[1:-1].split(" ; ") if p):
            m = re.match(r"\((\d+),(\d+)\)(P?)", part)
            spans.append((int(m.group(1)), int(m.group(2)), bool(m.group(3))))
        return ("matches", tuple(spans))
    m = re.match(r"\((\d+),(\d+)\)(P?) ", answer)
    if not m:
        return None  # ERR, ASSERT, EXC: unanswered
    span = (int(m.group(1)), int(m.group(2)))
    if m.group(3):
        return ("partial", span)
    g = answer.split(" g", 1)[1].split(" c", 1)[0]
    groups = tuple(None if x.group(0) == "(-)" else (int(x.group(1)), int(x.group(2))) for x in _GROUP.finditer(g))
    return ("match", span, groups)


def history_key(answer: str | None):
    """The port's answer with each group read from its capture history: the last capture, or unset
    when the history is empty. .NET's Group contract (Success means Captures is non-empty, Value is
    the last capture) makes this the same as the group's value in a consistent answer; PCRE2 and
    Perl report the last capture on the successful path, so an unset group there means none."""
    k = port_key(answer)
    if not k or k[0] != "match":
        return k
    hist = answer.split(" c", 1)[1]
    last = []
    for part in re.findall(r"\[([^\]]*)\]", hist):
        caps = re.findall(r"\((\d+),(\d+)\)", part)
        last.append((int(caps[-1][0]), int(caps[-1][1])) if caps else None)
    return (k[0], k[1], tuple(last))


def _engine_key(res: dict | None):
    if not res or res.get("status") not in survey.ANSWERS:
        return None
    return survey.key(dict(res), "")


def _why_not(res: dict | None) -> str:
    if not res:
        return "not asked"
    if res.get("status") == "n/a":
        return res.get("reason", "n/a")
    return f"{res.get('status')}: {res.get('error', '')}"[:120]


def _difference(a, b) -> str:
    if a[0] != b[0]:
        if "none" in (a[0], b[0]):
            return "existence differs"
        return f"{a[0]} vs {b[0]}"
    if a[0] == "matches":
        return "finditer spans differ"
    if a[1] != b[1]:
        return "span differs"
    return "groups differ"


_CAPTURE_OPEN = re.compile(r"\((?!\?)|\(\?P?<(?![=!])\w+>|\(\?'\w+'")


def negative_test_captures(pattern: str) -> bool:
    """A capture inside a negative lookaround used as a condition's test (OPEN-2's rows)."""
    for m in re.finditer(r"\(\?\((\?<?!)", pattern):
        start, depth, k, in_set = m.start(1) - 1, 0, m.start(1) - 1, False
        while k < len(pattern):
            c = pattern[k]
            if c == "\\":
                k += 2
                continue
            if in_set:
                in_set = c != "]"
            elif c == "[":
                in_set = True
            elif c == "(":
                depth += 1
            elif c == ")":
                depth -= 1
                if depth == 0:
                    break
            k += 1
        if _CAPTURE_OPEN.search(pattern[start + 1:k]):
            return True
    return False


def verdict(row: dict, base: str | None, rec: dict | None) -> tuple:
    """(status, kind, port answer, engines' answer) for check C7."""
    if rec is None:
        return ("n/a", "not surveyed", base, None)
    res = rec["results"]
    mine = port_key(base)
    if mine is None:
        return ("n/a", "port unanswered", base, None)
    if rec.get("dropped"):
        up, down = res.get("regex"), res.get("regex-dropped")
        if not up or not down or _engine_key(up) is None or _engine_key(down) is None:
            return ("n/a", "upstream guard unanswered", base, None)
        if (_engine_key(up), up.get("captures")) != (_engine_key(down), down.get("captures")):
            return ("n/a", "a dropped flag changed upstream's answer: " + "+".join(rec["dropped"]), base, None)
    keys = {e: _engine_key(res.get(e)) for e in JUDGES}
    answered = [e for e in JUDGES if keys[e] is not None]
    others = ",".join(e for e in ("node", "dotnet", "re", "boost") if _engine_key(res.get(e)) is not None
                      and _engine_key(res.get(e)) == mine)
    tail = f" (port agrees with: {others})" if others else ""
    shown = {e: keys[e] for e in answered}
    if not answered:
        return ("n/a", "; ".join(f"{e}: {_why_not(res.get(e))}" for e in JUDGES), base, None)
    open2 = negative_test_captures(row["pattern"])
    if len(answered) == 1:
        e = answered[0]
        other = JUDGES[1 - JUDGES.index(e)]
        if keys[e] == mine and history_key(base) == mine:
            return ("single-agree", f"{e} only", base, str(shown))
        refused = _why_not(res.get(other))
        if other == "pcre2" and "(?|" in row["pattern"] and "same name" in refused:
            return ("open", "OPEN-3 (PCRE2 refuses a repeated name in a branch reset)", base, str(shown))
        if open2:
            return ("open", f"OPEN-2 ({e} only)", base, str(shown))
        diff = "capture history differs" if keys[e] == mine else _difference(mine, keys[e])
        return ("single-disagree", f"{e} only, {diff}; {other}: {refused}", base, str(shown))
    if keys["pcre2"] != keys["perl"]:
        side = "port = pcre2" if mine == keys["pcre2"] else "port = perl" if mine == keys["perl"] else "port = neither"
        return ("engines-disagree", f"{_difference(keys['pcre2'], keys['perl'])}; {side}", base, str(shown))
    if keys["pcre2"] == mine:
        if history_key(base) == mine:
            return ("pass", "", base, str(keys["pcre2"]))
        kind = "capture history differs (a group's last capture, or a history where the engines' group is unset)"
        if open2:
            return ("open", f"OPEN-2 ({kind})", base, str(keys["pcre2"]))
        return ("fail", kind + tail, base, str(keys["pcre2"]))
    kind = _difference(mine, keys["pcre2"])
    if open2:
        return ("open", f"OPEN-2 ({kind})", base, str(keys["pcre2"]))
    return ("fail", kind + tail, base, str(keys["pcre2"]))


def main(argv: list[str]) -> int:
    src, out = Path(argv[0]), Path(argv[1])
    rows = [json.loads(line) for line in open(src, encoding="utf-8") if line.strip()]
    records = ask(rows, out.with_suffix(".work"))
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for rec in records:
            f.write(json.dumps({"id": rec["id"], "c7": rec}, ensure_ascii=True) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
