"""S59: what upstream answers for a named list that gains a member between two calls.

The port's static conveniences that take a `namedLists` dictionary BYPASS the pattern cache, and
`PatternCacheTests.A_caller_s_named_lists_dictionary_bypasses_the_cache` proves the bypass by
mutating the dictionary between two calls with the same pattern text and watching the answer
change. These are the two answers that test expects, taken from upstream rather than from us.

Run:  python tools/probes/s59-named-lists-rebind.py
"""

import regex

print("regex", regex.__version__)
print("no-match:", regex.match(r"\L<words>", "beta", words=["alpha"]))
print("match:   ", regex.match(r"\L<words>", "beta", words=["alpha", "beta"]))
