# D63: a call to a group that carries a fuzzy constraint

Status: design note, 2026-10-03. No code changed. The defect as filed rests on a wrong premise, and
what is left is a semantic question with two defensible answers, so it goes to the owner for a ruling
before anything is built.

## What D63 says

`(?<o>(?<i>a)b){e<=1}(?&o)` over 'abx' is (0, 2) with two deletions in the port and no match
upstream (regex 2026.9.10). The register reads this as "the call inherits the budget of the section
it calls", and takes `(?<o>(?<i>a)b){e<=1}(?:(?:a)b)`, which neither engine matches, as the
written-out form.

## Finding 1: the witness is not about calls

The 'abx' split reproduces with no call at all:

| pattern over 'ab' | port | upstream |
|---|---|---|
| `(ab){e<=1}(?1)` | (0, 2), 2 deletions | None |
| `(ab){e<=1}(?:ab){e<=1}` | (0, 2), 2 deletions | None |
| `(?:ab){d<=1}(?:xb){d<=1}` | (0, 2), 2 deletions | None |
| `(?:ay){d<=1}(?:ab){d<=1}` | (0, 2), 2 deletions | (0, 2), 2 deletions |

The match is 'a' with 'b' deleted in the first section, then 'a' deleted and 'b' matched in the
second. Each section has its own budget of one, so the match is legal. Upstream misses it only when
the first section can also match exactly, because upstream never goes back to delete an item that
matched exactly. That is the existing divergence "A fuzzy item that matched exactly can still be
deleted" (DIVERGENCES.md:92, ledgers 42 and 44, F17), where the port is right.

## Finding 2: both engines make the constraint part of the group

- Upstream's parser moves a constraint written after a group inside it:
  `apply_constraint`, `upstream/regex/_regex_core.py:596-600` ("If a group is marked as fuzzy
  then put all of the fuzzy part in the group"). The port's `ParseFunctions.ApplyConstraint`
  (`src/FuzzyRegex/Parsing/ParseFunctions.cs:603`) does the same. So `(ab){e<=1}` parses to
  `Group(Fuzzy(ab))`, the same tree as `((?:ab){e<=1})`.
- `Group.FixGroups` (`Nodes.cs:4017-4020`) records the group as defined outside any section, so a
  call from outside a section has the group's own features and needs no copy
  (`CheckGroupFeatures`, `ParseFunctions.cs:3244-3252`). The call runs the group as written, FUZZY
  node included. The compiled code for the witness is `CALL_REF 0, GROUP 1, FUZZY ..., END,
  GROUP_CALL 0`.
- Each time a FUZZY node is entered its counts start from zero (`_regex.c:13136-13144`, ported
  as-is), so each call gets a fresh budget, the same as a second copy of the section written at the
  call site.

So in both engines `(?1)` here behaves as `(?:ab){e<=1}` written at the call site. That is the D40
rule ("a call answers as the group written out there") applied to the parsed tree.

A group that really is inside a section, as in `(?:x(ab)){e<=1}(?1)`, already gets an exact copy
when called from outside, and both engines answer as `(?:ab)` written out (shape `inside` below).

## Measurement

Debug build, `tools/probes/port-probe.cs` at 2 s per row, against upstream with `timeout=2`. There
are 10 shapes, each with no flag and with `(?r)`, `(?b)` and `(?e)`, 120 random subjects over
'abcx' of length 0-7, and partial on and off. Each row has three forms: the call, written-out form
A (constraint goes with the group) and written-out form B (constraint stays outside it).

| shape | call | port: call = A | port: call = B | upstream: call = A | upstream: call = B |
|---|---|---|---|---|---|
| suffix | `(ab){e<=1}(?1)` | 600/600 | 186 | 600/600 | 184 |
| suffix-named | the D63 witness | 600/600 | 186 | 600/600 | 184 |
| suffix-s | `(abc){s<=1}x(?1)` | 600/600 | 596 | 600/600 | 591 |
| call-first | `(?1)x(ab){e<=1}` | 600/600 | 354 | 600/600 | 354 |
| inside | `(?:x(ab)){e<=1}(?1)` | 600/600 | 600 | 600/600 | 600 |
| explicit-in | `((?:ab){e<=1})(?1)` | 600/600 | 600 | 600/600 | 600 |
| other-sect | `(ab){e<=1}(?:x(?1)){s<=1}` | 600/600 | 563 | 600/600 | 546 |
| nested | `(ab){e<=1}(c(?1)){i<=1}(?2)` | 600/600 | 575 | 600/600 | 558 |

On all 4,800 call rows the port differs from upstream on exactly the keys where it already differs
on written-out form A, with 0 mismatches. Those differences are the exact-deletion divergence above,
nested sections bounded by the outer budget (ledger 39, DIVERGENCES.md:89), and fuzzy counts on
partial matches. None of them comes from the call.

Recursion: `(a(?1)?b){e<=1}` and `(?:a(?R)?b){e<=1}` raise MemoryError upstream on every row,
because each level opens a new section with a fresh budget and nothing bounds the depth. In the port
each level's section is nested in the caller's, so ledger 39 bounds the whole match by the outer
budget (every row has at most one error) and the re-entry guard (DIVERGENCES.md:67) bounds the depth.

## The open question: does `{...}` after a group belong to the group?

**A. It belongs to the group (status quo, both engines).** `(?1)` calls `(?:ab){e<=1}`, with its own
budget, and nested inside an enclosing section when called from one.
- For: both engines agree on every row measured. Upstream chose this on purpose (the
  `apply_constraint` comment). `(ab){e<=1}` and `((?:ab){e<=1})` stay one tree, as they are in
  both engines. Recursion stays bounded in the port. No code change.
- Against: it reads differently from a quantifier. In `(ab){2}(?1)`, PCRE2 and Perl call `ab` once,
  not twice, and a user may read `{e<=1}` the same way. The README says only "the fuzziness of a
  regex item is specified ... after the item", which does not settle it.

**B. It applies from outside, like a quantifier.** `(?1)` calls `ab` exactly. This is D63 as filed.
- For: it matches how quantifiers work, and the README's wording reads that way.
- Against: it diverges from upstream on about 70% of the `suffix` rows (414 of 600), for example
  `(ab){e<=1}(?1)` over 'abaX' becomes no match instead of (0, 4) with one substitution. The parser
  would have to remember where the constraint was written, so that `(ab){e<=1}` and
  `((?:ab){e<=1})` compile differently even though both engines treat them as one tree. Calls need
  an exact copy that strips the group's top-level FUZZY. No other engine has both fuzzy matching and
  group calls, so no survey can back it.

**Recommendation: A.** Close D63 as not a defect. Record the rule in DECISIONS.md: a constraint
written after a group is part of the group, and a call answers as `(?:body){constraint}` written at
the call site. Pin it with tests: the witness pinned as an instance of DIVERGENCES.md:92, plus one
control per shape above. B changes hundreds of upstream-agreeing answers on an analogy to
quantifiers that upstream rejected in its parser, and no engine survey can support it.

## Siblings checked

- Nested calls, calls from one section into another, and calls written before the group: shapes
  `nested`, `other-sect` and `call-first`, where call = A on every row in both engines.
- `(?R)` and recursion: shapes `recurse` and `whole`. The port is bounded. Upstream raises
  MemoryError (already covered by DIVERGENCES.md:67 and ledger 39).
- BESTMATCH, ENHANCEMATCH, reversed and partial: the `(?b)`, `(?e)` and `(?r)` prefixes and partial
  rows of every shape, where call = A on every row.
- Captures: the group's capture list is not compared above. The port drops a call's captures
  on return (DIVERGENCES.md:102). That is unchanged by either option.
- Fuzzy counts: compared on every row above.
- Performance: no change under A. Under B, a call-free pattern would pay nothing (the copy is made
  only for a call), but every call to a constrained group would compile one extra copy.
