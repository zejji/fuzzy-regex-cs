#!/usr/bin/env python
r"""Upstream's fuzzy CHANGE list survives a failed search attempt where its COUNTS do not.

`basic_match`'s `start_match` clears `state->fuzzy_counts` (upstream/src/_regex.c:11790-11792) and
leaves `state->fuzzy_changes` alone. `match_fuzzy_changes` (:20522) then reports the first
`sum(fuzzy_counts)` entries of that list, so a change recorded by an attempt that was abandoned does
not merely sit there unread - it DISPLACES the change the winning attempt recorded.

The result is an answer that contradicts itself, which is what makes this a defect rather than a
convention: upstream says one deletion was used and names a substitution.

    python tools/probes/upstream-fuzzy-restart-leak.py

Measured 2026-09-13 on regex 2026.7.19 and re-run on 2026.9.10 (S40a). This port reproduces the two
`qab` rows exactly - they are pinned by
FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one,
which S38 added - and reaches the same leak on shapes upstream's prefilter keeps it away from. See
ledger entry 11.
"""

from __future__ import annotations

import regex

TIMEOUT = 5.0


def contradicts(match) -> bool:
    """Whether the per-kind counts and the per-kind change lists disagree."""
    counts = match.fuzzy_counts
    changes = match.fuzzy_changes
    return tuple(counts) != tuple(len(part) for part in changes)


def show(pattern: str, subject: str, note: str = "") -> None:
    compiled = regex.compile(pattern)
    match = compiled.search(subject, timeout=TIMEOUT)
    if match is None:
        print(f"  {pattern!r:<44} {subject!r:<8} -> None  {note}")
        return

    flag = "  <-- CONTRADICTS ITS OWN COUNTS" if contradicts(match) else ""
    print(
        f"  {pattern!r:<44} {subject!r:<8} -> span={match.span()} "
        f"counts={match.fuzzy_counts} changes={match.fuzzy_changes}{flag}  {note}"
    )


def main() -> None:
    print("regex", regex.__version__)

    print("-- the leak, upstream's own answer --")
    show(r"(?:[ab][bc](*PRUNE)[wx]){e<=2}", "qab", "# S38 pinned this")
    show(r"(?:[ab](*SKIP)[bc][wx]){e<=2}", "qab", "# and this")

    print("-- the shapes where upstream's prefilter keeps it away from the leak --")
    print("   (`$` has a search_start_* twin, so upstream makes ONE attempt where this port makes four)")
    show(r"(?<=(?:[ab][cd]){e<=1})$", "axc", "# port: dels=[1]")
    show(r"(?<=(?:abc){e<=2})$", "ac", "# port: dels=[1,2]")

    print("-- the controls: a literal tail, so both engines walk every position and both agree --")
    show(r"(?<=(?:[ab][cd]){e<=1})q", "axcq")
    show(r"(?<=(?:ab){e<=1})d", "axqayd")
    show(r"(?:ab){e<=1}d", "axbaxd", "# no lookaround at all")


if __name__ == "__main__":
    main()
