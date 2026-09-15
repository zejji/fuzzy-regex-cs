# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS STILL A CHECKPOINT (2026-09-15, sitting 3) - AND THIS IS THE THIRD, so the driver stops
here and the owner decides.** Suite 6,109 / 6,109 / 0 skipped, ratchet GREEN, baseline 5,995 ->
**6,001**. The slice file stays pending and its sitting-3 notes carry the detail.

**The remaining red is gone.** All six `(*SKIP)` rows sitting 2 left unjudged are judged, classified
and pinned, and the **three-seed 2000-row wave is GREEN at every seed** - 126,000 rows, diverge 0.
Three families, not one: rows 24018 and 24737 into `partial-retry-carried-slice-forward` (upstream's
partial call answers a NON-PARTIAL match its own non-partial call denies); rows 24224 and 38101 into
a new **`end-of-line-reads-a-skip-moved-slice`**; rows 25854 and 38151 into a new
**`skip-carried-slice-on-a-scan-with-no-walk`**. Six gap tests, each mutated red. Ledger entry 5
gains the line to put at the top of that report: seven of upstream's eight text-edge predicates read
a TEXT bound and `try_match_END_OF_LINE` alone reads `slice_end` - **including `$`'s own Unicode
twin**, so upstream's `$` disagrees with itself in one file.

**Sitting 4's job, all of it still unstarted in S52:** the long-subject generators, the timeout rows,
the `oracle.yml` CI job for the sweep, the 20-seed sweep run (`tools/sweep-seeds.ps1` exists from
sitting 1) and the 6000-row three-seed gate. Nothing is blocked; the slice is simply bigger than
three sittings, which is what the owner should weigh.

**Two process facts worth carrying.** (1) A stepwise walk is NOT a control for a row whose pattern
reads the end of the subject - passing an `endpos` sets the very bound the defect leaves stale, and
the recorder's `_reads_the_end_of_the_subject` refusal is right rather than a gap. (2) **Never write
a tracked file from a Python helper on Windows without `newline=""`**: two scratch scripts flipped
three `.cs` files to CRLF against `.editorconfig`'s `end_of_line = lf`, and `dotnet build
tests/FuzzyRegex.OracleTests` then failed with 14 IDE0055 errors in untouched code. The ratchet
builds only `tests/FuzzyRegex.Tests`, so it did not see it - **build the oracle tests too**.

**Owed, carried:** a repo-wide `_regex.c` citation reconciliation - `:14545`/`:14551`/`:20903`/
`:18160` are stale against the 2026.9.10 pin and `:14553`/`:14555`/`:20927-20928`/`:18159` are
right. The correct spelling is the majority for the two `RE_OP_SKIP` writes and the MINORITY for
the scanner step (`:20903` 22 uses against `:20927`'s 7), so a sweep cannot just follow the
count. Only sitting 3's own text is right; `.claude/skills/port-tests/SKILL.md` still teaches
`FuzzyRegex.Search(...)`; `record-oracle.py --self-check` exits 1 (pre-existing since S43);
`tools/run-controls.py` cannot measure a control that mutates the recorder (S42-2A); broken control
sites S32-B and S38-A; S35-A and S29-A/D thin; PORTMAP `_regex.c` lines stale.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
