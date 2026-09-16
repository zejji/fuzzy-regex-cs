# Metamorphic invariants over upstream, and over the port

The differential oracle answers one question: *do the two engines agree?* It is blind to a bug the
port inherited line for line, because then they do agree. Every serious inherited bug in
`docs/plan/upstream-reports/LEDGER.md` was found instead by **upstream contradicting itself** - one
spelling of a question answered differently from another spelling of the same question - and every
one of those contradictions was spotted by hand, one row at a time, when a session happened to look.

This file is the list of those contradictions stated as properties, so the recorder can check them
on every wave row with no port involved, and the comparer can check them on the port's answers too.
Written for S52c scope item 1 (owner request 2026-09-15, design spec amendment 25).

**The list is an instrument, not a claim about correctness.** An invariant firing means one of two
things: the engine is wrong, or the invariant is. Scope item 3 triages every violation and prunes
the list with the documentation quoted when the invariant is the thing that was wrong. That is the
calibration, and it is expected to remove items from this file.

## How to read an entry

Each invariant carries four things, and the fourth is the one that matters:

- **ID** - the string written into a row's `selfContradiction: [...]` field. Stable; never reused.
- **Statement** - what must hold, in terms of calls the recorder can make.
- **Ground** - why it must hold: a property of the regex language, or a quoted line of upstream's
  own documentation. Not "it seems reasonable".
- **Calibration** - the ledger entries this invariant would have caught **automatically**. This is
  the evidence that the invariant is worth its cost: an invariant that would have caught nothing
  anyone has ever found is a hypothesis, and is marked as one.

And two costs:

- **Cost** - `FREE` (decided from the row the recorder already has), `+1`, `+2` (extra upstream
  calls in the same child process, per scope item 2).
- **Tier** - `SHIP` (first checker), `DEFER` (real but not yet worth its call budget), `REJECTED`
  (false as the slice stated it; kept here with the reason so it is not re-proposed).

**A `SHIP` invariant with no calibration is not a mistake.** Several are near-free and total
(`escape-round-trip`, `split-rejoins`); a cheap check that has never fired is cheap evidence, and
the ledger only records what somebody thought to look for.

---

## What is implemented, and what it found

**Seven invariants are in the checker, all of them at zero extra upstream calls.** Four are read off
the recorded row (`_structural_violations` in `tools/record-oracle.py`) and three off the ablation
twins `_CONTROLS` already records, so the first checker asks upstream nothing it was not already
being asked. That is not only a budget decision: ledger entry 9 is upstream CRASHING on a question
asked a particular way, so a checker that asks its own questions can be the thing that faults and
can take a whole wave with it. The `+1` and `+2` tiers below are a later sitting's.

The same four structural checks run on THIS PORT's answers in `SelfConsistency.Check`
(`tests/FuzzyRegex.OracleTests/SelfConsistency.cs`), swept over every row of every wave by
`OracleWaveTests.Our_own_answers_never_contradict_themselves`.

**Measured over 126,240 rows - 22 generators at 2,000 rows a generator, at seeds 7, 4242 and
20260916 (S52c, 2026-09-16).** `eligible` is how many rows the invariant had something to decide on,
and it is the number that makes a zero readable: 0 of 34,721 is cheap evidence, 0 of 0 is a check
that is not running, and the wave summary prints those two identically.
`tools/probes/invariant-triage.py` is what produces this table.

| Invariant | Cost | Eligible | Fired | What the firings were |
| --- | --- | ---: | ---: | --- |
| `fuzzy-counts-match-changes` | FREE | 1,774 | **6** | ledger 11, re-found automatically |
| `bestmatch-no-worse` | FREE (twin) | 2,123 | **2** | ledger 12's shape, re-found automatically |
| `group-spans-inside-match` | FREE | 14,142 | **2** | the `overlapped-skip-stale-slice` family |
| `captures-are-the-texts-of-spans` | FREE | 34,721 | 0 | - |
| `lastindex-participated` | FREE | 11,684 | 0 | - |
| `posix-chooses-among-flagless-answers` | FREE (twin) | 5,562 | 0 | - |
| `no-fault-where-a-twin-answers` | FREE (twin) | 1,800 | 0 | - |

**The calibration the slice asked for came back.** Ledger 11 and ledger 12 were both re-found with
no hand-written probe and nobody looking, on rows no earlier sitting had seen, which is the claim
this file existed to make good. Ledger 13 was not re-found and ledger 9, 16 and 23 were not either -
see "Ledger entries no invariant in this file reaches" at the foot, which now also covers those.

**Ten candidates in 126,240 rows is the number that matters as much as the six.** An instrument that
fires on one row in 12,624 is one a session can triage in an afternoon; the failure this file was
most at risk of was an invariant that is false for a documented reason and floods the ledger, which
is what the blind review is briefed to hunt for. Three narrowings below are why it did not happen,
and every one of them was forced by a measurement rather than foreseen.

---

## A. Doors onto the same answer

Upstream implements `search`, `match`, `fullmatch`, `finditer` and the overlapped scan over one
engine. Where two of them can be made to ask the same question, they must give the same answer.
Three ledger entries are exactly this shape.

### `search-anchored-agree` - SHIP, cost +1

**Statement.** If `search(p, s, pos, endpos)` reports a match at `i` with span `(i, j)` and group
spans `G`, then `match(p, s, i, endpos)` reports span `(i, j)` and group spans `G`.

**Ground.** `search` is defined as trying `match` at successive start positions and reporting the
first that succeeds. Anchoring at the position it reported asks the same question of the same
engine over the same text object.

**Calibration.** Ledger 3 (a reversed `search` reports a partial that the same pattern's `match` and
`fullmatch` deny), 5 (an overlapped scan with `(*SKIP)` contradicts the same pattern's own `match`),
13 (`BESTMATCH` loses a partial match that the same pattern's own `match` still finds).

**Known confounder, and the checker must record it rather than assume it away.** Moving `pos` from
the search's start to `i` changes what the *slice start* is, and a pattern element that reads a
bound - `\A`, `^`, `\G`, `\K`, a lookbehind, or a reversed run-out - may legitimately read it
differently. Whether upstream's reversed partial reads `slice_start` or `text_start` is the open
question in ledger 24 and is not settled here. **On a violation the checker must also record the
answer for the pattern with those elements absent**, so triage can tell "the two doors disagree"
from "the two doors were asked different questions". A violation where the confounder-free twin
also violates is the strong finding.

### `search-none-anchored-none` - SHIP, cost +1 per position sampled

**Statement.** If `search(p, s, pos, endpos)` reports no match, then `match(p, s, k, endpos)` reports
no match for every `k` the recorder tries in `[pos, endpos]`.

**Ground.** The converse of the above, and the same definition.

**Calibration.** Ledger 3 (the direction that was actually wrong there was a *surplus* partial, but
the missing-match direction is the one a `BESTMATCH`-style loss takes - see 12 and 13).

**Cost control.** Do not sweep every position. Sample the positions the row already names - the
slice bounds, the reported spans of the row's other doors, and the ablation twins' starts - which is
where a contradiction has ever actually been found.

### `fullmatch-is-the-whole-slice-match` - SHIP, cost +1

**Statement.** `fullmatch(p, s, pos, endpos)` succeeds with span `(pos, endpos)` if and only if
`match(p, s, pos, endpos)` can report span `(pos, endpos)`.

**Ground.** `fullmatch` is documented as a `match` required to consume the whole slice.

**Calibration.** Ledger 4 (a reversed `fullmatch` of a repeat fails on a slice that is exactly the
match), 16 (whose third line is upstream's own `fullmatch` finding the `(0, 9)` its scan dropped).

### `overlapped-superset` - SHIP, cost +1

**Statement.** Every match of the non-overlapped scan appears, with the same span, among the
overlapped scan's matches.

**Ground.** `overlapped=True` adds start positions to the scan. It cannot remove an answer the
narrower scan reached.

**Calibration.** Ledger 5, 16.

### `finditer-findall-sub-agree` - SHIP, cost +2

**Statement.** Over one call's limits: `findall`'s items are the group texts of `finditer`'s
matches, in order; `subn`'s count equals the number of `finditer` matches within the same `count`
limit; `sub`'s output equals the subject with the template applied at exactly those spans.

**Ground.** Documented as one scan with different renderings of it.

**Calibration.** None directly - but **ledger 23 was caught only because the recorder writes the
replacement count down**: both of its answers rendered identical text and the count was the single
field that differed. This invariant is that field promoted to a rule.

---

## B. The structure of one match object

Free: decided from the row the recorder already has, with no extra call. The cheapest tier in the
file, and entry 11 is proof that upstream can contradict itself inside a single match object.

### `fuzzy-counts-match-changes` - SHIP, cost FREE

**Statement.** `fuzzy_counts` equals the tally of `fuzzy_changes` by kind: substitutions, insertions
and deletions counted from the change positions equal the three numbers reported.

**Ground.** The two attributes are documented as two views of the same edit set.

**Calibration.** Ledger 11 (a fuzzy match reports change positions that contradict its own change
counts) - the entry with seven distinct doors found so far, all of one shape. S47 checked this
property by hand; this generalises it to every row.

**AND THE CALIBRATION CAME BACK: 6 firings of 1,774 eligible rows**, at all three seeds, on six rows
no earlier sitting had seen, with nobody looking. Every one is ledger 11's shape - counts and
positions describing different edit scripts, such as `counts=[0, 1, 0]` beside a single
SUBSTITUTION position. Four of the six are PARTIAL matches, which is a concentration worth the next
sitting's attention and is not something any of entry 11's seven hand-found doors pointed at. Listed
in `docs/plan/slices/notes/S52c-sittings.md`; the entry is not re-opened for them because it is
already open and already reported.

### `captures-are-the-texts-of-spans` - SHIP, cost FREE

**Statement.** For every group `g`: `len(captures(g)) == len(spans(g))`, and `captures(g)[k]` is the
subject text of `spans(g)[k]`.

**Ground.** Documented as the text and the position of the same capture list.

**Calibration.** None. 0 firings of 34,721 eligible rows - the widest reach in the file.

**AND IT IS NOT THE VACUOUS CHECK IT READS AS**, which is worth saying because the obvious objection
is that upstream computes one from the other. It does, and that is the point: `match_spans` and
`match_get_captures_by_index` walk the same `group->captures[i]` array (`upstream/src/_regex.c:19115`
and `:19174`), but the captures arm renders each span through
`get_slice(self->substring, start - self->substring_offset, ...)`. Nothing else this recorder reads
exercises that offset at all, so `spans()` cannot see a wrong `substring_offset` and this can.

**No POSIX guard, unlike `fuzzyChanges`, and that is measured.** Reading `fuzzy_changes` on a POSIX
fuzzy match that spent an error kills the interpreter (entry 9) and
`tools/probes/upstream-posix-fuzzy-safe-attributes.py` never asked about `captures`. Section 6 of
`tools/probes/upstream-free-tier-invariant-grounds.py` now does, over three POSIX fuzzy patterns
including a repeated capturing group, and every read returns normally.

### `lastindex-participated` - SHIP (narrowed), cost FREE

**Statement.** If `lastindex` is not `None` it names a group whose span is not `(-1, -1)`.

**Ground.** Documented as the last group that *participated* in the match.

**The second limb this entry used to carry - "and `lastgroup` names the same group" - IS FALSE, and
is pruned.** Measured in `tools/probes/upstream-free-tier-invariant-grounds.py` section 1 (regex
2026.9.10, 2026-09-16): `(?P<x>a)(b)` over `'ab'` answers `lastindex=2` and `lastgroup='x'`, and
group 2 has no name at all. `lastgroup` is the last NAMED group, which is what `_describe_match`'s
own comment in `tools/record-oracle.py` has said since S14 - so the first draft of this file
contradicted the recorder, and the recorder was right. The limb is not recoverable from a row in any
case: a recorded row carries group NUMBERS and no names.

**Calibration.** None. 0 firings of 11,684 eligible rows.

### `group-spans-inside-match` - SHIP (narrowed), cost FREE

**Statement.** Every participating group's span, and every one of its captures, lies within the
match span - **for rows whose pattern contains no `\K` and no lookaround**.

**Ground.** A group matches a part of what the match consumed.

**Both narrowings are MEASURED**, in `tools/probes/upstream-free-tier-invariant-grounds.py` sections
3 and 5 (regex 2026.9.10, 2026-09-16):

- **`\K`** resets the reported match start, so a group before it legitimately lies outside the span:
  `(a)\Kb` over `'ab'` is match `(1, 2)` with group 1 at `(0, 1)`. `\K` is in the wave's alphabet -
  ledger 23's own pattern is `...\g<1>\K$`.
- **A lookaround** consumes nothing, so a group inside one matches text the match never covered:
  `a(?=(b))` over `'ab'` is match `(0, 1)` with group 1 at `(1, 2)`, and `(?:(?=(bc))b)` over `'bc'`
  is match `(0, 1)` with group 1 at `(0, 2)`. **This file's first draft did not have this
  narrowing**, and without it the invariant would have fired on a large share of the `lookaround`,
  `interactions` and `conditionals` generators - the flood the blind review is briefed to hunt for,
  caught by running the probe before the checker rather than by reading the wave afterwards.

**A GROUP CALL IS NOT NARROWED AROUND, and this file's first draft said it was.** The same probe's
section 3 measured both spellings - `(a)b(?1)` and `(?P<g>a)b(?&g)` over `'aba'` - and each records
group 1 at `(0, 1)`, INSIDE the match. A call re-enters a group; it does not move the span reported
for it. The claim was plausible and wrong, and excluding those rows would have been a hole for no
reason. What the ledger actually records under a group call (entry 8, and the `group-call-direction`
family in `run-oracle.ps1`) is a call inside a LOOKAROUND, which the narrowing above already covers.

**Calibration.** 2 firings of 14,142 eligible rows, both of them the `overlapped-skip-stale-slice`
family already in `ExpectedDivergences.cs` - an overlapped `(*SKIP)` scan reporting a capture
outside its own match. Not predicted as a calibration for this invariant and found anyway, which is
the first evidence that the free tier reaches a family nobody aimed it at.

---

## C. Partial matching

Upstream's documentation defines a partial match precisely, and the definition is testable. Four
ledger entries live here.

### `partial-prefix-closure` - SHIP, cost +1

**Statement.** If a full match exists with span `(a, b)`, then for `m` strictly between `a` and `b`,
the same call over the slice `[a, m)` with `partial=True` reports a match.

**Ground.** `upstream/docs/Features.html` line 576: *"A partial match is one that matches up to the
end of string, but that string has been truncated and you want to know whether a complete match
could be possible if the string had not been truncated."* The truncation of a known complete match
is the one case where the answer is not a matter of judgement.

**Calibration.** Ledger 21 (upstream issue 589 - a partial `fullmatch` denies a prefix whose
completion exists; the maintainer considers it correct, this repo disagrees on his own documentation
and on PCRE2 10.47), 15 (`(*SKIP)` blocks the one repeat retreat a partial match needs - a
regression in 2026.9.10), 2.

**Cost control.** One `m` per row, chosen by the row's seed, not every `m`.

### `full-implies-partial` - SHIP, cost +1

**Statement.** If a call succeeds without `partial`, the same call with `partial=True` reports a
match with the same span, and reports it as complete rather than partial.

**Ground.** `partial=True` is documented as *adding* partial matches to the answers, not replacing
them.

**Calibration.** Ledger 13, 12.

### `partial-flag-and-full-answer-agree` - SHIP, cost +1

**Statement.** A match reported with `match.partial` true must not be reportable as a complete match
over the same slice by the same call without `partial`.

**Ground.** The two are defined as disjoint outcomes of one call.

**Calibration.** Ledger 3.

---

## D. Fuzzy matching

### `fuzzy-budget-monotone-existence` - SHIP, cost +1

**Statement.** Loosening a fuzzy budget - raising an `e`, `s`, `i` or `d` bound, or widening
`{e<=n}` to `{e<=n+1}` - never loses a match the tighter budget found.

**Ground.** The looser budget's acceptable-edit set is a superset of the tighter one's. Every match
of the tighter is a match of the looser.

**Calibration.** Ledger 20 (upstream issue 564, which is this property named as a bug report), 19
(`\m` before a fuzzy section does not match at position 0 - upstream issue 563).

### `bestmatch-no-worse` - SHIP, cost +1

**Statement.** Under `BESTMATCH`, (a) a match exists wherever the plain fuzzy call finds one, and
(b) the reported error count at the same start is no greater than the plain call's.

**Ground.** `BESTMATCH` is documented as choosing the best among the matches the ordinary engine can
already make. It is a selection, so it cannot select nothing from a non-empty set, and it cannot
select something worse than what it chose from.

**Calibration.** Ledger 12 (`BESTMATCH` loses a match that plain fuzzy matching finds, when the best
fit ends in a trailing insertion - fixed in this port by S46), 13 (the same in the partial
direction).

**Note the strengthening.** The slice's starting list had only limb (b), the error count. Limb (a),
existence, is what entries 12 and 13 actually are.

**COST FREE IN PRACTICE, not `+1`.** `_CONTROLS` in `tools/record-oracle.py` has recorded
`bestmatchFreeOutcome` since S48b, so the flagless twin is already on the row and this invariant
asks upstream nothing. The same is true of `posix-chooses-among-flagless-answers` below, which is
how the first checker reaches ledger 12 and ledger 9's families at no call budget at all.

**CALIBRATION CAME BACK: 2 firings of 2,123 eligible rows, both limb (a).** The clearer of the two
is `(?b)(?fi)(?:(?:[\U0001f600\U0001d518][ab]){e<=1}){s<=1,i<=1,d<=1}` asked as a `fullmatch` over
`'\U0001d518S\U0001f3fb'`: upstream answers NO MATCH, and the same row without `(?b)` matches
`(0, 5)` at a cost of `[1, 1, 0]`. That is ledger 12 exactly - `BESTMATCH` selecting nothing from a
non-empty set - found by machine on a row nobody had looked at.

**ONE NARROWING, forced by the first three-seed wave and guarded in `_self_check`.** Three of the
four firings of that first wave were rows upstream TIMED OUT on: `_matches_of` renders a timeout as
"no matches", the existence limb read that as the flag choosing nothing, and a row where upstream
merely ran out of its ten seconds was filed as `BESTMATCH` losing a match. The checker now compares
only two ANSWERS. The fault case belongs to `no-fault-where-a-twin-answers` and is handled there.

### `enhancematch-no-worse` - SHIP, cost +1

**Statement.** Under `ENHANCEMATCH`, a match exists wherever the plain fuzzy call finds one, and its
error count is no greater.

**Ground.** Documented as an attempt to improve a match it has already found.

**Calibration.** None yet - a hypothesis, marked as one. S41 ported `ENHANCEMATCH` and found no
contradiction by hand; this makes the same check automatic and free once `bestmatch-no-worse`'s
machinery exists.

### `fuzzy-budget-monotone-cost` - REJECTED as stated, SHIP narrowed, cost +1

**Statement as the slice had it.** Loosening a budget "never increases the reported error count for
the same span."

**Why that is false.** Without `BESTMATCH` the engine reports the *first* acceptable match it
reaches, not the cheapest. A looser budget changes the order in which the fuzzy section may spend,
so a more expensive answer for the same span is a legitimate outcome, not a contradiction.

**What ships instead.** The same statement **under `BESTMATCH` only**, where the engine is defined
to report the cheapest. Outside `BESTMATCH` the existence limb above is the whole of the property.

---

## E. Flags defined as choosing among answers that already exist

Two flags in upstream are defined as *selecting* among the flagless engine's matches. That makes
them checkable against the flagless engine with no reference to any other implementation - and it
is the richest vein in the ledger: three entries, three different ways of breaking one contract.

### `posix-chooses-among-flagless-answers` - SHIP, cost +1

**Statement.** Under `POSIX`: (a) every reported match is one the flagless engine can also make at
the same start, and (b) where the flagless engine can make several at one start, the reported span
is the longest.

**Ground.** Quoted in ledger 23: *"`POSIX` is documented as leftmost-longest matching - it chooses
among the matches the ordinary engine can already make."* Limb (a) is "chooses among", limb (b) is
"longest".

**Calibration.** Ledger 16 (a `POSIX` overlapped scan of a `BESTMATCH` fuzzy pattern drops its
longest match - limb (b)), 23 (`POSIX` adds a zero-width match the flagless engine does not make -
limb (a)), 9 (a `POSIX` search of a fuzzy pattern charges a span more errors than its own flagless
engine needs, and crashes the C engine).

**This is the highest-yield invariant in the file** and it is cheap: the recorder already records a
flagless twin for several controls.

**IT FIRED ON NOTHING: 0 of 5,562 eligible rows**, and the sitting that predicted it would re-find
ledger 9, 16 and 23 was wrong about that. Two reasons, and only the first is a limit of this file:

1. **What is implemented is weaker than the statement.** A `search` reports only its FIRST match, so
   a flagless answer at a different start says nothing about whether the flagless engine could also
   match where POSIX did - the checker therefore compares only rows where the two answered at the
   SAME start, and treats a different start as ambiguity rather than as a finding. Ledger 16 is an
   overlapped SCAN dropping its longest match, which this shape does not reach at all.
2. **Ledger 9 is a crash**, which is `no-fault-where-a-twin-answers`'s business and not this one's.

So this remains the highest-yield invariant *by calibration* and has yet to earn it by firing. What
would change that is limb (b) over a scan rather than over one match, which needs the position
sampling `search-none-anchored-none` also wants; that is the strongest single candidate for the
`+1` tier's first sitting.

### `inline-version-equals-flag` - SHIP, cost +1

**Statement.** `(?V0)` at the head of a pattern gives the same answer as compiling that pattern with
`flags=V0`, and likewise `(?V1)` / `V1`, whatever `DEFAULT_VERSION` is set to.

**Ground.** The inline form is documented as the flag.

**Calibration.** Ledger 22 (`(?V0)` does not mean version 0 when `DEFAULT_VERSION` is `VERSION1` -
fixed in this port by S50b).

### `v0-v1-agree-outside-nested-sets-and-full-case-folding` - REJECTED

**Why.** The documented differences between V0 and V1 are not a closed list of two; they cover set
operators, nested sets, the meaning of a bare `[`, full case-folding, and more. An invariant whose
exception list is open-ended cannot separate a bug from an exception, and would spend triage time at
a rate the ledger gives no reason to expect a return on. `inline-version-equals-flag` above is the
part of this idea that has actually caught something, and it ships.

---

## F. Reversal, and the gate row

### `greedy-lazy-existence-agree` - SHIP, cost +2

**Statement.** For a pattern whose quantifier the recorder can respell, the greedy spelling and the
lazy spelling agree on **whether a match exists** at a given start. They may differ on the span;
they may not differ on existence.

**Ground.** Greediness orders the candidate set; it does not change its membership.

**Calibration.** Gate row 104366, handed over by S52 sitting 11 and this slice's worked example
(scope item 7): `(?r)\xdfﬁ(.*?)\b` asked as `match(subject, 2, 2, partial=True)` over `'ﬁı'`.
Upstream answers a zero-width partial at `(2, 2)`; this port answers no match. Over the 33 cells of
`tools/probes/{upstream,port}-reversed-partial-ignores-the-slice-start.*` the two engines agree on
23 and differ on 10, and **the 10 split both ways** - so the oracle's "do they agree" question has
no useful answer on that row, and it must not be pinned by agreement. The property that separates
right from wrong there is exactly this one: a partial call may not deny what the same engine's
greedy and lazy spellings of one pattern both allow. Ledger 24 holds the reading; the owner's
ruling on `slice_start` versus `text_start` is still open and **this invariant does not wait for
it** - if it fires on both engines, that is the finding.

**IT FIRED ON ONE ENGINE, AND THAT IS THE RESULT.** Run over a six-cell grid by
`tools/probes/upstream-gate-row-greedy-lazy.py` and `tools/probes/port-gate-row-greedy-lazy.ps1`
(2026-09-16, regex 2026.9.10 against a Debug build of this port):

| Cell | Upstream lazy / greedy | Port lazy / greedy |
| --- | --- | --- |
| **the gate row** | `(2, 2) partial` / **no match** | no match / no match |
| without the reversal | `(2, 2) partial` / `(2, 2) partial` | `(2, 2) partial` / `(2, 2) partial` |
| **without the `\b`** | `(2, 2) partial` / **no match** | no match / no match |
| without the unmatchable prefix | complete `(2, 2)` / complete | complete `(2, 2)` / complete |
| over the whole subject | `(0, 2) partial` / `(0, 2) partial` | `(0, 2) partial` / `(0, 2) partial` |
| not partial | no match / no match | no match / no match |

**Upstream breaks the invariant on 2 of 6 cells; this port breaks it on 0 of 6.** Greediness orders
the candidate set and cannot change its membership, so upstream answering a zero-width partial to
`(.*?)` and no match to `(.*)` over the same empty slice is upstream contradicting itself - and the
port's "no match", which the differential oracle could only report as a disagreement, is the
self-consistent answer. On every cell where upstream is self-consistent the two engines agree.

**The ablations attribute it.** Removing the `(?r)` makes the invariant hold AND makes both engines
agree; removing the `\b` does neither. So the mechanism is the reversed partial path and not the
boundary: it is upstream's lazy arm reaching a partial its greedy arm does not.

**What this does and does not settle.** It settles that the port is the self-consistent engine on
gate row 104366, which is what scope item 7 asked for and what the oracle's "do they agree" question
could not answer. It does NOT settle ledger 24's open question of whether upstream's reversed
run-out should read `slice_start` or `text_start`; that is the owner's ruling and S52d's slice. No
row is pinned here on the strength of it - the finding is recorded and handed on.

**S52d moved the port's row of that table, and the invariant still holds on 6 of 6 (2026-09-16).**
The owner ruled for `slice_start` and S52d applied it, so the port now answers `(2, 2) partial` to
every one of the first three cells rather than no match:

| Cell | Upstream lazy / greedy | Port lazy / greedy, AFTER S52d |
| --- | --- | --- |
| **the gate row** | `(2, 2) partial` / **no match** | `(2, 2) partial` / `(2, 2) partial` |
| without the reversal | `(2, 2) partial` / `(2, 2) partial` | `(2, 2) partial` / `(2, 2) partial` |
| **without the `\b`** | `(2, 2) partial` / **no match** | `(2, 2) partial` / `(2, 2) partial` |
| without the unmatchable prefix | complete `(2, 2)` / complete | complete `(2, 2)` / complete |
| over the whole subject | `(0, 2) partial` / `(0, 2) partial` | `(0, 2) partial` / `(0, 2) partial` |
| not partial | no match / no match | no match / no match |

**Upstream still breaks it on 2 of 6 and this port still breaks it on 0 of 6** - and the port now
matches upstream's SELF-CONSISTENT arm on both broken cells rather than its other one, which is a
strictly better place to be self-consistent from. Re-run:
`python tools/probes/upstream-gate-row-greedy-lazy.py` and
`pwsh -File tools/probes/port-gate-row-greedy-lazy.ps1` after a Debug build.

### `reverse-mirrors-forward` - DEFER

**Statement as the slice had it.** `(?r)` on the reversed subject mirrors the forward answer for a
pattern the recorder can reverse mechanically.

**Why deferred rather than rejected.** `(?r)` makes the *scan* run right to left; it does not
reverse the pattern's semantics, so "mirror" needs a pattern transformation the recorder would have
to be trusted to get right - and a bug in that transformation is indistinguishable from a bug in the
engine. The transformation is only safe for literals, classes and fixed repeats, which is the
thinnest slice of the alphabet. `greedy-lazy-existence-agree` reaches the same reversed rows for two
extra calls and no transformation, so it goes first. Revisit only if the reversed generators still
have unexplained rows after the first wave.

---

## G. Total identities, near-free

### `split-rejoins` - SHIP, cost +1

**Statement.** `split`'s pieces, re-interleaved with the separator texts `finditer` reports over the
same call, reconstruct the subject exactly.

**Ground.** `split` is defined as the subject cut at those matches.

**Calibration.** None. Near-free, total, and it covers a call the wave otherwise only records.

### `split-maxsplit-prefix` - SHIP, cost +1

**Statement.** `split` with `maxsplit=k` agrees with the first `k` matches of `finditer` over the
same call.

**Ground.** Documented as the same scan, stopped early.

**Calibration.** None. Pairs with `finditer-findall-sub-agree`.

### `escape-round-trip` - SHIP, cost +1

**Statement.** `escape(s)` compiled matches exactly `s`: `fullmatch` succeeds with span
`(0, len(s))`, for every subject the wave generates.

**Ground.** `escape` is documented as producing a pattern that matches its argument literally.

**Calibration.** None. The cheapest check in the file and it runs against every subject the
generators produce, which is the point - it is the one invariant whose input space is the wave's
whole alphabet.

---

## H. Not metamorphic, but the ledger earns them a place

Six ledger entries are not "two answers disagree" but "one door falls over while another answers".
The recorder already runs ablation twins (`pruneOutcome` and the other three controls), so these
cost nothing beyond reading outcomes the row already carries.

### `no-fault-where-a-twin-answers` - SHIP, cost FREE

**Statement.** A call that raises, crashes or is killed by the per-row timeout, where an ablation
twin of the same row answers normally, is a violation.

**Ground.** Not a language property - an operational one. A flag or a verb that is defined as
narrowing or ordering the answer set cannot legitimately turn an answer into a crash.

**Calibration.** Ledger 9 (a `POSIX` search of a fuzzy pattern crashes the C engine), 6
(`IndexError` out of `regex.compile` on a reversed, case-folded pattern), 10 (`(*SKIP)` inside an
atomic group after an optional item loops for ever), 14 (a self-recursive call round a fuzzy section
that can match empty exhausts memory), 18 (a repeated capture group costs hundreds of bytes per
repetition).

**Warning, from ledger 9.** The checker's own extra calls can be the thing that faults. A violation
here must name which call faulted, and the blind review is briefed to hunt for a checker that
crashes upstream by how it asks rather than what it asks. The first checker asks NO extra calls at
all, so it cannot be the thing that faults - which is the strongest form of this warning being
heeded rather than merely noted.

**TWO NARROWINGS, BOTH FORCED BY A WAVE, and this invariant is the one that needed them.** Stated
flat it fired 7 times in 126,240 rows and every one was the invariant being wrong. Both are guarded
in `_self_check` because each is one predicate deep.

1. **A TIMEOUT beside a RANKING flag's twin is not a fault.** Five of the seven were a `(?b)` row
   that spent its whole ten seconds while the same row without `(?b)` answered. `BESTMATCH` is
   documented to do more work - *"By default, fuzzy matching searches for the first match that meets
   the given constraints ... The BESTMATCH flag will make it search for the best match instead"*
   (`upstream/README.rst:592`) - and POSIX's leftmost-longest must see every match at a position
   before picking the longest. Taking either flag away leaves an engine that may stop at the first
   acceptable answer, so the twin finishing is a COST difference, not a contradiction. **The other
   two controls keep the timeout case**, and that is why this is a narrowing rather than "drop
   timeouts": `(?>` to `(?:` and `(*SKIP)` to `(*PRUNE)` both REMOVE pruning, so the twin explores
   at least as much - a row that hangs WITH the pruning construct and finishes without it cannot be
   explained by cost, and that is ledger entry 10 itself.
2. **A substitution twin that REPLACED NOTHING is not an answer.** The remaining two were `subf`
   rows raising `IndexError` while matching - the shape of ledger 6, and not what it was. Measured
   in `tools/probes/upstream-free-tier-invariant-grounds.py` section 7: upstream's `subf` renders
   the template with `str.format` over the GROUP LIST, so `{0[2]}` on a pattern with no group 2 is
   `IndexError: list index out of range`, and `regex.subf('abcdefgh', '{0[2]}...', 'abcdefgh')`
   raises it with no verb, no fuzzy section and no reversal in sight. **The template is only
   rendered where something matched**, which is the whole mechanism: on both rows the twin replaced
   NOTHING, so it never rendered the template and never reached the question. A twin that DID
   replace proves the template is fine for the pattern, and then the row's own raise is a real
   finding again - so the checker tests the replacement count rather than excluding `sub` rows.

**After both: 0 firings of 1,800 eligible rows** (eligible = every row upstream faulted on, which is
the denominator that makes the zero readable; the violation condition is a strict subset of it).

---

## What this list does not do

It does not prove correctness. It proves *self-consistency*, which is a necessary condition and
nothing more: two doors can agree on the same wrong answer, and a specification this port and
upstream both misread would satisfy every item above. The oracle proves agreement between two
implementations; the invariants prove consistency within one. Only the two together say anything
about correctness on a row where the port inherits upstream's answer, and neither says anything at
all about a row where both engines are wrong in the same way. That is what `docs/VERIFICATION.md`'s
paragraph on this file has to say, and why the second engines in `docs/plan/OPERATIONS.md` remain
the third leg.

## What the second engine can and cannot add here

Amendment 16 wants an independent engine beside a claim, and on a FUZZY row upstream is the only
engine on Windows that does approximate matching at all - PCRE2, Perl and .NET each answer `{e<=n}`
with a syntax error. **TRE 0.8.0 in WSL is the second fuzzy engine** (`docs/plan/OPERATIONS.md`),
asked by `tools/probes/tre-fuzzy-check.py`, and what it measured is worth stating plainly because
the number is small:

- **Over the three 2,000-row waves: 10 rows of 126,240 are in TRE's dialect, and TRE CONFIRMED all
  10** - same existence, and a cheapest cost never above what upstream reported.
- **Of this slice's 10 violation rows, TRE could answer NONE**, each with its reason recorded: four
  `fullmatch`, three scans, one `partial`, two carrying flags TRE has no form for.

**That is the honest ceiling, and it is a fact about dialects rather than about TRE.** TRE offers a
SEARCH and nothing else, so every anchored operation is out; its syntax is POSIX ERE, so `\p{...}`,
backreferences, lookarounds, verbs and `\K` are out; and it has no BESTMATCH, no ENHANCEMATCH, no
per-section budget and no `fuzzy_changes`. The generators that produce violations are exactly the
ones - `interactions`, `verbs`, composed `fuzzy` - whose alphabet is furthest from that core.

So on the rows this file is for, **the invariants are the instrument and the second engine is not
available**, which is the reverse of the usual arrangement and is why this file exists. The 10
CONFIRMED rows are not the point; that the probe answers them at all is what says a zero elsewhere
is a dialect gap and not a broken instrument, and `--self-test` is there for the same reason.

**Two of TRE's answers were this probe's own bugs before they were evidence**, which is worth
recording as a method note. The first dialect gate let `(?i)`-prefixed and lazily-quantified
patterns through and TRE refused seven of them; the corrected gate then reported one DIFFERENT row,
which turned out to be the probe defaulting an unnamed error kind to *unbounded* where upstream
documents it as *forbidden* (`upstream/README.rst:561`, "If a certain type of error is specified,
then any type not specified will **not** be permitted"). A second engine's disagreement is a
hypothesis about the harness before it is one about either engine.

## Ledger entries no invariant in this file reaches

Stated plainly so the next sitting does not assume the list is total. Entry 1 (`(*SKIP)` retries
below the position it committed past) and entry 17 (branch reset gives two groups in the same branch
the same number, upstream issue 425) are both engine-internal: no pair of doors onto one question
disagrees, so nothing here fires. Entry 7 (Turkic case-folding rows in the default tables) is a data
bug, found by reading `CaseFolding.txt` against the built table. Entry 8 (a group call inside a
lookaround that runs the other way loses the match, on a path that never enters the call) would need
an ablation invariant - "a branch never taken cannot change the answer" - which is real, and is the
strongest candidate for extending this list once the first wave's triage is done.
