"""Does `Pattern.flags` carry POSIX when the flag was written INLINE as `(?p)`?

The whole S46 POSIX design rests on the recorder being able to tell, from the compiled pattern
alone, that a row is POSIX - so that `_describe_match` can omit `fuzzy_changes`, whose getter
faults on a POSIX fuzzy match that spent an error (ledger entry 9). The recorder already reads
`compiled.flags & _REVERSE_FLAG` for an inline `(?r)`, so the shape is precedented, but a
precedent is not a measurement.

Prints the POSIX bit for each spelling. Run it, do not reason about it.
"""

import regex


CASES = [
    ("(?p)abc", 0),
    ("abc", regex.POSIX),
    ("abc", 0),
    # Inline, but not at the start of the pattern: upstream hoists a global flag wherever it is
    # written, and a recorder that pattern-matched on a `(?p)` prefix would miss this one.
    ("a(?p)bc", 0),
    # Inline inside a group, which upstream also treats as global for a global-only flag.
    ("(?:(?p)abc)", 0),
    # A fuzzy POSIX row, the actual shape at issue.
    ("(?p)(?:abc){e<=1}", 0),
    ("(?:abc){e<=1}", 0),
]


def main() -> None:
    print(f"regex {regex.__version__}, POSIX bit = 0x{regex.POSIX:x}")
    for pattern, flags in CASES:
        try:
            compiled = regex.compile(pattern, flags)
        except Exception as e:  # noqa: BLE001 - a rejected spelling is an answer too
            print(f"{pattern!r:24} flags=0x{flags:x}  -> rejected: {type(e).__name__}: {e}")
            continue

        posix = bool(compiled.flags & regex.POSIX)
        print(f"{pattern!r:24} flags=0x{flags:x}  -> compiled.flags=0x{compiled.flags:x}  POSIX={posix}")


if __name__ == "__main__":
    main()
