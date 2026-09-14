r"""Where upstream's `BESTMATCH` loses a partial: `(*SKIP)` leaks the slice into the partial retry.

Ledger entry 13. S47c traced it to the line in a `/Od /Zi` build of the pinned 2026.9.10 source,
stepped with `fprintf` instrumentation. The chain, every link measured by this file:

  1. `_regex.c:14555` - `RE_OP_SKIP` sets `state->slice_start = state->text_pos`. On
     `(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)` over `'ab.'` that is `slice_start 0 -> 3`, during the
     NORMAL (non-partial) attempt, which then fails.
  2. `_regex.c:18170` - `do_match` falls back to the partial attempt and restores `text_pos`
     ALONE. The slice stays where `(*SKIP)` left it, so the second attempt runs with
     `slice=[3,3]` and `text_pos=0`.
  3. `_regex.c:17625` - `do_best_fuzzy_match`'s scan loop is guarded by
     `state->slice_start <= start_pos && start_pos <= state->slice_end`. With `slice_start=3` and
     `start_pos=0` that guard is FALSE, the loop body never runs once, and `status` keeps the
     initialiser `RE_ERROR_FAILURE` it was given at `:17599`. THIS IS THE DISCARD: the partial is
     not ranked and rejected, it is never attempted.

Why the flag matters, and it is not a ranking rule at all. `do_simple_fuzzy_match` (the flagless
path) is handed the SAME leaked `slice=[3,3]` - the trace shows it - and still answers, because it
has no such guard: it calls `basic_match` from `text_pos` and lets the match walk. So `(?b)` does
not filter the match out; it routes the retry through the one entry point whose loop guard the
leaked bound falsifies.

`do_enhanced_fuzzy_match` restores the slice on every return that is not a hard error (`:18003`; the
`goto error` at `:18001` is the exception, and it aborts the whole match anyway), which is upstream's
own statement that the slice is per-attempt state. `do_best_fuzzy_match` restores it only inside its
`found_match && fewest_errors > 0` branch (`:17848`), so an attempt that merely fails leaks.

Two fixes, both measured here, both leaving upstream's own suite at 101 run / 0 failed:

  A  `do_match:18155-18159` saves `slice_start`/`slice_end` beside `text_pos` and `:18170` restores
     them.
     Fixes all five judged wave rows AND the minimised shape. It also reaches the non-BESTMATCH
     retry, which changes one flagless answer (row 77937 gains `fuzzy=(1,1,1)` and group 2).
     THIS IS WHAT THIS PORT ALREADY DOES, at `Matcher.cs:10098-10100`, chosen in S40b on
     self-refutation grounds before the upstream mechanism was known.
  B  `do_best_fuzzy_match` saves the slice at entry and restores it on every return, as its sibling
     `do_enhanced_fuzzy_match` does. Fixes the same five rows and the minimised shape, and touches
     nothing on the non-BESTMATCH path.

With either fix, upstream's answer UNDER `(?b)` equals this port's answer IN FULL - span, groups and
fuzzy counts - on all five judged rows, row 77937 included. That retires this entry's old caveat
that 77937 agreed on the span alone: the flagless answer was simply the wrong yardstick for it.

Measured 2026-09-14, CPython 3.14.6 / Windows, MSVC 14.44.35207, against the pinned upstream
2026.9.10 checkout in `upstream/`.

Usage:
  python tools/probes/upstream-bestmatch-lost-candidate.py
      The plain contradiction against whatever `regex` is installed. No compiler needed.
  python tools/probes/upstream-bestmatch-lost-candidate.py --trace
      Builds an instrumented `/Od /Zi` copy of `upstream/` and prints the trace above.
  python tools/probes/upstream-bestmatch-lost-candidate.py --fix
      Builds stock and fixed copies, prints the five wave rows from each, and runs upstream's
      own unittest suite against both.
  ... --all    does all three.

The builds need MSVC. Set REGEX_VCVARS to a `vcvars64.bat` if it is not in a default location.
Everything is built under `.scratch/`, which is gitignored; `upstream/` is never touched.
"""

import importlib.machinery
import os
import shutil
import subprocess
import sys
import sysconfig

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UPSTREAM = os.path.join(ROOT, "upstream")
SCRATCH = os.path.join(ROOT, ".scratch")
PYD = "_regex" + importlib.machinery.EXTENSION_SUFFIXES[0]

VCVARS_CANDIDATES = [
    os.environ.get("REGEX_VCVARS", ""),
    r"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat",
    r"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat",
    r"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
]

MINIMUM = r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)"
SUBJECT = "ab."

# The five judged rows of ledger entry 13, each asked the operation the wave asked it. Spans are
# in CODEPOINTS here, which is what Python counts; this port reports UTF-16 code units, so an
# astral subject differs by the surrogate count.
WAVE_ROWS = [
    (
        "seed 7 row 74938",
        r"(?b)(?r)(?:[^\d]+(*SKIP)\p{L}|[^\d])(?:(?:(\p{Nd}{1})(?:(?P<g2>\p{ASCII})){e<=2,i<=1}){s<=1,i<=1,d<=1}(*SKIP)\p{L}|\w)\D",
        " \U00010428 ",
        "match",
    ),
    (
        "seed 7 row 77937",
        r"(?b)(?r)^(\d)(?:([^\d]{3,4}?)a(?:[[:alpha:]]{2,3}?){e<=2,s<=1:[A-Za-z_]}){s<=1,i<=1,d<=1}(?:\S*?(*SKIP)\w|[^\d])",
        "a\n\U00010400\r\na\U0001d518",
        "search",
    ),
    (
        "seed 4242 row 76251",
        r"(?b)(?e)^(?:(?:AA){s<=1,i<=1,d<=1}(*SKIP)\p{ASCII}|\s)(?:(?:A([[a-z]--[aei]])(?:(\D*)){e<=2,s<=1}){i<=1}(*SKIP)\p{ASCII}|[A-Z])",
        "A",
        "fullmatch",
    ),
    (
        "seed 4242 row 76681",
        r"(?b)(?e)(?:a\w){s<=1,i<=1,d<=1}(?:\S(*SKIP)[\p{L}\p{N}]|\W)",
        "\r\na\U0001d518\n",
        "search",
    ),
    (
        "seed 20260913 row 76593",
        "(?b)\ufb01(?:(?:(.)\ufb01\ufb01){s<=1:[^a-z]}(*SKIP)[A-Z]|\\p{ASCII})(?P<g2>[[:digit:]])?",
        "\ufb01\ufb01\ufb01\ufb01\u00df\u00df\n ",
        "search",
    ),
]

# Two more shapes the docs make claims about, so the probe has to measure them too.
#
#   * The minimised REVERSED shape. It is row 7 of this entry's pin. `RE_OP_SKIP` writes `slice_end`
#     rather than `slice_start` when the node is `RE_STATUS_REVERSE` (`:14553`), and the only other
#     pinned rows that reach that arm are wave rows 1 and 2 - each several hundred characters of
#     generated pattern. This is its minimised form, which is why row 6 exists beside the forward
#     wave rows too.
#   * Seed 20260914 row 76345, which S47b un-classified from the sibling entry. It is NOT pinned
#     here, because its recorded question is no longer on disk; what is claimed about it is only
#     that this mechanism reaches it, and this is where that is measured. Note the row is the
#     `search` from position 0: upstream does answer this pattern from other doors, so a claim of
#     "None at every door" would be false.
EXTRA_ROWS = [
    (
        "the minimised shape reversed (pin row 7)",
        r"(?b)(?r)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)",
        ".ab",
        "search",
    ),
    (
        "seed 20260914 row 76345 (explained, NOT pinned)",
        r"(?b)(?e)\b(?:\p{Ll}(*SKIP)[^\d]|\W)(?=(?:(\p{ASCII}+)([^\d]*)a){e<=2,s<=1})",
        "aaa",
        "search",
    ),
]

# The `fprintf` instrumentation. Each anchor must appear exactly once in the pinned source; a
# count of anything else means upstream moved and the trace below would be describing code that is
# no longer there, so the probe stops rather than print a stale story.
TRACE_PATCHES = [
    (
        "/* #define VERBOSE */\n",
        "/* #define VERBOSE */\n"
        '#define LC(...) do { fprintf(stderr, "[LC] " __VA_ARGS__); fflush(stderr); } while (0)\n',
    ),
    (
        '    TRACE(("<<do_best_fuzzy_match>>\\n"))\n',
        '    TRACE(("<<do_best_fuzzy_match>>\\n"))\n'
        '    LC("ENTER best search=%d text_pos=%zd slice=[%zd,%zd] partial_side=%d\\n",\n'
        "      (int)search, state->text_pos, state->slice_start, state->slice_end,\n"
        "      (int)state->partial_side);\n",
    ),
    (
        '    TRACE(("<<do_simple_fuzzy_match>>\\n"))\n',
        '    TRACE(("<<do_simple_fuzzy_match>>\\n"))\n'
        '    LC("ENTER simple search=%d text_pos=%zd slice=[%zd,%zd] partial_side=%d\\n",\n'
        "      (int)search, state->text_pos, state->slice_start, state->slice_end,\n"
        "      (int)state->partial_side);\n",
    ),
    (
        "            if (node->status & RE_STATUS_REVERSE)\n"
        "                state->slice_end = state->text_pos;\n"
        "            else\n"
        "                state->slice_start = state->text_pos;\n",
        "            if (node->status & RE_STATUS_REVERSE) {\n"
        '                LC("    SKIP :14553 slice_end %zd -> %zd\\n", state->slice_end,\n'
        "                  state->text_pos);\n"
        "                state->slice_end = state->text_pos;\n"
        "            } else {\n"
        '                LC("    SKIP :14555 slice_start %zd -> %zd\\n", state->slice_start,\n'
        "                  state->text_pos);\n"
        "                state->slice_start = state->text_pos;\n"
        "            }\n",
    ),
    (
        "        if (status == RE_ERROR_SUCCESS)\n"
        "            status = basic_match(state, search);\n"
        "\n"
        "        /* Has an error occurred, or is it a partial match? */\n"
        "        if (status < 0)\n"
        "            goto error;\n"
        "\n"
        "        if (status == RE_ERROR_FAILURE)\n"
        "            break;\n",
        '        LC("  scan :17625 GUARD PASSED start_pos=%zd slice=[%zd,%zd]\\n", start_pos,\n'
        "          state->slice_start, state->slice_end);\n"
        "        if (status == RE_ERROR_SUCCESS)\n"
        "            status = basic_match(state, search);\n"
        '        LC("  scan :17641 basic_match -> status=%d total_errors=%zd match_pos=%zd "\n'
        '          "text_pos=%zd\\n", status, (Py_ssize_t)state->total_errors, state->match_pos,\n'
        "          state->text_pos);\n"
        "\n"
        "        /* Has an error occurred, or is it a partial match? */\n"
        "        if (status < 0) {\n"
        '            LC("  scan :17645 goto error with status=%d (PARTIAL is -13)\\n", status);\n'
        "            goto error;\n"
        "        }\n"
        "\n"
        "        if (status == RE_ERROR_FAILURE) {\n"
        '            LC("  scan :17648 break on FAILURE\\n");\n'
        "            break;\n"
        "        }\n",
    ),
    (
        "        if (status == RE_ERROR_FAILURE) {\n"
        "            /* Fall back to the partial match as originally requested. */\n"
        "            state->text_pos = text_pos;\n"
        "            status = do_match_2(state, search);\n"
        "        }\n",
        "        if (status == RE_ERROR_FAILURE) {\n"
        "            /* Fall back to the partial match as originally requested. */\n"
        '            LC("do_match :18170 partial retry: text_pos restored to %zd, slice LEFT at "\n'
        '              "[%zd,%zd]\\n", text_pos, state->slice_start, state->slice_end);\n'
        "            state->text_pos = text_pos;\n"
        "            status = do_match_2(state, search);\n"
        "        }\n",
    ),
    (
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "\n"
        "    return status;\n"
        "\n"
        "mem_error:\n",
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "\n"
        '    LC("RETURN :17857 status=%d (0 is RE_ERROR_FAILURE)\\n", status);\n'
        "    return status;\n"
        "\n"
        "mem_error:\n",
    ),
    (
        "error:\n"
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "    return status;\n"
        "}\n",
        "error:\n"
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        '    LC("RETURN :17865 (error path) status=%d\\n", status);\n'
        "    return status;\n"
        "}\n",
    ),
]

# Fix A: do_match restores the slice, not only text_pos, before the partial retry. This is what
# this port does at Matcher.cs:10098-10100.
FIX_A_PATCHES = [
    (
        "        int partial_side;\n"
        "        Py_ssize_t text_pos;\n"
        "\n"
        "        partial_side = state->partial_side;\n"
        "        text_pos = state->text_pos;\n",
        "        int partial_side;\n"
        "        Py_ssize_t text_pos;\n"
        "        Py_ssize_t slice_start;\n"
        "        Py_ssize_t slice_end;\n"
        "\n"
        "        partial_side = state->partial_side;\n"
        "        text_pos = state->text_pos;\n"
        "        slice_start = state->slice_start;\n"
        "        slice_end = state->slice_end;\n",
    ),
    (
        "            /* Fall back to the partial match as originally requested. */\n"
        "            state->text_pos = text_pos;\n",
        "            /* Fall back to the partial match as originally requested. */\n"
        "            state->text_pos = text_pos;\n"
        "            state->slice_start = slice_start;\n"
        "            state->slice_end = slice_end;\n",
    ),
]

# Fix B: do_best_fuzzy_match restores the slice on every exit, as do_enhanced_fuzzy_match does.
FIX_B_PATCHES = [
    (
        "    RE_BestList best_list;\n"
        "    RE_BestChangesList best_changes_list;\n"
        "    Py_ssize_t start_pos;\n"
        "    int status = RE_ERROR_FAILURE;\n",
        "    RE_BestList best_list;\n"
        "    RE_BestChangesList best_changes_list;\n"
        "    Py_ssize_t start_pos;\n"
        "    Py_ssize_t entry_slice_start;\n"
        "    Py_ssize_t entry_slice_end;\n"
        "    int status = RE_ERROR_FAILURE;\n",
    ),
    (
        "    init_best_list(&best_list);\n    init_best_changes_list(&best_changes_list);\n",
        "    entry_slice_start = state->slice_start;\n"
        "    entry_slice_end = state->slice_end;\n"
        "\n"
        "    init_best_list(&best_list);\n"
        "    init_best_changes_list(&best_changes_list);\n",
    ),
    (
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "\n"
        "    return status;\n"
        "\n"
        "mem_error:\n",
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "\n"
        "    state->slice_start = entry_slice_start;\n"
        "    state->slice_end = entry_slice_end;\n"
        "\n"
        "    return status;\n"
        "\n"
        "mem_error:\n",
    ),
    (
        "error:\n"
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "    return status;\n"
        "}\n",
        "error:\n"
        "    fini_best_list(state, &best_list);\n"
        "    fini_best_changes_list(state, &best_changes_list);\n"
        "    state->slice_start = entry_slice_start;\n"
        "    state->slice_end = entry_slice_end;\n"
        "    return status;\n"
        "}\n",
    ),
]


def say(text):
    sys.stdout.buffer.write(text.encode("utf-8", "backslashreplace") + b"\n")
    sys.stdout.flush()


def find_vcvars():
    for candidate in VCVARS_CANDIDATES:
        if candidate and os.path.isfile(candidate):
            return candidate
    raise SystemExit(
        "No vcvars64.bat found. Install the Visual Studio Build Tools C++ workload, or set "
        "REGEX_VCVARS to its vcvars64.bat."
    )


def patch(path, patches):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    for anchor, replacement in patches:
        count = text.count(anchor)
        if count != 1:
            raise SystemExit(
                "anchor appears %d times, expected 1 - upstream has moved and this probe's line "
                "numbers no longer describe it:\n%s" % (count, anchor[:200])
            )
        text = text.replace(anchor, replacement)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)


def build(name, patches=()):
    """Copy upstream/ to .scratch/<name>, apply patches, and build it /Od /Zi. Returns the tree."""
    tree = os.path.join(SCRATCH, name)
    if os.path.isdir(tree):
        shutil.rmtree(tree)
    shutil.copytree(UPSTREAM, tree)
    if patches:
        patch(os.path.join(tree, "src", "_regex.c"), patches)

    obj = os.path.join(tree, "build-obj")
    os.makedirs(obj, exist_ok=True)
    include = sysconfig.get_paths()["include"]
    libs = os.path.join(sys.base_prefix, "libs")
    cl = (
        f'cl /nologo /Od /Zi /FS /MD /W3 /I "{include}" /Fo"{obj}\\\\" /Fd"{obj}\\\\vc.pdb" '
        f'"{tree}\\src\\_regex.c" "{tree}\\src\\_regex_unicode.c" '
        f'/LD /Fe:"{tree}\\regex\\{PYD}" /link /LIBPATH:"{libs}" /DEBUG'
    )
    bat = os.path.join(SCRATCH, "build-%s.bat" % name)
    with open(bat, "w") as f:
        f.write('@echo off\ncall "%s" >nul\n%s\n' % (find_vcvars(), cl))
    done = subprocess.run(["cmd", "/c", bat], cwd=tree, capture_output=True, text=True)
    if done.returncode != 0:
        say(done.stdout[-4000:])
        say(done.stderr[-4000:])
        raise SystemExit("build of %s failed" % name)
    say("built %s -> %s" % (name, os.path.join(tree, "regex", PYD)))
    return tree


def run_in(tree, source, *args):
    """Run a snippet against a built tree, in its own interpreter, and return (stdout, stderr)."""
    done = subprocess.run(
        [sys.executable, "-c",
         "import sys; sys.path.insert(0, sys.argv[1])\n" + source, tree, *args],
        cwd=tree, capture_output=True, text=True,
    )
    return done.stdout, done.stderr


ANSWERS = r"""
import regex
print('using', regex._regex.__file__)

def full(m):
    if m is None:
        return 'None'
    spans = [m.span(i) for i in range(1, m.re.groups + 1)]
    return '%s%s spans=%s fuzzy=%s' % (m.span(), 'P' if m.partial else '', spans, m.fuzzy_counts)

import json
for label, pattern, subject, operation in json.loads(sys.argv[2]):
    with_flag = getattr(regex.compile(pattern), operation)(subject, partial=True, timeout=10.0)
    without = getattr(regex.compile(pattern[4:]), operation)(subject, partial=True, timeout=10.0)
    out = '%-24s %-10s (?b)=%s\n%-24s %-10s none=%s' % (
        label, operation, full(with_flag), '', '', full(without))
    sys.stdout.buffer.write(out.encode('utf-8', 'backslashreplace') + b'\n')
"""

SUITE = r"""
import regex, unittest
print('using', regex._regex.__file__)
from regex.tests import test_regex
r = unittest.TextTestRunner(verbosity=0).run(
    unittest.defaultTestLoader.loadTestsFromModule(test_regex))
print('RAN', r.testsRun, 'FAIL', len(r.failures), 'ERR', len(r.errors))
print('\n'.join(sorted(str(t[0]) for t in r.failures + r.errors)))
"""


def plain_contradiction():
    """No compiler needed: the same compiled pattern denies from `search` what its `match` finds."""
    import regex

    say("--- the plain contradiction, against the installed regex %s ---" % regex.__version__)
    say("    %s" % regex.__file__)
    compiled = regex.compile(MINIMUM)
    say("  search(%r, partial=True)        -> %s" % (SUBJECT, compiled.search(SUBJECT, partial=True)))
    for start in range(len(SUBJECT) + 1):
        say("  match(%r, %d, partial=True)     -> %s"
            % (SUBJECT, start, compiled.match(SUBJECT, start, partial=True)))
    say("  the same pattern without (?b)   -> %s"
        % regex.compile(MINIMUM[4:]).search(SUBJECT, partial=True))


def trace():
    tree = build("regex-lostcand-trace", TRACE_PATCHES)
    source = r"""
import regex
print('using', regex._regex.__file__)
pattern, subject = sys.argv[2], sys.argv[3]

def ask(label, fn):
    sys.stderr.write('\n=== ' + label + ' ===\n')
    sys.stderr.flush()
    sys.stderr.write('ANSWER: ' + repr(fn()) + '\n')
    sys.stderr.flush()

p = regex.compile(pattern)
ask('(?b) search', lambda: p.search(subject, partial=True))
ask('(?b) match at 0', lambda: p.match(subject, 0, partial=True))
ask('(?b) match at 2', lambda: p.match(subject, 2, partial=True))
q = regex.compile(pattern[4:])
ask('flagless search', lambda: q.search(subject, partial=True))
"""
    out, err = run_in(tree, source, MINIMUM, SUBJECT)
    say("--- the instrumented trace ---")
    say(out.strip())
    say(err.strip())

    # Which arm of the leak each pinned row fires. The forward arm writes `slice_start` (:14555) and
    # the reversed one writes `slice_end` (:14553); the two `(?r)` wave rows reach the reversed arm,
    # so the minimised reversed shape is a smaller witness of it rather than the only one.
    import json

    arms = r"""
import json, regex
rows = json.loads(sys.argv[2])
for label, pattern, subject, operation in rows:
    sys.stderr.write('\n=== ' + label + ' ===\n')
    sys.stderr.flush()
    getattr(regex.compile(pattern), operation)(subject, partial=True, timeout=10.0)
    sys.stderr.flush()
"""
    everything = WAVE_ROWS + [("the minimised shape", MINIMUM, SUBJECT, "search")] + EXTRA_ROWS
    _, err = run_in(tree, arms, json.dumps(everything))
    say("")
    say("--- which SKIP arm each row fires ---")
    for line in err.splitlines():
        if line.startswith("===") or "SKIP :" in line:
            say(line)


def fixes():
    import json

    rows = json.dumps(WAVE_ROWS)
    trees = {
        "stock": build("regex-lostcand-stock"),
        "fix A (do_match restores the slice)": build("regex-lostcand-fixa", FIX_A_PATCHES),
        "fix B (do_best_fuzzy_match restores the slice)": build("regex-lostcand-fixb", FIX_B_PATCHES),
    }
    for label, tree in trees.items():
        say("")
        say("--- %s: the minimised shape ---" % label)
        out, err = run_in(tree, r"""
import regex
print('using', regex._regex.__file__)
p = regex.compile(sys.argv[2])
print('search  :', p.search(sys.argv[3], partial=True))
print('match@0 :', p.match(sys.argv[3], 0, partial=True))
print('match@2 :', p.match(sys.argv[3], 2, partial=True))
print('flagless:', regex.compile(sys.argv[2][4:]).search(sys.argv[3], partial=True))
""", MINIMUM, SUBJECT)
        say(out.strip() or err.strip())

        say("--- %s: the five judged wave rows ---" % label)
        out, err = run_in(tree, ANSWERS, rows)
        say(out.strip() or err.strip())

        say("--- %s: the reversed minimum, and row 76345 ---" % label)
        out, err = run_in(tree, ANSWERS, json.dumps(EXTRA_ROWS))
        say(out.strip() or err.strip())

        say("--- %s: upstream's own test suite ---" % label)
        out, err = run_in(tree, SUITE)
        say(out.strip() or err.strip()[-800:])


def main():
    args = sys.argv[1:]
    everything = "--all" in args
    if everything or not args or "--plain" in args:
        plain_contradiction()
    if everything or "--trace" in args:
        say("")
        trace()
    if everything or "--fix" in args:
        say("")
        fixes()


main()
