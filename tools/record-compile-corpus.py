#!/usr/bin/env python
"""Records upstream's compiler output for every pattern its own test suite compiles.

Phase 2 of the port turns pattern text into bytecode and nothing else: no opcode can match a
string yet, so the ported test suite verifies almost none of it. Upstream's bytecode, though, is
observable. ``regex._main._compile`` hands the finished code to ``regex._regex.compile`` as plain
Python values (upstream/regex/_main.py:660-663), so running upstream's own suite with that call
intercepted records exactly what our compiler has to produce, for every pattern the suite
exercises. That fixture is the oracle for slices S07-S13.

Three hooks, all installed on module attributes that are looked up at call time:

* ``regex._regex.compile`` - one row per successful compile.
* ``regex._main._compile`` - one row per ``regex.error``, and the source of the *input* flags and
  named lists, which the ``_regex.compile`` call no longer carries.
* ``regex._main._compile_replacement_helper`` - one row per replacement template. The C engine
  looks this function up by name at call time (upstream/src/_regex.c:19918, :21835), so wrapping
  the module attribute is seen.

Determinism: see ``_install_set_order_patches``.

Usage::

    python tools/record-compile-corpus.py [--output PATH] [--check]

``--check`` regenerates into a temporary file and diffs it against the committed fixture rather
than overwriting it; that is what CI runs.
"""

from __future__ import annotations

import argparse
import filecmp
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
UPSTREAM_TESTS = REPO_ROOT / "upstream" / "regex" / "tests" / "test_regex.py"
DEFAULT_OUTPUT = REPO_ROOT / "tests" / "FuzzyRegex.Tests" / "Gaps" / "CompileParity" / "corpus.json"

FIXTURE_COMMENT = (
    "GENERATED FILE - do not edit by hand. Written by tools/record-compile-corpus.py from a run "
    "of upstream's own test suite with regex._regex.compile intercepted. Regenerate: "
    "python tools/record-compile-corpus.py"
)


# --------------------------------------------------------------------------------------------
# Sorting the two places where a Python set becomes an ordered list
# --------------------------------------------------------------------------------------------


def _render_key(value):
    """Renders a node's ``_key`` as a string that C# can compute identically.

    ``RegexBase.__hash__`` hashes ``self._key``, whose first element is the node's *class object*
    (upstream/regex/_regex_core.py:1943, :1999). A class object's hash is its address, so it
    varies from one process to the next no matter what ``PYTHONHASHSEED`` says, and any place
    upstream turns a ``set`` of nodes into a list therefore produces a per-process order. Class
    name instead of class object, recursing into nested nodes and tuples, is stable and is
    computable on both sides of the port.
    """
    if isinstance(value, type):
        return value.__name__
    key = getattr(value, "_key", None)
    if key is not None:
        return _render_key(key)
    if isinstance(value, (tuple, list)):
        return "(" + ",".join(_render_key(v) for v in value) + ")"
    if isinstance(value, (set, frozenset)):
        return "{" + ",".join(sorted(_render_key(v) for v in value)) + "}"
    return repr(value)


def _install_set_order_patches(regex_core):
    """Sorts the two set-to-list conversions that make upstream's bytecode nondeterministic.

    Measured 2026-08-30 with these patches disabled: eight runs disagreed on 111 of the suite's
    1547 compiles, and no two runs disagreed on the same set (51 rows for one pair, 82 for
    another) - so any single pair of runs undercounts. Two sites are responsible, both turning an
    unordered ``set`` of parse nodes into an ordered ``list``:

    * ``_check_firstset`` (upstream/regex/_regex_core.py:380-411), ``members`` into ``SetUnion``.
    * ``Branch._flush_set_members`` (upstream/regex/_regex_core.py:2470-2483), ``items`` into
      ``SetUnion``, reached from ``Branch._reduce_to_set`` (:2417-2443).

    Set-member order carries no matching semantics, so sorting at exactly those two points is
    safe, and the port sorts identically. Recorded in the "Where we diverge" table of
    docs/PORTMAP.md. The alternative - canonicalising in the C# comparator - needs a bytecode
    decoder, which is Phase 3 work and a second thing to get wrong.

    Each replacement is upstream's body with ``list(...)`` changed to ``sorted(..., key=...)``
    and nothing else, so a future upstream diff over these functions is easy to reapply.
    """
    Character = regex_core.Character
    SetUnion = regex_core.SetUnion
    NOCASE = regex_core.NOCASE
    FULLCASE = regex_core.FULLCASE
    IGNORECASE = regex_core.IGNORECASE

    def _check_firstset(info, reverse, fs):
        "Checks the firstset for the pattern."
        if not fs or None in fs:
            return None

        # If we ignore the case, for simplicity we won't build a firstset.
        members = set()
        case_flags = NOCASE
        for i in fs:
            if isinstance(i, Character) and not i.positive:
                return None

            case_flags |= i.case_flags
            members.add(i.with_flags(case_flags=NOCASE))

        if case_flags == (FULLCASE | IGNORECASE):
            return None

        # Build the firstset. PORT: sorted(), not list().
        fs = SetUnion(info, sorted(members, key=_render_key),
          case_flags=case_flags & ~FULLCASE, zerowidth=True)
        fs = fs.optimise(info, reverse, in_set=True)

        return fs

    def _flush_set_members(info, reverse, items, case_flags, new_branches):
        # Flush the set members.
        if not items:
            return

        # PORT: sorted(), not list(), in both branches.
        ordered = sorted(items, key=_render_key)
        if len(ordered) == 1:
            item = ordered[0]
        else:
            item = SetUnion(info, ordered).optimise(info, reverse)

        new_branches.append(item.with_flags(case_flags=case_flags))

        items.clear()

    regex_core._check_firstset = _check_firstset
    regex_core.Branch._flush_set_members = staticmethod(_flush_set_members)


# --------------------------------------------------------------------------------------------
# The three recording hooks
# --------------------------------------------------------------------------------------------


class Recorder:
    """Collects deduplicated compile, error and template rows while the suite runs."""

    def __init__(self):
        self.compiles: dict[tuple, dict] = {}
        self.errors: dict[tuple, dict] = {}
        self.templates: dict[tuple, dict] = {}
        # The input flags and named lists that _regex.compile no longer carries. A list because
        # _compile is re-entrant: regex._main line 740 compiles '' at import time, and a pattern
        # that raises _UnscopedFlagSet re-parses inside the same call.
        self._pending: list[tuple[int, dict]] = []
        self.default_versions: set[int] = set()

    # -- regex._main._compile ------------------------------------------------------------

    def wrap_compile(self, regex_main, error_type):
        inner = regex_main._compile

        def _compile(pattern, flags, ignore_unused, kwargs, cache_it):
            # cache_it=False so every call really compiles. A cache hit would hide a pattern
            # that a later test compiles under different named lists, and would make the error
            # rows depend on test execution order.
            kwargs = _canonical_kwargs(kwargs)
            self._pending.append((flags, kwargs))
            try:
                return inner(pattern, flags, ignore_unused, kwargs, False)
            except Exception as e:
                # Not just error_type: upstream rejects a flags conflict with a plain ValueError
                # (upstream/regex/_main.py:559-568), before any code is generated. Catching only
                # regex.error left four (pattern, flags) pairs with no row of any kind, so a port
                # that happily compiled them would have gone unnoticed. Measured 2026-08-30: 46
                # regex.error rows and 4 ValueError rows.
                if isinstance(pattern, str):
                    key = (pattern, flags, _named_lists_key(kwargs))
                    is_parse_error = isinstance(e, error_type)
                    self.errors.setdefault(key, {
                        "pattern": pattern,
                        "flags": flags,
                        "namedLists": _named_lists(kwargs),
                        "exception": type(e).__name__,
                        "message": e.msg if is_parse_error else str(e),
                        # Only regex.error carries an offset into the pattern.
                        "position": e.pos if is_parse_error else None,
                    })
                raise
            finally:
                self._pending.pop()

        regex_main._compile = _compile

    # -- regex._regex.compile ------------------------------------------------------------

    def wrap_regex_compile(self, regex_module, default_version_holder):
        inner = regex_module.compile

        def compile(pattern, flags, code, group_index, index_group, named_lists,
                    named_list_indexes, req_offset, req_chars, req_flags, group_count):
            if isinstance(pattern, str) and self._pending:
                input_flags, kwargs = self._pending[-1]
                self.default_versions.add(default_version_holder.DEFAULT_VERSION)
                key = (pattern, input_flags, _named_lists_key(kwargs))
                self.compiles.setdefault(key, {
                    "pattern": pattern,
                    "flags": input_flags,
                    "namedLists": _named_lists(kwargs),
                    "resolvedFlags": flags,
                    "code": list(code),
                    "groupIndex": dict(sorted(group_index.items())),
                    "compiledNamedLists": {k: sorted(v) for k, v in sorted(named_lists.items())},
                    "namedListIndexes": [sorted(v) for v in named_list_indexes],
                    "reqOffset": req_offset,
                    "reqChars": list(req_chars),
                    "reqFlags": req_flags,
                    "groupCount": group_count,
                })
            return inner(pattern, flags, code, group_index, index_group, named_lists,
                         named_list_indexes, req_offset, req_chars, req_flags, group_count)

        # copyreg registers _regex.compile as the reconstructor for a pickled Pattern
        # (upstream/regex/_main.py:754-757), and pickle saves a function by module and qualified
        # name. Without these three lines test_hg_bugs fails on "Can't pickle local object".
        compile.__module__ = inner.__module__
        compile.__qualname__ = inner.__qualname__
        compile.__name__ = inner.__name__
        regex_module.compile = compile

    # -- regex._main._compile_replacement_helper ------------------------------------------

    def wrap_replacement_helper(self, regex_main):
        inner = regex_main._compile_replacement_helper

        def _compile_replacement_helper(pattern, template):
            compiled = inner(pattern, template)
            if isinstance(pattern.pattern, str) and isinstance(template, str):
                key = (pattern.pattern, pattern.flags, template)
                self.templates.setdefault(key, {
                    # Provenance only: nothing in the replacement compiler reads the pattern
                    # text or its flags. compile_repl_group (upstream/regex/_regex_core.py:
                    # 1902-1918) reads pattern.groups and pattern.groupindex and nothing else,
                    # so those two are the seam's real inputs and the C# test needs no compiled
                    # pattern to run this row.
                    "pattern": pattern.pattern,
                    "patternFlags": pattern.flags,
                    "groupCount": pattern.groups,
                    "groupIndex": dict(sorted(pattern.groupindex.items())),
                    "template": template,
                    # ints are group references, strings are literal runs.
                    "compiled": list(compiled),
                })
            return compiled

        regex_main._compile_replacement_helper = _compile_replacement_helper


def _canonical_kwargs(kwargs):
    """Replaces each named list with a sorted list, so its order is not a Python set's.

    Third ordering leak, found on 2026-08-30 by the two-seed check rather than by reading:
    ``StringSet.__init__`` (upstream/regex/_regex_core.py:4088-4100) iterates
    ``info.kwargs[name]`` and sorts the branches by length only, a *stable* sort, so two
    equal-length members keep the caller's iteration order. Upstream's own suite passes a
    ``set`` (test_regex.py:2578-2579), and a set's order is per-process.

    This one is not upstream's to fix and is not sorted in the port: the leak is in the
    *caller's* container, so the recorder canonicalises the input and the port keeps whatever
    order its caller gave. The fixture stores each named list sorted, which is then exactly the
    order upstream compiled it in.
    """
    if not kwargs:
        return kwargs
    canonical = {}
    for name, values in kwargs.items():
        try:
            canonical[name] = sorted(values)
        except TypeError:
            canonical[name] = values
    return canonical


def _named_lists(kwargs):
    """The named lists as JSON: name to sorted list of values, string values only."""
    if not kwargs:
        return {}
    result = {}
    for name, values in sorted(kwargs.items()):
        try:
            items = sorted(values)
        except TypeError:
            continue
        if all(isinstance(v, str) for v in items):
            result[name] = items
    return result


def _named_lists_key(kwargs):
    return tuple((name, tuple(values)) for name, values in sorted(_named_lists(kwargs).items()))


# --------------------------------------------------------------------------------------------
# Running the suite
# --------------------------------------------------------------------------------------------


def _load_test_module(work_dir: Path):
    """Imports upstream's test file from a copy outside upstream/.

    Importing it in place would put ``upstream/`` on ``sys.path``, where ``import regex`` finds
    the uncompiled submodule package (no built ``_regex`` C extension) instead of the installed
    module. Copying the one file sidesteps that without touching the submodule.
    """
    copied = work_dir / "test_regex.py"
    shutil.copyfile(UPSTREAM_TESTS, copied)
    spec = importlib.util.spec_from_file_location("upstream_test_regex", copied)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def record() -> dict:
    import regex
    import regex._main
    import regex._regex
    import regex._regex_core

    _install_set_order_patches(regex._regex_core)

    recorder = Recorder()
    recorder.wrap_compile(regex._main, regex.error)
    recorder.wrap_regex_compile(regex._regex, regex._main)
    recorder.wrap_replacement_helper(regex._main)

    with tempfile.TemporaryDirectory() as tmp:
        module = _load_test_module(Path(tmp))
        suite = unittest.defaultTestLoader.loadTestsFromModule(module)
        runner = unittest.TextTestRunner(stream=sys.stderr, verbosity=0)
        result = runner.run(suite)

    if not result.wasSuccessful():
        # A failing upstream suite means the oracle is not the module we think it is, and every
        # row it produced is suspect.
        raise SystemExit(
            f"upstream's suite is not green against regex {regex.__version__}: "
            f"{len(result.failures)} failures, {len(result.errors)} errors"
        )

    astral = sorted({p for p in
                     [row["pattern"] for row in recorder.compiles.values()]
                     + [row["pattern"] for row in recorder.errors.values()]
                     + [row["template"] for row in recorder.templates.values()]
                     if any(ord(c) > 0xFFFF for c in p)})
    if astral:
        # Python indexes a str by codepoint and C# by UTF-16 unit, so a supplementary-plane
        # character would silently shift every recorded parse-error offset. No pattern in the
        # suite has one today (measured 2026-08-30); if that changes, the fixture needs an
        # offset conversion rather than a quiet wrong answer.
        raise SystemExit(f"pattern above U+FFFF, so error offsets no longer port: {astral[0]!r}")

    if len(recorder.default_versions) != 1:
        raise SystemExit(
            f"DEFAULT_VERSION varied during the run ({sorted(recorder.default_versions)}); the "
            "fixture records it once, so the recorder would have to become per-row"
        )

    return {
        "comment": FIXTURE_COMMENT,
        "regexVersion": regex.__version__,
        "upstreamCommit": _upstream_commit(),
        "defaultVersion": recorder.default_versions.pop(),
        "compiles": [recorder.compiles[k] for k in sorted(recorder.compiles)],
        "errors": [recorder.errors[k] for k in sorted(recorder.errors)],
        "templates": [recorder.templates[k] for k in sorted(recorder.templates)],
    }


def _upstream_commit() -> str:
    return subprocess.run(
        ["git", "-C", str(REPO_ROOT / "upstream"), "rev-parse", "HEAD"],
        capture_output=True, text=True, check=True,
    ).stdout.strip()


def write(fixture: dict, path: Path) -> None:
    """Writes the fixture as one JSON document with exactly one line per row.

    Not ``indent=2``: that puts each of the 29k bytecode integers on its own line, and a slice
    that changes one pattern's output would show up as a diff of unreadable single-integer
    lines. One compact line per row means the diff names the rows that changed, which is what
    the fixture is for. ASCII only, so the bytes are the same on every platform and a lone
    surrogate in some future upstream pattern cannot fail the encode.
    """
    sections = ("compiles", "errors", "templates")
    lines = ["{"]
    for key, value in fixture.items():
        if key in sections:
            continue
        lines.append(f"  {json.dumps(key)}: {json.dumps(value)},")
    for i, section in enumerate(sections):
        rows = fixture[section]
        lines.append(f"  {json.dumps(section)}: [")
        for j, row in enumerate(rows):
            comma = "," if j < len(rows) - 1 else ""
            lines.append("    " + json.dumps(row, sort_keys=False) + comma)
        lines.append("  ]" + ("," if i < len(sections) - 1 else ""))
    lines.append("}")

    path.parent.mkdir(parents=True, exist_ok=True)
    # newline='' so Windows does not turn these into CRLF; the fixture is diffed byte for byte.
    with open(path, "w", encoding="ascii", newline="") as f:
        f.write("\n".join(lines) + "\n")


_DETERMINISM_RUNS = 4


def _verify_determinism() -> int:
    """Records several times in fresh interpreters and requires byte-identical output.

    Fresh *processes*, not just different ``PYTHONHASHSEED`` values, and that distinction is the
    point: the order that leaks is an iteration order over sets of parse nodes, and
    ``RegexBase.__hash__`` hashes ``self._key`` whose first element is the node's class *object*,
    whose hash is its address. The seed does not perturb that, the address-space layout does, so
    two runs are two samples whatever the seeds. Measured 2026-08-30 with
    ``_install_set_order_patches`` disabled: a single pair of runs disagreed on 51 rows, another
    pair on 82, and eight runs between them touched 111 of the 1547; with the patches in place,
    all eight agreed exactly.

    So this is a smoke test, not a proof: a leak affecting one row could survive a few runs. Four
    runs cost about three seconds and would have caught every leak found so far on the first
    comparison. If it fails, there is another set-to-list conversion beyond the ones
    ``_install_set_order_patches`` and ``_canonical_kwargs`` handle; find it rather than
    canonicalising the output.
    """
    with tempfile.TemporaryDirectory() as tmp:
        outputs = []
        for run in range(_DETERMINISM_RUNS):
            env = dict(os.environ, PYTHONHASHSEED=str(run * 7919 + 1), PYTHONDONTWRITEBYTECODE="1")
            out = Path(tmp) / f"corpus-{run}.json"
            subprocess.run([sys.executable, str(Path(__file__).resolve()), "--output", str(out)],
                           env=env, check=True, stdout=subprocess.DEVNULL)
            outputs.append(out)

        for other in outputs[1:]:
            if not filecmp.cmp(outputs[0], other, shallow=False):
                print("the recorder is not deterministic: two runs produced different fixtures",
                      file=sys.stderr)
                return 1

    print(f"deterministic: {_DETERMINISM_RUNS} runs produced byte-identical fixtures")
    return 0


def _comparable(fixture: dict) -> dict:
    """A fixture with the one field that legitimately varies by environment removed.

    ``regexVersion`` only. The oracle CI job builds ``regex`` from the pinned submodule
    (2026.8.12) while this machine has 2026.7.19 installed, so comparing it would report "stale"
    about a fixture whose every recorded value is correct. Everything else - including
    ``upstreamCommit``, the pin the whole oracle rests on - has to match.
    """
    return {key: value for key, value in fixture.items() if key != "regexVersion"}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check", action="store_true",
                        help="diff against the committed fixture instead of overwriting it")
    parser.add_argument("--verify-determinism", action="store_true",
                        help=f"record {_DETERMINISM_RUNS} times in fresh interpreters and require "
                             "byte-identical output")
    args = parser.parse_args(argv)

    if args.verify_determinism:
        return _verify_determinism()

    fixture = record()

    counts = (f"{len(fixture['compiles'])} compiles, {len(fixture['errors'])} errors, "
              f"{len(fixture['templates'])} templates, "
              f"{sum(len(row['code']) for row in fixture['compiles'])} bytecode integers")

    if args.check:
        if not args.output.exists():
            print(f"no committed fixture at {args.output}", file=sys.stderr)
            return 1

        committed = json.loads(args.output.read_text(encoding="ascii"))
        if _comparable(fixture) != _comparable(committed):
            print(f"the committed fixture is stale; regenerate it ({counts})", file=sys.stderr)
            return 1

        # The comparison above is over parsed JSON, so it would accept a reformatted or
        # hand-edited file that happens to carry the same values. Round-tripping the committed
        # data back through write() and diffing the bytes says it really is what the recorder
        # emits - which the file's own header claims.
        with tempfile.TemporaryDirectory() as tmp:
            round_tripped = Path(tmp) / "corpus.json"
            write(committed, round_tripped)
            if not filecmp.cmp(round_tripped, args.output, shallow=False):
                print("the committed fixture has been reformatted or hand-edited; regenerate it",
                      file=sys.stderr)
                return 1

        if committed["regexVersion"] != fixture["regexVersion"]:
            print(f"note: recorded against regex {committed['regexVersion']}, checked against "
                  f"{fixture['regexVersion']}; the two agree on every recorded value")

        print(f"fixture is current: {counts}")
        return 0

    write(fixture, args.output)
    print(f"wrote {args.output}: {counts}")
    return 0


if __name__ == "__main__":
    os.environ.setdefault("PYTHONDONTWRITEBYTECODE", "1")
    sys.exit(main())
