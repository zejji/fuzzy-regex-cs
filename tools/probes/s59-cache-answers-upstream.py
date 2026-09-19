"""The two upstream answers PatternCacheTests asserts that are not cache mechanics.

The cache is this port's own design and owes upstream nothing, but two of that file's
assertions are about MATCHING and COMPILING rather than about the cache, and the
provenance rule (owner, 2026-09-15) says an expected value names the real upstream run it
came from. This prints both. Run: python tools/probes/s59-cache-answers-upstream.py

Recorded 2026-09-19 against regex 2026.9.10.
"""

import regex

print("regex", regex.__version__)

# 1. A literal pattern that does not occur in the subject does not match. The test uses a
#    fresh GUID-suffixed literal so the key cannot already be cached; the shape is this.
pattern = "s59-purged-0123456789abcdef0123456789abcdef"
print("no-match:", regex.search(pattern, "subject"))

# 2. An unclosed group is a compile error, not a pattern that matches nothing.
try:
    regex.compile("(")
    print("compile: NO ERROR")
except regex.error as exc:
    print("compile-error:", type(exc).__name__, "-", exc)
