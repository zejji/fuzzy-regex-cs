"""D51 survey: one pattern and subject per line, answered by upstream, PCRE2 and Perl.

    python tools/probes/d51-call-capture-survey.py ROWS.tsv

Each line of ROWS.tsv is PATTERN<TAB>SUBJECT[<TAB>OPERATION], OPERATION being search (the
default), match or fullmatch; a line starting with # is skipped. Upstream (`regex`) prints each
group's span and capture list, PCRE2 (pip `pcre2`) and Perl (d51-perl-match.pl) each group's span.
Every engine call is bounded: `regex` by its own timeout, Perl by a subprocess timeout, and PCRE2
is only ever given the short hand-written witnesses this probe is for.
Used for the witnesses in tests/FuzzyRegex.Tests/Gaps/Engine/CallCaptureTests.cs (2026-09-30).
"""

import subprocess
import sys
from pathlib import Path

import pcre2
import regex

PERL = Path(__file__).with_name("d51-perl-match.pl")
TIMEOUT = 5


def upstream(pattern, subject, op):
    m = getattr(regex, op)(pattern, subject, timeout=TIMEOUT)
    if m is None:
        return "None"
    return f"{m.span()} " + " ".join(f"{m.span(i)}{m.captures(i)}" for i in range(1, len(m.regs)))


def pcre(pattern, subject, op):
    m = getattr(pcre2, op)(pattern, subject)
    if m is None:
        return "None"
    spans = []
    i = 1
    while True:
        try:
            spans.append(str(m.span(i)))
        except Exception:
            break
        i += 1
    return f"{m.span()} " + " ".join(spans)


def perl(pattern, subject, op):
    # Hex-encoded, because the shell's argument parsing mangles backslashes.
    args = [pattern.encode().hex(), subject.encode().hex(), op.encode().hex()]
    done = subprocess.run(["perl", str(PERL), *args], capture_output=True, text=True, timeout=TIMEOUT)
    return done.stdout.strip() or "ERR " + done.stderr.strip()[:80]


def main():
    for line in open(sys.argv[1], encoding="utf-8"):
        line = line.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        parts = line.split("\t")
        pattern, subject = parts[0], parts[1]
        op = parts[2] if len(parts) > 2 and parts[2] else "search"
        print(f"{pattern!r} {op} {subject!r}")
        for name, engine in (("upstream", upstream), ("pcre2", pcre), ("perl", perl)):
            try:
                answer = engine(pattern, subject, op)
            except Exception as e:  # an engine's error is its answer
                answer = f"ERR {type(e).__name__}"
            print(f"  {name + ':':9} {answer}")


if __name__ == "__main__":
    main()
