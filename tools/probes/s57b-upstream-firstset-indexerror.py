r"""Why upstream cannot COMPILE `(?r)^\u0130\ufb00` under IGNORECASE|FULLCASE.

Two rows of the 6000-row gate - seed 20260920 rows 81232 and 87091 - are not divergences at all:
upstream raises `IndexError: tuple index out of range` while compiling a pattern this port accepts,
so there is no upstream answer to compare with. This probe minimises both to the same four
ingredients and then shows the defect in upstream's own parse tree, so the ledger entry rests on
upstream's data structures rather than on a traceback.

    python tools/probes/s57b-upstream-firstset-indexerror.py

THE MECHANISM, in upstream's own source (regex 2026.9.10, submodule 7dd71c15):

  1. `Sequence._fix_full_casefold` (upstream/regex/_regex_core.py:3636-3667) splits a literal into
     the chunks that need full case folding and the chunks that do not. It finds those chunks in
     the FOLDED text - `string = fold_case(FULL_CASE_FOLDING, ...)` at :3641 - and then slices the
     UNFOLDED `characters` with the offsets it found (:3657, :3660, :3665). The two index spaces are
     the same length only while no character expands on folding. U+0130 folds to two characters and
     U+FB00 folds to two, so after `\u0130\ufb00` the folded string is four characters long and the
     unfolded tuple holds two: the second chunk's slice `characters[2:4]` is EMPTY.
  2. `Sequence._flush_characters` (:3617-3626) turns each chunk into a node. A chunk of one
     character becomes a `Character`; every other chunk, the empty one included, becomes a `String`.
  3. Compilation asks for a first set only when the pattern has no simple start
     (upstream/regex/_main.py:643), which is to say when it begins with something like `^` or `\A`.
  4. `Sequence.get_firstset` (:3576-3587) REVERSES its items under `(?r)` and asks the last one
     first. That is the empty `String`, and `String.get_firstset` (:4030-4036) indexes
     `self.characters[-1]`.

Forward, the anchor is asked first, answers `None`, and the walk stops at the non-empty `String`
before it ever reaches the empty one - which is why only the reversed spelling raises.
"""

import regex
import regex._regex as _regex
from regex._regex_core import FULL_CASE_FOLDING, FULLIGNORECASE, Sequence

IC = regex.IGNORECASE | regex.FULLCASE

# One ingredient removed per row, so each line says what its own absence does.
GRID = [
    (r"(?r)^\u0130\ufb00", "(?r)^\u0130\ufb00", IC, "all four ingredients"),
    (r"^\u0130\ufb00", "^\u0130\ufb00", IC, "forward"),
    (r"(?r)\u0130\ufb00", "(?r)\u0130\ufb00", IC, "no anchor, so no first set is computed"),
    (r"(?r)^\u0130\ufb00", "(?r)^\u0130\ufb00", regex.IGNORECASE, "simple folding"),
    (r"(?r)\A\u0130\ufb00", "(?r)\\A\u0130\ufb00", IC, "\\A instead of ^"),
    (r"(?r)^\ufb00\ufb00", "(?r)^\ufb00\ufb00", IC, "both expand by one, so the offsets still line up"),
    (r"(?r)^a\ufb00", "(?r)^a\ufb00", IC, "only the second expands"),
    (r"(?r)^\u0130", "(?r)^\u0130", IC, "one character, so a Character and not a String"),
]

# The two gate rows, verbatim out of `tools/probes/s57b-gate-rows.jsonl`. The `flags` field is the
# integer the recorder hands to `regex.compile`, so it is repeated here as an integer rather than
# spelled out in flag names that would have to be trusted.
DRAWN = [
    (20260920, 81232, "(?r)^\u00df(?<=\u0130\u0130\ufb00)(?:(?<=\\p{Nd})\u0130)?$", 0x4002),
    (20260920, 87091, "(?r)^(?:(?(?<![\ufb01])\ufb01[^a]|(?(?<!(?:\ufb01|\\p{Ll})+)\\p{Nd}*?|))\u0130|\u0130)\ufb01$", 0x400A),
]


def _chunks_of(folded: str) -> list[tuple[int, int]]:
    """`_fix_full_casefold`'s own chunk search, repeated here so its offsets can be printed."""
    expanded = [_regex.fold_case(FULL_CASE_FOLDING, c) for c in _regex.get_expand_on_folding()]
    chunks = []
    for e in expanded:
        found = folded.find(e)
        while found >= 0:
            chunks.append((found, found + len(e)))
            found = folded.find(e, found + 1)
    return chunks


def verdict(pattern: str, flags: int) -> str:
    try:
        regex.compile(pattern, flags)
    except Exception as e:                      # noqa: BLE001 - the exception IS the answer
        return f"{type(e).__name__}: {e}"
    return "compiles"


def main() -> int:
    print("regex", regex.__version__)

    print("\nTHE TWO GATE ROWS, as drawn")
    for seed, number, pattern, flags in DRAWN:
        print(f"  seed {seed} row {number}  {verdict(pattern, flags)}")
        print(f"    {ascii(pattern)}")

    print("\nTHE INGREDIENTS, one removed per row")
    for shown, pattern, flags, note in GRID:
        print(f"  {shown:<24} {verdict(pattern, flags):<38} {note}")

    print("\nTHE EMPTY `String`, in upstream's own splitter")
    for characters, shown in [
        ([0x0130, 0xFB00], r"\u0130\ufb00"),
        ([0xFB00, 0xFB00], r"\ufb00\ufb00"),
        ([0x0061, 0xFB00], r"a\ufb00"),
    ]:
        folded = _regex.fold_case(FULL_CASE_FOLDING, "".join(chr(c) for c in characters)).lower()
        literals = Sequence._fix_full_casefold(characters)
        shapes = ", ".join(
            f"{len(lit.characters)} char{'' if len(lit.characters) == 1 else 's'}"
            + (" FULLIGNORECASE" if lit.case_flags == FULLIGNORECASE else " IGNORECASE")
            for lit in literals
        )
        print(f"  {shown:<16} unfolded {len(characters)} -> folded {ascii(folded)} ({len(folded)})")
        print(f"  {'':<16} chunks found at {Sequence._merge_chunks(_chunks_of(folded))} -> {shapes}")

    # The crash is one symptom of the mis-sliced chunk. The other is silent: the first chunk keeps
    # both characters and is given SIMPLE folding, so the character that needed the full fold no
    # longer has it, and the forward spelling - which compiles - fails to match the folded text.
    print("\nTHE SILENT HALF, forward, where nothing raises")
    for pattern, subject, note in [
        ("^İﬀ", "i̇ff", "the full fold of both characters"),
        ("^ﬀ", "ff", "one character, so no chunk is mis-sliced"),
        ("^aﬀ", "aff", "two chunks, both non-empty"),
        ("^ﬀﬀ", "ffff", "one chunk covering both"),
    ]:
        found = regex.compile(pattern, IC).match(subject)
        print(f"  {ascii(pattern):<22} over {ascii(subject):<12} {'matches' if found else 'NO MATCH':<9} {note}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
