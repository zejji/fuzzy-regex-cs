"""Which parts of the engine at HEAD has no Stryker run ever mutated?

A queue chunk mutates character windows of a file as that file stood when the chunk ran. The
engine queue took three days, and the engine moved under it (S56b's compile budget, S60's
required-string prefilter), so a window's evidence belongs to the revision the chunk used, not to
HEAD. Every chunk report embeds the source it mutated, so the two can be compared directly:
anything in HEAD that no mutated revision contained has never been mutation-tested.

Prints ready-to-paste `tools/stryker-queue.json` entries for the uncovered regions, in HEAD's
character offsets, line-aligned and split into windows of about the same size as the rest of the
queue.

    python tools/stryker-topup-windows.py
"""

import difflib
import json
import os
import subprocess
from collections import defaultdict

ROOT = os.path.join("TestResults", "stryker")
PLACEHOLDER = "File ignored by mutate filter"
WINDOW = 2000          # characters, matching the queue's existing windows
CONTEXT_LINES = 3      # a changed line's neighbours compile with it; mutate them together
CHUNK_WINDOWS = 6      # windows per chunk, matching the queue


def head_source(rel):
    return subprocess.run(
        ["git", "cat-file", "blob", f"HEAD:{rel}"], capture_output=True, check=True,
    ).stdout.decode("utf-8")


def mutated_sources():
    """{repo-relative path: [source, ...]} - every distinct revision the queue actually mutated.

    Every chunk counts, not just the engine ones: FuzzyRegex.cs belongs to the `api` chunk and
    Substitution.cs to `substitution`, and those ran on their own day's revision too. The chunks
    S55 discarded as invalid (per-test coverage analysis mislabelled survivors as Timeout) are
    left out, so a region they touched still counts as needing a re-run.
    """
    seen = defaultdict(dict)
    invalid = ("api-pertest-4runners", "api-failed")
    for chunk in sorted(os.listdir(ROOT)):
        report = os.path.join(ROOT, chunk, "reports", "mutation-report.json")
        if chunk.startswith(invalid) or not os.path.exists(report):
            continue
        with open(report, encoding="utf-8") as fh:
            files = json.load(fh)["files"]
        for fname, info in files.items():
            src = info.get("source", "")
            if src == PLACEHOLDER:
                continue
            rel = "src/FuzzyRegex/" + fname.replace("\\", "/").split("/src/FuzzyRegex/")[-1]
            seen[rel][hash(src)] = src
    return {rel: list(revs.values()) for rel, revs in seen.items()}


def uncovered_lines(old, new):
    """Indices (0-based, into new) of lines in new that old did not contain at that place."""
    a, b = old.splitlines(keepends=True), new.splitlines(keepends=True)
    out = set()
    for tag, _i1, _i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if tag == "equal":
            continue
        for j in range(max(0, j1 - CONTEXT_LINES), min(len(b), j2 + CONTEXT_LINES)):
            out.add(j)
    return out


def line_offsets(text):
    offsets, pos = [], 0
    for line in text.splitlines(keepends=True):
        offsets.append((pos, pos + len(line)))
        pos += len(line)
    return offsets


def windows_for(rel, revisions):
    new = head_source(rel)
    # Uncovered at HEAD = uncovered against EVERY revision that was mutated: a line one run
    # missed may have been mutated by a run on another revision.
    missing = set.intersection(*[uncovered_lines(old, new) for old in revisions])
    if not missing:
        return [], 0
    offsets = line_offsets(new)
    runs, current = [], None
    for j in sorted(missing):
        if current and j == current[1] + 1:
            current = (current[0], j)
        else:
            if current:
                runs.append(current)
            current = (j, j)
    if current:
        runs.append(current)

    out = []
    for first, last in runs:
        start, end = offsets[first][0], offsets[last][1]
        while end - start > WINDOW:
            split = start + WINDOW
            # line-align the split
            for lo, hi in offsets:
                if lo <= split < hi:
                    split = hi
                    break
            out.append((start, split))
            start = split
        out.append((start, end))
    return out, len(missing)


if __name__ == "__main__":
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    entries = []
    for rel, revisions in sorted(mutated_sources().items()):
        wins, lines = windows_for(rel, revisions)
        if not wins:
            print(f"{rel}: fully covered by {len(revisions)} mutated revision(s)")
            continue
        short = rel.split("src/FuzzyRegex/")[-1]
        print(f"{rel}: {lines} lines never mutated, {len(wins)} window(s), "
              f"{sum(e - s for s, e in wins)} chars")
        entries.extend(f"{short}{{{s}..{e}}}" for s, e in wins)

    print(f"\n{len(entries)} windows total\n")
    chunks = [entries[i:i + CHUNK_WINDOWS] for i in range(0, len(entries), CHUNK_WINDOWS)]
    for n, group in enumerate(chunks, start=1):
        print(json.dumps({"chunk": f"engine-topup-{n:02d}", "mutate": group}, indent=2))
