# DRAFT - NOT FILED

Ledger entry 84. Written on 2026-10-15 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A tail guard is kept when a later backreference reads a group, so the leftmost match is missed

**Version:** regex 2026.9.10, CPython 3.14, Windows x64.

```python
>>> import regex, re
>>> regex.search(r'(b?)(?:a*|b)*\1c', 'bac')
<regex.Match object; span=(1, 3), match='ac'>
>>> re.search(r'(b?)(?:a*|b)*\1c', 'bac')
<re.Match object; span=(0, 3), match='bac'>
>>> regex.match(r'(b?)(?:a*|b)*\1c', 'bac')
>>>
```

A match at 0 exists: group 1 takes nothing, the outer repeat takes `b` then `a`, `\1` matches the
empty string and `c` matches. `re`, PCRE2 10.47, Perl 5.42, Java 26, JavaScript (V8) and .NET's
System.Text.RegularExpressions all answer (0, 3). `regex.match` finds nothing at all.

The cause seems to be the tail guard of the inner `a*`. `add_repeat_guards` (`_regex.c:23273`)
leaves a tail guard on when the tail contains no group reference, but its walk stops at the
enclosing repeat's END_GREEDY_REPEAT (`:23334-23337`), so for `a*` it sees only the end of the outer
repeat's body and never the `\1` after it (`:23421` sets RE_STATUS_TAIL). The tail of `a*` really
runs on through the outer repeat into `\1c`, which reads group 1.

So the attempt at 0 first takes group 1 = 'b', lets `a*` take the `a`, fails on `\1c` at 2 and,
backtracking `a*` (`:15855`), guards a*'s tail at 2. Backtracking then gives group 1 = '' and the
outer repeat takes `b` at 0 and enters `a*` at 1; `a*` would take the `a` and continue at 2, but
the guard check (`:13346`) refuses position 2, so `a*` matches empty and the path fails. The guard
recorded a failure that held only while group 1 was 'b'.

Other shapes with the same cause:

```python
>>> regex.search(r'(b?)(?:a*|b)*\1c', 'bbac').span()        # re: (0, 4)
(2, 4)
>>> regex.search(r'(b?)(?:(?:a*|b)*x)*\1c', 'baxc').span()  # re: (0, 4)
(1, 4)
>>> regex.search(r'(?:(b)|)(?:a*|b)*(?(1)z|c)', 'bac').span()  # re: (0, 3)
(1, 3)
>>> regex.search(r'(b*)(?:a*|c|(b))*\1c', 'baaaac').span()  # re: (0, 6)
(1, 6)
```

The last of these is the form it was found in. A group-exists condition reads the captures the way
a backreference does. The answer is right when `a*` writes no tail guard: `a+` (no backtrack entry
at the minimum), `a*+`, `(?>a*)`, or `(b??)`, which tries group 1 = '' first.

The same walk has a second blind spot: it marks a bounded repeat's body LIMITED without walking it
(`:23383-23386`), so a reference inside `(?:...)?` or `{m,n}` is not seen either, and no enclosing
repeat is needed. A reference earlier in the enclosing body, reached on the next iteration, is
missed the same way as one after it:

```python
>>> regex.search(r'(a*)a*(?:d\1)?c', 'adc').span()          # re: (0, 3)
(1, 3)
>>> regex.search(r'(a*)a*(?:d\1)*c', 'adc').span()          # unbounded: walked, right
(0, 3)
>>> regex.search(r'(a?)(?:(?:\1d)?a*|b)*c', 'adc').span()   # re: (0, 3)
(1, 3)
```

A possible fix, in `add_repeat_guards`: give the END_GREEDY_REPEAT and END_LAZY_REPEAT cases the
reference status of the enclosing repeat's tail and body (the body runs again), and look for a
reference inside a bounded body even though its repeat status stays LIMITED. Then a tail guard is
off wherever the rest of the match can read a group. That costs the guards' protection on nested
repeats before a reference: `(x?)(?:a*a*)*\1c$` over 'a' * 12 + 'cx' is fast today and
exponential without the tail guards, as it is in `re`. A fix that keeps the protection keys each
such tail guard list on the values of the groups the rest of the match can read, so a failure
recorded under one value never blocks a path under another. A group set inside the loop must be
keyed also on the position it opened at: `.?(a*)(?:b\1)?c` over 'aabaac' gives (2,6) for (0,6)
with a key on the captured text alone.

---

## Notes (not part of the report)

- Found 2026-10-15 by the `perf/memo-br` builder's differential (rows in `FailureMemoTests` on that
  branch). Minimised, surveyed and traced 2026-10-15 (`.scratch/s1016/d85/`).
- Fixed in the port on `fix/d85-c9` with keyed tail guards (LEDGER.md entry 84, final addendum);
  the witnesses are `Gaps/Engine/NestedTailGuardTests`.
- The line numbers were read; the mechanism was shown in the port by a guard trace and by
  switching the guards off (LEDGER.md entry 84).
- The title should change if the bounded-body half turns out to be a separate report; it shares
  the function and the fix, so it is kept here.
