# DRAFT - NOT FILED

Ledger entry 54. Written on 2026-09-29 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## A group call inside a lookaround leaves a capture behind when the lookaround's body is thrown away

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(a)(?:(?!.(?1))|.)+?b', 'aaab').spans(1)
[(0, 1), (2, 3)]
>>> regex.search(r'(?P<x>a)(?:(?!.(?P<x>a))|.)+?b', 'aaab').spans(1)
[(0, 1)]
>>> regex.search(r'(a)(?:(?!.()(?1))|.)+?b', 'aaab').spans(1)
[(0, 1)]
>>> regex.search(r'(a)(?:(?=.(?1))x|.)+?b', 'aaab').spans(1)
[(0, 1), (2, 3)]
>>> regex.search(r'(a)(?:(?<!(?1)).|.)+?b', 'aaab').spans(1)
[(0, 1), (0, 1), (1, 2)]
```

In the first pattern, at position 1 the negative lookahead's body matches: `.` takes the 'a' at 1
and the call `(?1)` matches the 'a' at 2, adding (2, 3) to group 1's capture list. That makes the
negative lookahead fail, so the match takes the other branch, and nothing the body did should
survive. The entry (2, 3) does. The second pattern writes the same group directly inside the same
lookahead, and its entry is discarded, as it should be. The third pattern is the first with an empty
group added to the lookahead's body, and that alone makes the entry go away. The fourth and fifth
show the same for a positive lookahead the match backtracks past and for a negative lookbehind.

The cause is in `_regex.c`. `RE_OP_LOOKAROUND` saves the captures only when the lookaround node
carries `RE_STATUS_HAS_GROUPS` (line 13772), and restores them from that save when a negative body
has matched (line 12997) or when backtracking passes a positive lookaround (line 15668).
`build_LOOKAROUND` sets the flag when its body's `has_groups` is set (line 25022), and only
`build_GROUP` sets `has_groups` (line 24811). `build_GROUP_CALL` sets `RE_STATUS_HAS_GROUPS` on its
own node (line 24850), where nothing reads it, but not `args->has_groups`, so a body that only
calls a group is never saved, although the call writes the called group's captures.

A possible fix, in `build_GROUP_CALL`:

```c
    node->status |= RE_STATUS_HAS_GROUPS;
    node->status |= RE_STATUS_HAS_REPEATS;
    args->has_groups = TRUE;
```

Then the lookaround saves the captures for such a body, and all five patterns above give group 1
`[(0, 1)]`. It only affects capture lists: `group()`, spans and fuzzy counts are unchanged, since a
call already restores the caller's current capture on return. Atomic groups and conditionals save
the captures unconditionally, so they are not affected.

PCRE2 10.47 and Perl 5.42 give the same match and group 1 span for every pattern above. They keep
no capture lists and restore a called group on return, so they cannot show the extra entry. What
every engine agrees on is that a failed assertion keeps nothing it captured:
`(?:(?!(a)b)\w|a)` over 'ab' and `(?:(?=(a))x|a)` over 'a' leave group 1 unset in regex, Python
`re`, PCRE2, Perl and JavaScript, and .NET 10, which does keep capture lists, gives group 1 an
empty one for both.
