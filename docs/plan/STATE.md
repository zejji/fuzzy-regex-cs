# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 to S10 done, three pending.

**Current slice:** none in flight. The tree is clean.

**Where S10 left the port:** ratchet GREEN, 1447 passing tests (baseline 1447), 3843 total, nothing
failing; overall parity 0.7%. Sets and set operators, POSIX classes, properties, `\N{...}`,
`(?#...)`, case folding and the whole inline-flag surface are ported. `character-classes` drops
from 573 waiting tests to 114, `unicode-properties` to 95, `case-folding` to 69. `quantifiers`
(179) is now the biggest block, then `fuzzy-syntax` (162) and `substitution` (148) - all three
need a matching engine, which is Phase 3.

**Next action:** S11, `docs/plan/slices/S11-groups-references-lookaround.md` - backreferences,
group calls, conditionals, lookaround, atomic groups and verbs. `parse_paren` throws for every
`(?` form it owns; nothing else blocks it.

**Blockers:** none.

**Worth knowing before the next slice:**

- **S11's scope lists `parse_comment` (`:978`); S10 already ported it.** Skip that bullet.
- **Un-skip nothing speculatively in S11.** Every `groups`/`named-groups`/`branch-reset` test needs
  a match, so the slice's evidence is corpus rows, not newly passing upstream tests.
- **A differential wave beats the corpus for anything the upstream suite is thin on.** S10's found
  the `in_set` defect and three upstream-internal-error patterns the corpus never reaches. The
  recorder is `.scratch/s10_diff_record.py`; the C# comparison half was scratch and is gone.
- **Suspect the parser before the tables.** The Unicode tables are checked against upstream
  exhaustively; the corpus is not. A row that disagrees only in set-member order is a set-order
  leak - S10 landed the sort as `RegexBase.RenderKey`, so check that first.
- **Regenerating anything Unicode:** `tools/transliterate-unicode.py`, then
  `tools/build-character-names.py`, then `tools/build-lowercase.py`, then
  `tools/record-unicode-fixtures.py` - in that order. `oracle.yml` runs all four with `--check`.
- **Local `regex` is 2026.7.19; upstream is pinned at 2026.8.12.** CI builds the oracle from the
  pinned submodule.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
