# DRAFT - NOT FILED

Ledger entry 37. Written on 2026-09-26 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## With a scoped IGNORECASE, the required-string search loses full case folding

**Version:** regex 2026.9.10, CPython 3.14.7, Windows x64.

```python
>>> import regex
>>> regex.search(r'(?V1)(?i:ss)', '\xdf')
>>> regex.search(r'(?V1)(?i)ss', '\xdf')
<regex.Match object; span=(0, 1), match='ß'>
>>> regex.search(r'(?V1)(?i:ss)|q', '\xdf')
<regex.Match object; span=(0, 1), match='ß'>
```

The first search returns None. The pattern's own code is right: the third pattern has no required
string, and it matches. `(?V1)(?i:ss)x` over `'\xdfx'`, `(?V1)(?i:fi)` over `'\ufb01'` and
`(?V0)(?f)(?i:ss)` over `'\xdf'` return None too. Perl 5.42.3 matches all of them.

**Cause.** `_get_required_string` (`_main.py:602`) takes IGNORECASE and FULLCASE from the literal's
own case flags, and they reach `_regex.compile` as `req_flags`. `pattern_new` (`_regex.c:26125`)
then removes FULLCASE whenever the pattern's global flags lack IGNORECASE, which they do when the
only IGNORECASE is scoped. So the required-string search looks for 'ss' with simple folding, never
finds 'ß', and rejects the subject before the matcher runs.

**Suggested fix.** Decide FULLCASE from the literal's case flags alone, since they already say
whether the literal ignores case. With that, the searches above agree with the global
`(?V1)(?i)ss`.
`tools/probes/upstream-scoped-ignorecase-required-string.py` in the reporter's repository prints
every case above.
