---
slice: S52c
phase: 6
title: Metamorphic invariants - the recorder checks upstream's answers against each other on every wave row, so an upstream bug is a candidate the moment it contradicts itself
delivers: []
---

# S52c - Metamorphic invariants over upstream, and over the port

**Per-sitting notes: `docs/plan/slices/notes/S52c-sittings.md`. The list itself, once written, lives
in `docs/ORACLE-INVARIANTS.md`.**

The differential oracle detects DISAGREEMENT between the port and upstream. It cannot see a bug the
port inherited line for line, because then the two agree; ledger 11's mechanisms C and D were
exactly that ("both engines agree, so the oracle is blind"). Every serious inherited bug in the
ledger was found instead by upstream contradicting ITSELF: `search` empty where the same pattern's
anchored `match` finds something (entry 13); a documented "best match" flag turning a match into
none (13); `fuzzy_counts` and `fuzzy_changes` describing different edits (11); `$` and `\Z`
reading different bounds (5). Those checks were applied by hand, one row at a time, when a session
happened to notice. This slice makes them automatic on every row, against upstream's own answers
with no port involved, and then against the port's. Owner request 2026-09-15 (spec amendment 25).

## Scope

1. **The invariant list, written first and argued for.** Each is a property of the regex language
   or of upstream's documented contract, so it holds for features no other engine has. Starting
   list, to be pruned or extended with a reason per change:
   - `search(p, s)` returns a match at position `i` if and only if `match(p, s, pos=i)` returns
     the same span and groups; `search` returns none only if `match` fails at every position it
     could have reported (checked at the positions the recorder already has).
   - `fullmatch` agrees with a `match` whose span is the whole `[pos, endpos)`.
   - `finditer`, `findall` and `sub` with a count agree on the same sequence of matches; `subn`'s
     count equals the number of `finditer` matches within the same limit.
   - Every group span lies inside the match span, `captures(g)` lists the texts of `spans(g)`, and
     `lastindex`/`lastgroup` name a group that participated.
   - A partial match exists wherever a full one does, and a full match is never partial.
   - Loosening a fuzzy budget never loses a match the tighter budget found (issue 564's property),
     and never increases the reported error count for the same span.
   - `fuzzy_counts` equals the tally of `fuzzy_changes` by kind (S47's property, generalised).
   - Under `BESTMATCH` the reported error count is at most the plain fuzzy match's.
   - `(?r)` on the reversed subject mirrors the forward answer for a pattern the recorder can
     reverse mechanically (literals, classes, fixed repeats).
   - `(?V0)` and `(?V1)` agree outside nested sets and full case-folding.
   - `split` re-joined with its separators reconstructs the subject; `split` with `maxsplit`
     agrees with the first matches of `finditer`.
   - `escape(s)` compiled matches exactly `s`.
2. **The checker in the recorder** (`tools/record-oracle.py`): for each wave row, evaluate every
   invariant whose inputs the row already has, or derive them with one extra upstream call
   (anchored `match` at the reported position, the flagless twin, the reversed subject). A
   violation is written into the row as `selfContradiction: [<invariant id>, ...]`, and the wave
   summary counts them. Per-row timeout unchanged; the extra calls run in the same child.
3. **Triage, not inflation.** Every violation on a 2000-row three-seed wave is minimised, classified
   and, if real, becomes a ledger entry under amendment 16's standard - self-contradiction is
   strong evidence but the mechanism is still owed. A violation that is the invariant being wrong
   (a documented exception) prunes the list with the documentation quoted. Expect entries 5, 11
   and 13 to be re-found: that is the calibration.
4. **The same checker on the port's answers**, in `OracleComparer`, so a port bug that both engines
   share is visible too. A port violation is a port bug unless the same row violates upstream and
   the ledger explains why the port follows.
5. **Fuzzy second engine.** The `tre` PyPI binding (0.8.0) fails to build on Windows (measured
   2026-09-15: "Failed to build 'tre' when getting requirements to build wheel"). Try one
   alternative within thirty minutes - `agrep` from Git for Windows or a prebuilt TRE - and record
   in `docs/plan/OPERATIONS.md` what works; if nothing does, say so and keep the invariants as the
   fuzzy instrument.
6. **Docs.** `docs/VERIFICATION.md` gains a paragraph: the oracle proves agreement, the invariants
   prove consistency, and only the two together say anything about correctness where the port
   inherits upstream's answer.
7. **Gate row 104366 is this slice's worked example, handed over by S52 sitting 11.** A reversed
   partial over an EMPTY slice at the end of the subject: `(?r)\xdfﬁ(.*?)\b` asked as
   `match(subject, 2, 2, partial=True)` over `'ﬁı'`, upstream answering a zero-width
   partial at (2, 2) and this port answering no match. It is not pinned and must not be pinned by
   agreement, because **NEITHER ENGINE IS SELF-CONSISTENT**: over the 33 cells of
   `tools/probes/{upstream,port}-reversed-partial-ignores-the-slice-start.*` the two agree on 23 and
   differ on 10, and the 10 split both ways. That is exactly the shape this slice exists to catch
   automatically - a row where the oracle's "do they agree" question has no useful answer and only a
   metamorphic invariant ("a partial call may not deny what the same engine's greedy and lazy
   spellings of one pattern both allow") separates right from wrong. Ledger entry 24 holds the
   reading; the owner's ruling on whether the reversed run-out should read `slice_start` rather than
   `text_start` is still open, and this slice should NOT wait for it - the invariant is worth having
   either way, and if it fires on both engines that is the finding.

## Verification

- Waves at three seeds, 2000 rows, with the checker on: violations counted, every one triaged in
  the notes; the three known entries re-found or their absence explained.
- Ratchet GREEN; blind review (hunt: an invariant that is false for a documented reason and would
  flood the ledger; a checker that calls upstream in a way that itself faults, see entry 9), then
  the verifier pass re-running the wave summary.

## Done when

- [ ] Invariant list committed with a reason per item; checker in the recorder and the comparer.
      *(Sitting 1: list DONE - `docs/ORACLE-INVARIANTS.md`, 22 invariants, a calibration per item.
      Checker not started.)*
- [ ] Three-seed wave run with violations triaged; ledger entries for real ones.
- [x] Fuzzy second engine tried and the result recorded in OPERATIONS. *(Sitting 1: none reachable.
      `agrep` and Perl `String::Approx` are absent; `fuzzysearch` is blocked by permissions, not
      proven to fail. The invariants are the fuzzy instrument, as this scope item provides for.)*
- [ ] Gate row 104366 run through the invariants, and what they say about it recorded.
      *(Sitting 1: the invariant that handles it is `greedy-lazy-existence-agree`; running the row
      through it belongs to the checker sitting.)*
- [ ] VERIFICATION.md paragraph; DECISIONS entry.
- [ ] Ratchet GREEN, blind review, verifier, commit.
