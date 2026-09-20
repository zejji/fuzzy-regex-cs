"""Upstream's answer for every example in the demo's sidebar.

The sidebar (demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json) is the guided tour, so each entry is
a claim about what this library does. The expected values the browser checks assert therefore come
from a real upstream run rather than from the port's own output (port-slice rule, owner
2026-09-15). This probe reads the shipped JSON, so the examples have exactly one source.

    python tools/probes/demo-examples-expectations.py

Run 2026-09-18 against regex 2026.9.10; the output is quoted in wwwroot/checks.html.

Spans are UTF-16 code unit offsets, not Python's code point offsets, because the page slices a
JavaScript string with them. Every subject here is BMP-only, where the two agree, and the probe
fails loudly if one ever is not rather than printing an offset the page cannot use.
"""

import json
import pathlib
import sys

import regex

# One example is Greek, and a Windows console is cp1252, so printing it is an encoding error rather
# than an answer unless this is said out loud.
sys.stdout.reconfigure(encoding="utf-8")

EXAMPLES = pathlib.Path("demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json")

# FuzzyRegexOptions member names, which is what the demo's flag box takes, to upstream's flags.
FLAGS = {
    "IgnoreCase": regex.IGNORECASE,
    "Multiline": regex.MULTILINE,
    "Singleline": regex.DOTALL,
    "IgnorePatternWhitespace": regex.VERBOSE,
    "Unicode": regex.UNICODE,
    "Ascii": regex.ASCII,
    "Word": regex.WORD,
    "RightToLeft": regex.REVERSE,
    "BestMatch": regex.BESTMATCH,
    "EnhanceMatch": regex.ENHANCEMATCH,
    "FullCase": regex.FULLCASE,
    "Posix": regex.POSIX,
    "Version0": regex.VERSION0,
    "Version1": regex.VERSION1,
}


def parse_flags(text):
    # VERSION1 unless the example asks for VERSION0, because the port defaults to Version1 where
    # upstream defaults to VERSION0 (a deliberate divergence, docs/DIVERGENCES.md). Without this
    # the probe answers a different question from the one the page asks: measured 2026-09-18,
    # `[\w--[\d]]+` on "abc123def456" is 0 matches under upstream's default and 2 under the port's.
    tokens = text.replace(",", " ").replace("|", " ").split()
    flags = regex.VERSION0 if "Version0" in tokens else regex.VERSION1
    for token in tokens:
        flags |= FLAGS[token]
    return flags


def main():
    examples = json.loads(EXAMPLES.read_text(encoding="utf-8"))
    print(f"regex {regex.__version__}, {len(examples)} examples from {EXAMPLES}")

    for example in examples:
        # The timeout sample is deliberately exponential and there is nothing upstream to compare:
        # the demo's two-second budget is its own contract, not a parity claim. Running it here
        # would hang this probe, which has no timeout of its own.
        if example.get("key") == "timeout":
            print()
            print(f"{example['title']}  pattern={example['pattern']!r} - SKIPPED, no upstream expectation")
            print("  the demo's MatchTimeout is its own contract; the expected answer is its error sentence")
            continue

        subject = example["subject"]
        if not subject.isascii() and len(subject.encode("utf-16-le")) // 2 != len(subject):
            raise SystemExit(f"{example['title']}: subject is not BMP-only, so the spans below would not be UTF-16 offsets")

        # A named-list block is written the way the demo's own box takes it: one list per line, as
        # `name: word, word`. Upstream takes the same lists as keyword arguments.
        lists = {}
        for line in example.get("namedLists", "").splitlines():
            if not line.strip():
                continue
            name, _, words = line.partition(":")
            lists[name.strip()] = [word.strip() for word in words.split(",") if word.strip()]

        compiled = regex.compile(example["pattern"], parse_flags(example["flags"]), **lists)
        mode = example.get("mode", "")
        print()
        print(f"{example['title']}  pattern={example['pattern']!r} flags={example['flags']!r} subject={subject!r}")
        if lists:
            print(f"  namedLists={json.dumps(lists)}")

        if mode == "partial":
            # The demo's partial mode asks the single-match entry point, because upstream's
            # scanning functions take no `partial` argument and neither do the port's.
            one = compiled.search(subject, partial=True)
            matches = [one] if one else []
            print(f"  mode=partial partial={bool(one and one.partial)}")
        elif mode == "replace":
            print(f"  mode=replace replacement={example['replacement']!r}")
            print(f"  replaced={compiled.sub(example['replacement'], subject)!r}")
            matches = list(compiled.finditer(subject))
        else:
            matches = list(compiled.finditer(subject))

        spans = [[m.start(), m.end() - m.start()] for m in matches]
        print(f"  matches={len(matches)} spans={json.dumps(spans)}")
        for index, match in enumerate(matches[:3]):
            counts = match.fuzzy_counts  # (substitutions, insertions, deletions)
            print(f"  [{index}] text={match.group()!r} counts(sub,ins,del)={counts}")
            for number, name in sorted((n, g) for g, n in (compiled.groupindex or {}).items()):
                captures = [[start, end - start] for start, end in match.spans(number)]
                print(f"        group {number} ({name}): participated={match.span(number) != (-1, -1)} captures={json.dumps(captures)}")


if __name__ == "__main__":
    main()
