"""The Python `regex` side of the v1.0 performance gate.

One pyperf benchmark per row of bench/FuzzyRegex.Benchmarks/WorkloadBenchmarks.cs and
ManyInputsBenchmarks.cs that Python can express, named exactly as the BenchmarkDotNet method, so the
two result tables join on the name. Each row uses the same pattern, the same subject and the same
operation as the C# row, and consumes the result the same way (a C# Count is counted here, a C#
Matches is materialised here, a lazy "first two" stops after two).

Before any row is timed, its answer is computed once here and compared with the C# answer, which
the benchmark project prints with `workload-answers` and `usage-answers`: the row's returned number
plus the full answer behind it (every span, the substituted text, which inputs matched; see
bench/FuzzyRegex.Benchmarks/AnswerText.cs). A row whose answer differs is reported as "answer
differs" and still timed: a faster number for different work is not evidence, and hiding the row
would hide that.

Usage (from the repo root, quiet machine, see .claude/skills/benchmark/SKILL.md):

    python -m pip install regex pyperf
    # 1. Write the C# inputs and answers once (builds Release):
    python bench/baselines/measure_python.py --prepare .scratch/gate-inputs
    # 2. Answer check only, no timing:
    python bench/baselines/measure_python.py --inputs .scratch/gate-inputs --check-only
    # 3. The baseline: answer check, then every row timed by pyperf:
    python bench/baselines/measure_python.py --inputs .scratch/gate-inputs -o python-regex.json
    python -m pyperf check python-regex.json

Any pyperf option works (--rigorous, --fast, --debug-single-value, ...). --rows REGEX limits the run
to rows whose name matches. The JSON metadata records the regex, Python and pyperf versions, the git
SHA, and per row its flags, version, operation and answer-check result.

Version rule. This port's default is Version 1, under which IgnoreCase means full case folding, so
the rows are timed under regex.V1. The exception is ledger entry 28: on full-folded fuzzy patterns
upstream rejects matches the port (correctly) finds, under V1 and under V0 | FULLCASE alike
(measured 2026-10-11: the three phrases match 2038, 1532 and 1468 records there, against the
port's 2038, 1587 and 1482). So the FuzzyPhrase rows run under V0 | IGNORECASE, simple folding,
which gives the port's answers on this all-ASCII data. That is slightly less folding work than the
port does, so it errs in Python's favour. Each row's flags are printed and in the metadata.
"""

import argparse
import dataclasses
import hashlib
import json
import os
import platform
import re
import site
import subprocess
import sys
from typing import Callable

# pyperf's workers get only a whitelist of environment variables, so a per-user install of pyperf
# and regex (%APPDATA%\Python\Python314\site-packages, this machine) is invisible to them and every
# worker dies with "No module named 'pyperf'". S58's fix (tools/probes/s58-pyperf-floor.py, measured
# 2026-09-19, seen again here 2026-10-11): put the user site on PYTHONPATH and name it on
# --inherit-environ. Harmless where the packages are installed system-wide.
_USER_SITE = site.getusersitepackages()
if _USER_SITE and os.path.isdir(_USER_SITE):
    _existing = os.environ.get("PYTHONPATH", "")
    if _USER_SITE not in _existing.split(os.pathsep):
        os.environ["PYTHONPATH"] = _USER_SITE if not _existing else _USER_SITE + os.pathsep + _existing
    if not any(arg.startswith("--inherit-environ") for arg in sys.argv[1:]):
        sys.argv.append("--inherit-environ=PYTHONPATH")

import pyperf  # noqa: E402 - after the PYTHONPATH fix above
import regex  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BENCH_PROJECT = os.path.join(REPO, "bench", "FuzzyRegex.Benchmarks")

# ---- Corpus.cs, rebuilt byte for byte --------------------------------------------------------

SENTENCE = "the quick brown fox jumps over the lazy dog "
MEGABYTE = 1024 * 1024


def pad(tail: str, size: int = MEGABYTE) -> str:
    """Corpus.Pad: whole filler sentences until at least `size` characters, then the tail."""
    return SENTENCE * -(-size // len(SENTENCE)) + tail


def large_pattern() -> str:
    """Corpus.BuildLargePattern."""
    words = "|".join(f"word{i:03d}" for i in range(300))
    return "(?:" + words + r")[0-9]{2,4}(?:[A-Za-z_]\w*)?\b"


CORPUS = {
    "Long": pad("a needle in a haystack."),
    "LongPartial": pad("a needle in a hay"),
    "LongNoMatch": pad("a pin in a haystack."),
    "Short": SENTENCE + "and finds a needle.",
    "Fuzzy": SENTENCE + "and finds a haystakc.",
    "Dense": pad("a needle in a haystack.", 100 * 1024),
    # WorkloadBenchmarks' own subjects.
    "SixteenAsCb": "a" * 16 + "cb",
    "AbRunThenD": "a" + "ab" * 200 + "d",
    "TwentyAs": "a" * 20 + "bc",
    "TwentyFourAs": "a" * 24 + "bc",
    "TwoHundredAs": "a" * 200 + "bc",
    "WordsThenBang": "aaaa " * 200 + "!",
}

# UsageCorpus.Phrases.
PHRASES = ["amber lantern works", "copper field studio", "violet stone archive"]

# ---- The answer text, as AnswerText.cs writes it ----------------------------------------------


def span(m) -> str:
    return f"{m.start()},{m.end()}" if m else "-"


def fuzzy_span(m) -> str:
    return f"{span(m)},{m.fuzzy_counts[0]},{m.fuzzy_counts[1]},{m.fuzzy_counts[2]}" if m else "-"


def spans(matches) -> str:
    return "\n".join(span(m) for m in matches)


def bitmap(hits) -> str:
    return "".join("1" if h else "0" for h in hits)


def digest(text: str) -> str:
    """AnswerText.Digest: short single-line texts as they are, else 16 hex digits of SHA-256."""
    if len(text) <= 80 and "\n" not in text:
        return text
    return "sha256:" + hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


# ---- The rows ---------------------------------------------------------------------------------

V1 = regex.V1
V0 = regex.V0
I, F, R, E = regex.IGNORECASE, regex.FULLCASE, regex.REVERSE, regex.ENHANCEMATCH


def flags_text(flags: int) -> str:
    names = [("V1", V1), ("V0", V0), ("I", I), ("F", F), ("R", R), ("E", E)]
    return "|".join(n for n, f in names if flags & f) or "0"


@dataclasses.dataclass
class Row:
    """One gate row. `timed` is what pyperf times; `answer` is the C# return value as text;
    `detail` is the full answer text (None where the row returns a bare yes or no)."""

    name: str
    suite: str  # "workload" or "usage": which C# answers command prints it
    pattern: str
    flags: int
    operation: str  # the Python operation, as written in the metadata
    timed: Callable[[], object]
    answer: Callable[[], str]
    detail: Callable[[], str] | None


# Rows Python cannot express. Excluded from the gate; listed so the published table can say why.
EXCLUDED = {
    "EnumerateMatchesSpanToEndDense": (
        "Span overload: the C# row walks a ReadOnlySpan<char> into ValueMatch structs, an API "
        "Python has no counterpart for; its str walk is EnumerateMatchesToEndDense."
    ),
    "EmptyIterationRecord": (
        r"Not timeable: upstream regex is exponential on ^(?:(?=(a))|a)*\1?$ (measured 2026-10-11 on "
        "2026.9.10: 21 ms at 16 a's, 83 ms at 18, four times slower per two more), so over the row's "
        "200 a's it gives neither a time nor an answer. The port answers in milliseconds (D17), so "
        "the row is a port win by construction rather than a comparison."
    ),
}


def compile_(pattern, flags, **kwargs):
    return regex.compile(pattern, flags, **kwargs)


def workload_rows() -> list[Row]:
    rows: list[Row] = []

    def add(name, pattern, flags, operation, timed, answer, detail=None, **kwargs):
        rows.append(Row(name, "workload", pattern, flags, operation, timed, answer, detail))

    long_, dense = CORPUS["Long"], CORPUS["Dense"]

    def start(m):
        # C#'s failed Match has Index 0.
        return m.start() if m else 0

    # LiteralMatch: _literal.Match(Long).Index
    p = compile_("needle", V1)
    add("LiteralMatch", p.pattern, V1, "search().start()",
        lambda p=p: start(p.search(long_)), lambda p=p: str(start(p.search(long_))),
        lambda p=p: span(p.search(long_)))

    # ClassScan: _classHeavy.Count(Long). Python has no count; findall is its cheapest.
    p = compile_("[a-z]{3}[^a-z]", V1)
    add("ClassScan", p.pattern, V1, "len(findall())",
        lambda p=p: len(p.findall(long_)), lambda p=p: str(len(p.findall(long_))),
        lambda p=p: spans(p.finditer(long_)))

    # CaseFoldedScan: IgnoreCase | FullCase, counted.
    fl = V1 | I | F
    p = compile_("QUICK", fl)
    add("CaseFoldedScan", p.pattern, fl, "len(findall())",
        lambda p=p: len(p.findall(long_)), lambda p=p: str(len(p.findall(long_))),
        lambda p=p: spans(p.finditer(long_)))

    # WholeWordList: _wholeWords.Count(Long), a whole-word search for ten words (C6).
    p = compile_(r"\b\L<words>\b", V1, words=["amber", "copper", "violet", "harbour", "meadow",
                                              "lantern", "granite", "jump", "fox", "needle"])
    add("WholeWordList", p.pattern, V1, "len(findall())",
        lambda p=p: len(p.findall(long_)), lambda p=p: str(len(p.findall(long_))),
        lambda p=p: spans(p.finditer(long_)))

    # WholeWordListIgnoreCase: the same list ignoring case (C6 option B).
    p = compile_(r"(?i)\b\L<words>\b", V1, words=["amber", "copper", "violet", "harbour", "meadow",
                                                  "lantern", "granite", "jump", "fox", "needle"])
    add("WholeWordListIgnoreCase", p.pattern, V1, "len(findall())",
        lambda p=p: len(p.findall(long_)), lambda p=p: str(len(p.findall(long_))),
        lambda p=p: spans(p.finditer(long_)))

    # WholeWordListIgnoreCaseKs: the same with kestrel and thistle for copper and violet (C6).
    p = compile_(r"(?i)\b\L<words>\b", V1, words=["amber", "kestrel", "thistle", "harbour", "meadow",
                                                  "lantern", "granite", "jump", "fox", "needle"])
    add("WholeWordListIgnoreCaseKs", p.pattern, V1, "len(findall())",
        lambda p=p: len(p.findall(long_)), lambda p=p: str(len(p.findall(long_))),
        lambda p=p: spans(p.finditer(long_)))

    # ReverseFailedScan: RightToLeft IsMatch.
    fl = V1 | R
    p = compile_("zebra", fl)
    add("ReverseFailedScan", p.pattern, fl, "search() is not None",
        lambda p=p: p.search(long_) is not None, lambda p=p: str(p.search(long_) is not None))

    # PartialMatch: Match(LongPartial, partial: true).Index
    lp = CORPUS["LongPartial"]
    p = compile_(r"a needle in a haystack\.", V1)
    add("PartialMatch", p.pattern, V1, "search(partial=True).start()",
        lambda p=p: start(p.search(lp, partial=True)), lambda p=p: str(start(p.search(lp, partial=True))),
        lambda p=p: span(p.search(lp, partial=True)))

    words = compile_(r"\w+", V1)
    w = words
    # MatchesToEnd / MatchesToEndDense: an eager MatchCollection, materialised, counted.
    for name, subject in (("MatchesToEnd", long_), ("MatchesToEndDense", dense)):
        add(name, w.pattern, V1, "len(list(finditer()))",
            lambda s=subject: len(list(w.finditer(s))), lambda s=subject: str(len(list(w.finditer(s)))),
            lambda s=subject: spans(w.finditer(s)))

    # EnumerateMatchesToEndDense: the lazy walk drained, counted.
    add("EnumerateMatchesToEndDense", w.pattern, V1, "sum(1 for _ in finditer())",
        lambda: sum(1 for _ in w.finditer(dense)), lambda: str(sum(1 for _ in w.finditer(dense))),
        lambda: spans(w.finditer(dense)))

    # MatchesFirstTwo: Matches(Long)[1].Index - materialises the whole collection first.
    add("MatchesFirstTwo", w.pattern, V1, "list(finditer())[1].start()",
        lambda: list(w.finditer(long_))[1].start(), lambda: str(list(w.finditer(long_))[1].start()),
        lambda: spans(list(w.finditer(long_))[:2]))

    # EnumerateMatchesFirstTwo: EnumerateMatches(Long).Take(2).Last().Index - stops after two.
    def first_two():
        it = w.finditer(long_)
        next(it)
        return next(it).start()

    def first_two_spans():
        it = w.finditer(long_)
        return spans([next(it), next(it)])

    add("EnumerateMatchesFirstTwo", w.pattern, V1, "two next(finditer()), .start()",
        first_two, lambda: str(first_two()), first_two_spans)

    # ReplaceTemplate: _pairs.Replace(Long, @"\2 \1").Length
    p = compile_(r"(\w+) (\w+)", V1)
    add("ReplaceTemplate", p.pattern, V1, r"len(sub(r'\2 \1'))",
        lambda p=p: len(p.sub(r"\2 \1", long_)), lambda p=p: str(len(p.sub(r"\2 \1", long_))),
        lambda p=p: p.sub(r"\2 \1", long_))

    # SplitLong: _words.Split(Long).Length
    add("SplitLong", w.pattern, V1, "len(split())",
        lambda: len(w.split(long_)), lambda: str(len(w.split(long_))),
        lambda: "\x01".join("\x02" if piece is None else piece for piece in w.split(long_)))

    # The fuzzy rows: Match(...).Index, and the ranking modes' FuzzyCounts.Total.
    one = compile_("(?:needle){e<=1}", V1)
    for name, key in (("FuzzyShort", "Short"), ("FuzzyLong", "Long")):
        s = CORPUS[key]
        add(name, one.pattern, V1, "search().start()",
            lambda s=s: start(one.search(s)), lambda s=s: str(start(one.search(s))),
            lambda s=s: fuzzy_span(one.search(s)))

    nomatch = CORPUS["LongNoMatch"]
    add("FuzzyNoMatchLong", one.pattern, V1, "search() is not None",
        lambda: one.search(nomatch) is not None, lambda: str(one.search(nomatch) is not None))

    fz = CORPUS["Fuzzy"]
    p = compile_("(?:haystack){e<=3}", V1)
    add("FuzzyBudgetThree", p.pattern, V1, "search().start()",
        lambda p=p: start(p.search(fz)), lambda p=p: str(start(p.search(fz))),
        lambda p=p: fuzzy_span(p.search(fz)))

    for name, pattern in (("EnhanceMatch", "(?e)(?:haystack){e<=3}"), ("BestMatch", "(?b)(?:haystack){e<=3}")):
        p = compile_(pattern, V1)
        add(name, pattern, V1, "sum(search().fuzzy_counts)",
            lambda p=p: sum(p.search(fz).fuzzy_counts), lambda p=p: str(sum(p.search(fz).fuzzy_counts)),
            lambda p=p: fuzzy_span(p.search(fz)))

    # The pathological group: IsMatch, except ConditionalBesideBranch, which is IsMatchAtStart.
    for name, pattern, key, op in (
        ("PathologicalSameBranches", "(?:a|a)+c", "TwentyAs", "search"),
        ("PathologicalPrefixBranches", "(a|aa)+c", "TwentyFourAs", "search"),
        ("PathologicalNestedRepeat", "(a+)+c", "TwoHundredAs", "search"),
        ("PathologicalWordsToEnd", r"(\w+\s?)+$", "WordsThenBang", "search"),
        ("TestedGroupRepeat", r"(a)(?:\1|b)*c", "AbRunThenD", "search"),
        ("ConditionalBesideBranch", "(?:(?(?=a)a|a)|a)*b", "SixteenAsCb", "match"),
        ("ConditionalPerToken", r"(?:(?(?=\w)\w+|\s))*$", "WordsThenBang", "search"),
        ("ConditionalPerTokenNoMemo", r"(\w)(?:(?(?=\w)\w+|\s))*\1$", "WordsThenBang", "search"),
    ):
        p = compile_(pattern, V1)
        f = getattr(p, op)
        s = CORPUS[key]
        detail = (lambda f=f, s=s: span(f(s))) if name == "ConditionalPerToken" else None
        add(name, pattern, V1, f"{op}() is not None",
            lambda f=f, s=s: f(s) is not None, lambda f=f, s=s: str(f(s) is not None), detail)

    # CompileLargePattern: new FuzzyRegex(LargePattern).GroupNumbers.Count. cache_pattern=False so
    # every call compiles, as the C# constructor does; GroupNumbers counts group 0 too.
    lp_src = large_pattern()
    add("CompileLargePattern", lp_src, V1, "compile(cache_pattern=False).groups + 1",
        lambda: regex.compile(lp_src, V1, cache_pattern=False).groups + 1,
        lambda: str(regex.compile(lp_src, V1, cache_pattern=False).groups + 1))

    return rows


def read_lines(inputs: str, name: str) -> list[str]:
    # UTF-8, LF, one input per line; never strip: most accented lines end in a space.
    with open(os.path.join(inputs, name), encoding="utf-8", newline="") as f:
        return f.read().split("\n")[:-1]


# Ledger entry 28: upstream drops full-folded fuzzy matches the port finds, under V1 and V0|F alike;
# V0|I (simple folding) gives the port's answer. See the module docstring.
PHRASE_FLAGS = V0 | I


def usage_rows(inputs: str) -> list[Row]:
    records = read_lines(inputs, "records.txt")
    emails = read_lines(inputs, "emails.txt")
    log_lines = read_lines(inputs, "log-lines.txt")
    accented = read_lines(inputs, "accented.txt")
    rows: list[Row] = []

    def add(name, pattern, flags, operation, timed, detail):
        rows.append(Row(name, "usage", pattern, flags, operation, timed, lambda: str(timed()), detail))

    def count_hits(p, lines):
        def run():
            search = p.search
            hits = 0
            for line in lines:
                if search(line):
                    hits += 1
            return hits

        return run

    def length_sum(p, lines):
        def run():
            search = p.search
            length = 0
            for line in lines:
                m = search(line)
                if m:
                    length += m.end() - m.start()
            return length

        return run

    fl = PHRASE_FLAGS
    one = compile_(f"(?:{PHRASES[0]}){{e<=2}}", fl)
    add("FuzzyPhraseOneIsMatch", one.pattern, fl, "search() per record, counted",
        count_hits(one, records), lambda: bitmap(one.search(r) for r in records))
    add("FuzzyPhraseOneMatch", one.pattern, fl, "search() per record, lengths summed",
        length_sum(one, records), lambda: "\n".join(span(one.search(r)) for r in records))

    fle = fl | E
    enh = compile_(one.pattern, fle)
    add("FuzzyPhraseOneEnhanced", enh.pattern, fle, "search() per record, lengths summed",
        length_sum(enh, records), lambda: "\n".join(span(enh.search(r)) for r in records))

    alt = compile_(f"(?:{'|'.join(PHRASES)}){{e<=2}}", fl)
    add("FuzzyPhraseThreeAlternation", alt.pattern, fl, "search() per record, counted",
        count_hits(alt, records), lambda: bitmap(alt.search(r) for r in records))

    named = compile_(r"(?:\L<phrases>){e<=2}", fl, phrases=PHRASES)
    add("FuzzyPhraseThreeNamedList", named.pattern, fl, "search() per record, counted",
        count_hits(named, records), lambda: bitmap(named.search(r) for r in records))

    separate = [compile_(f"(?:{e}){{e<=2}}", fl) for e in PHRASES]
    passes = [count_hits(p, records) for p in separate]
    add("FuzzyPhraseThreeSeparatePasses", " ; ".join(p.pattern for p in separate), fl,
        "search() per record per phrase, counted", lambda: sum(run() for run in passes),
        lambda: "\n".join(bitmap(p.search(r) for r in records) for p in separate))

    email = compile_(r"^[\w.+'-]+@[\w-]+(?:\.[\w-]+)+$", V1)
    add("ValidateEmails", email.pattern, V1, "search() per input, counted",
        count_hits(email, emails), lambda: bitmap(email.search(e) for e in emails))

    log = compile_(r"^(?<date>\d{4}-\d\d-\d\d) (?<level>[A-Z]+) (?<msg>.*)$", V1)

    def parse_log_lines():
        search = log.search
        length = 0
        for line in log_lines:
            m = search(line)
            if m:
                length += len(m.group("level"))
        return length

    add("ParseLogLines", log.pattern, V1, "search() per line, len(group('level')) summed",
        parse_log_lines, lambda: "\n".join(m.group("level") if (m := log.search(l)) else "-" for l in log_lines))

    digits = compile_(r"\d+", V1)

    def redact_digits():
        sub = digits.sub
        length = 0
        for record in records:
            length += len(sub("#", record))
        return length

    add("RedactDigits", digits.pattern, V1, "len(sub('#')) per record, summed",
        redact_digits, lambda: "\n".join(digits.sub("#", r) for r in records))

    fl = V1 | I | F
    acc = compile_("straße|café", fl)
    add("CaseFoldAccented", acc.pattern, fl, "search() per line, counted",
        count_hits(acc, accented), lambda: bitmap(acc.search(l) for l in accented))

    blind = compile_("(?i)(?:amber|copper|violet)", V1)
    add("CaseBlindAlternation", blind.pattern, V1, "len(findall()) per record, summed",
        lambda: sum(len(blind.findall(r)) for r in records),
        lambda: "\n".join(spans(blind.finditer(r)) for r in records))

    blind_ks = compile_("(?i)(?:amber|kestrel|thistle)", V1)
    add("CaseBlindAlternationKs", blind_ks.pattern, V1, "len(findall()) per record, summed",
        lambda: sum(len(blind_ks.findall(r)) for r in records),
        lambda: "\n".join(spans(blind_ks.finditer(r)) for r in records))

    whole = compile_(r"\b\L<words>\b", V1, words=["ORBIT", "MEADOW", "GRANITE", "FALCON", "BEACON",
                                                  "THISTLE", "AMBER", "HARBOR", "PARCELS", "TIMBERLAND"])
    add("WholeWordRecords", whole.pattern, V1, "len(findall()) per record, summed",
        lambda: sum(len(whole.findall(r)) for r in records),
        lambda: "\n".join(spans(whole.finditer(r)) for r in records))

    return rows


# ---- The answer check -------------------------------------------------------------------------


def read_answers(path: str) -> dict[str, tuple[str, str | None]]:
    answers = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if parts:
                answers[parts[0]] = (parts[1], parts[2] if len(parts) > 2 else None)
    return answers


def check(rows: list[Row], inputs: str) -> dict[str, str]:
    """Each row's verdict: "same", "answer differs: ...", or "no C# answer"."""
    csharp = {}
    for suite, file in (("workload", "workload-answers.txt"), ("usage", "usage-answers.txt")):
        csharp[suite] = read_answers(os.path.join(inputs, file))
    status = {}
    for row in rows:
        theirs = csharp[row.suite].get(row.name)
        if theirs is None:
            status[row.name] = "no C# answer"
            continue
        ours = (row.answer(), digest(row.detail()) if row.detail else None)
        if ours == theirs:
            status[row.name] = "same"
        else:
            status[row.name] = f"answer differs: C# {theirs[0]} {theirs[1]}; Python {ours[0]} {ours[1]}"
    return status


# ---- Driver -----------------------------------------------------------------------------------


def prepare(directory: str) -> None:
    """Writes the usage corpus and both C# answer files into `directory`."""
    os.makedirs(directory, exist_ok=True)
    base = ["dotnet", "run", "-c", "Release", "--project", BENCH_PROJECT, "--"]
    subprocess.run(base + ["usage-corpus", directory], check=True)
    for command, file in (("workload-answers", "workload-answers.txt"), ("usage-answers", "usage-answers.txt")):
        with open(os.path.join(directory, file), "w", encoding="utf-8", newline="\n") as out:
            subprocess.run(base[:6] + ["--no-build", "--"] + [command], check=True, stdout=out)


def git_sha() -> str:
    try:
        sha = subprocess.run(["git", "-C", REPO, "rev-parse", "HEAD"], capture_output=True, text=True,
                             check=True).stdout.strip()
        dirty = subprocess.run(["git", "-C", REPO, "diff", "--quiet", "HEAD"]).returncode != 0
        return sha + ("-dirty" if dirty else "")
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def add_cmdline_args(cmd: list[str], args) -> None:
    """Forwards this script's own options to pyperf's worker processes."""
    cmd.extend(["--inputs", args.inputs, "--git-sha", args.git_sha, "--answer-status", args.answer_status])
    if args.rows:
        cmd.extend(["--rows", args.rows])


def main() -> int:
    runner = pyperf.Runner(add_cmdline_args=add_cmdline_args)
    parser = runner.argparser
    parser.add_argument("--inputs", help="directory --prepare wrote: usage corpus and C# answers")
    parser.add_argument("--prepare", metavar="DIR", help="write the inputs into DIR and exit")
    parser.add_argument("--check-only", action="store_true", help="answer check only, no timing")
    parser.add_argument("--rows", help="only rows whose name matches this regular expression")
    parser.add_argument("--git-sha", help=argparse.SUPPRESS)
    parser.add_argument("--answer-status", help=argparse.SUPPRESS)
    args = runner.parse_args()

    if args.prepare:
        prepare(args.prepare)
        return 0
    if not args.inputs:
        parser.error("--inputs DIR is required (write it with --prepare DIR)")

    rows = workload_rows() + usage_rows(args.inputs)
    if args.rows:
        rows = [r for r in rows if re.search(args.rows, r.name)]

    if not args.worker:
        args.git_sha = git_sha()
        status = check(rows, args.inputs)
        print(f"Answer check against C#, {len(rows)} rows ({len(EXCLUDED)} excluded):")
        for row in rows:
            print(f"  {row.name:<34} {flags_text(row.flags):<10} {status[row.name]}")
        for name, reason in EXCLUDED.items():
            print(f"  {name:<34} {'-':<10} excluded: {reason}")
        if args.check_only:
            return 0 if all(s == "same" for s in status.values()) else 1
        args.answer_status = os.path.join(args.inputs, "answer-status.json")
        with open(args.answer_status, "w", encoding="utf-8") as f:
            json.dump(status, f)
    with open(args.answer_status, encoding="utf-8") as f:
        status = json.load(f)

    runner.metadata.update({
        "regex_version": regex.__version__,
        "python_version": platform.python_version(),
        "pyperf_version": pyperf.__version__,
        "git_sha": args.git_sha,
        "excluded_rows": json.dumps(EXCLUDED),
    })

    results = []
    for row in rows:
        bench = runner.bench_func(row.name, row.timed, metadata={
            "regex_flags": flags_text(row.flags),
            "regex_version_flag": "V0" if row.flags & V0 else "V1",
            "operation": row.operation,
            "answer_check": status[row.name],
        })
        results.append((row, bench))

    if not args.worker:
        print(f"\n{'row':<34} {'median':>12} {'stdev':>12}  {'flags':<10} answer check")
        for row, bench in results:
            if bench is None:
                continue
            stdev = bench.stdev() if bench.get_nvalue() > 1 else 0.0
            print(f"{row.name:<34} {bench.format_value(bench.median()):>12} {bench.format_value(stdev):>12}  "
                  f"{flags_text(row.flags):<10} {status[row.name]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
