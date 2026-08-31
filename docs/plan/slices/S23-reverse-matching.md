---
slice: S23
phase: 3
title: Reverse matching - the (?r) flag and every REV opcode variant
delivers: [right-to-left]
---

# S23 - Reverse matching: the (?r) flag and every REV variant

Needs S22 (the REV set includes IGN_REV and FLD_REV combinations, so the folding machinery must
exist first). The second cross-cutting dimension: `(?r)` searches from the end of the subject
toward the start, and every consuming opcode has a `_REV` twin that steps backward. The parser
already emits them all; this slice makes them run.

## Scope

All line references are `upstream/src/_regex.c`.

- **Direction plumbing**: `state.reverse` out of `state_init_2` (`:18275`), the reversed
  search-position advance in `basic_match`'s outer loop, reversed `beginning`/`length` clamping
  in `get_limits` (`:21627`) semantics, and backward codepoint stepping over UTF-16 (a low
  surrogate steps back two units - the mirror of S16's decode, and the first place it runs
  leftward).
- **Main-switch cases**: `ANY_REV` / `ANY_ALL_REV` / `ANY_U_REV` (`:11977`, `:11957`, `:12017`),
  `CHARACTER_REV` / `CHARACTER_IGN_REV` (`:12191`, `:12169`), `PROPERTY_REV` /
  `PROPERTY_IGN_REV` (`:13872`, `:13850`), `RANGE_REV` / `RANGE_IGN_REV` (`:13982`, `:13960`),
  `SET_*_REV` and `SET_*_IGN_REV` (`:14520-14523`, `:14496-14499`), `STRING_REV` /
  `STRING_IGN_REV` / `STRING_FLD_REV` (`:15103`, `:15046`, `:14882`), `REF_GROUP_REV` /
  `REF_GROUP_IGN_REV` / `REF_GROUP_FLD_REV` (`:14375`, `:14318`, `:14161`).
- **Backtrack cases**: the REV rows of the one-character block (`:15210-15243`), the REV string
  rows of both REPEAT_ONE sub-switches (`:15950-16342`, `:16608-17107`), the backreference REV
  rows (`:17269-17276`, `:17292`).
- **Zero-width predicates in reverse**: the boundary and anchor predicates are direction-neutral
  (they inspect both sides of a position) - verify that claim against the oracle rather than
  assume it, and pin whichever way it falls.
- `match_many_*_REV` steppers (`:3592`, `:3647`, `:3725`, `:3924`, `:3987`, `:4265`, `:4375`,
  `:4611`, `:4674`, `:4863`, `:4926`) for the REPEAT_ONE paths.

## Verification

- **Un-skip `needs:right-to-left`** (47 tests), reading each skip's prose first; stragglers
  retag with prose.
- **Oracle wave**: take the S16-S22 generators and prefix `(?r)` - literals, classes, groups,
  quantifiers, backreferences, boundaries, under `(?i)` and `(?f)` too - comparing spans and all
  groups. Reverse plus astral subjects is the highest-risk cell (upstream fixed exactly that
  interaction in 2026.5.9, `upstream/changelog.txt`: "Reverse matching with full unicode
  casefolding could lead to out-of-range string indexes"), so probe FLD_REV over astral and
  expanding-fold subjects hard. Zero divergences; negative control.

## Done when

- [ ] `right-to-left` delivered or stragglers retagged; counts in closing notes.
- [ ] Oracle wave green including the FLD_REV/astral probes; counts quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a backward step that lands on the low
      surrogate of a pair, an off-by-one between "position before the character" forward and
      "position after" backward, a REV string comparison iterating the pattern forward), commit.
