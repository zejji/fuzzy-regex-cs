# The complete interaction matrix (2026-09-30)

Part A, the answer key, is written on branch `matrix/answer-key`. This branch (`matrix/harness`)
owns only Part B's skeleton, below; the two parts meet when both branches merge.

## Part B: the measurement

### How it is measured

`tools/matrix/` holds the harness:

- `gen.py` builds rows by cell: every pair of the construct ids in `tools/matrix/constructs.json`
  (696 pairs, the 7 impossible ones excluded) and the 35 triples of the risky families. Each
  construct wraps a smaller pattern, so the constructs nest or sit side by side reading each other.
  A row counts for a cell only when upstream's parser finds every construct of the cell in it
  (`tagger.py`). The run fails if any feasible cell has fewer than 50 rows.
- `run.py` judges every row by every check that applies, in resumable chunks of under 45 minutes:

| Check | Question | Judge |
|---|---|---|
| C1 | Does the port answer as upstream regex 2026.9.10 does? | `record-oracle.py` and the oracle consumer; `EXPECTED` rows are ones an ExpectedDivergences entry accounts for |
| C1x | The same, for the finditer rows the recorder cannot ask (partial, pos/endpos) | `pyworker.py c1x` |
| C2 | Does a pattern with calls answer as the same pattern with each call written out? (depths 4-6 for recursion) | `port-runner.cs` |
| C3 | Does any Debug.Assert or exception fire? | `port-runner.cs`, Debug build |
| C4 | Is a partial answer consistent with the port's non-partial answers over every continuation up to 3 characters? | `port-runner.cs` judge and `d11-brute-judge.py` |
| C5 | Do the answers stay the same with the failure memos forced off and on? | `port-runner.cs` |
| C6 | Does the port agree with the reference matcher? | `tools/probes/fuzzy-reference-matcher.py` |
| C7 | Where PCRE2 10.47 and Perl 5.42.3 can both express a row and agree, does the port give their span, groups and last captures? (single-engine, engines-disagree and OPEN rows are counted apart, not as failures) | `c7.py` through the answer key's `survey.py`; `summary.md` lists its translation and exclusion rules |

A check that does not apply to a row is counted as n/a, never as a pass. `controls.py` holds one
row per check that must fire and one that must stay quiet.

### Results per check

Filled from `summary.md` and `cells.csv` of the run (`.scratch/matrix/results/<run>/`).

| Check | Applicable rows | n/a | Failures | Accounted (EXPECTED) | Phantoms | Cells with a failure |
|---|---|---|---|---|---|---|
| C1 | | | | | | |
| C1x | | | | | | |
| C2 | | | | | | |
| C3 | | | | | | |
| C4 | | | | | | |
| C5 | | | | | | |
| C6 | | | | | | |
| C7 | | | | | | |

### Failures by root cause

One entry per root cause: a minimal witness, the checks it fails, whether main and upstream share
it, and its register row (Dnn) or NEW.

| Root cause | Witness | Checks | Main / upstream | Register |
|---|---|---|---|---|

### Count of distinct root causes

(to be filled after triage)
