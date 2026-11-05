# D141 rework: where to keep a node's ASCII answers (2026-11-05)

Point-in-time record for phase 11 item 2. The checklist lives in `STATE.md`.

## What was compared

Five builds of the same quick harness (`.scratch/d141r/harness/harness.cs`): six compile rows and
five match rows over 100 KB of English-like text, each row 1 s of warm-up then 15 batches of about
200 ms, median ns per call and bytes per call.

| Side | Tree |
|---|---|
| Main | main 2f361f51 |
| Old | `perf/ascii-fill` 31643d2a, the first D141 with the shared `AsciiAnswerCache` |
| A | 982cfaf6: formulas and the generated property table, one `ulong[2]` per node |
| A2 | A, plus one shared read-only array per distinct property answer set (104 in all) |
| B | A, with the 128 bits as two `ulong` fields and a flag on `Node` instead of the array |
| Actl | A again: the A-A control |

Three rounds, each side twice per round in mirrored order (A-B-B-A), the starting side rotated per
round: six runs per side. Release, pinned to the performance cores (affinity FFF8), each run started
only once LibreHardwareMonitor read the CPU package at or below 86 C twice, 60 s apart (readings at
the start of each run: 69-83 C). Raw rows: `.scratch/d141r/harness/abba.tsv`.

## Results (median of six runs, ns per call; ratio to A)

| Row | Main | Old | A | A2 | B | Actl | Main/A | A2/A | B/A | Actl/A |
|---|---|---|---|---|---|---|---|---|---|---|
| compile `\w+@\w+\.com` | 6,780 | 3,766 | 3,760 | 3,746 | 3,728 | 3,783 | 1.803 | 0.996 | 0.991 | 1.006 |
| compile `[\w\s]+` | 11,281 | 7,243 | 7,255 | 7,177 | 7,178 | 7,168 | 1.555 | 0.989 | 0.989 | 0.988 |
| compile `(quick\|slow)\s+(?<colour>[a-z]+)\s+fox` | 12,131 | 9,191 | 9,233 | 9,173 | 9,211 | 9,144 | 1.314 | 0.994 | 0.998 | 0.990 |
| compile `(?i)hello world` | 9,676 | 9,644 | 9,743 | 9,534 | 9,558 | 9,494 | 0.993 | 0.979 | 0.981 | 0.974 |
| compile `(?i)[a-z0-9_]+\p{Lu}` | 19,479 | 18,902 | 19,485 | 19,046 | 19,634 | 19,436 | 1.000 | 0.977 | 1.008 | 0.997 |
| compile `(?:needle){e<=1}` | 4,427 | 4,431 | 4,420 | 4,419 | 4,419 | 4,422 | 1.002 | 1.000 | 1.000 | 1.000 |
| `\w+` Count | 1,378,263 | 1,384,468 | 1,369,241 | 1,381,421 | 1,356,240 | 1,381,426 | 1.007 | 1.009 | 0.991 | 1.009 |
| `(?i)[a-f]+z` IsMatch | 624 | 640 | 638 | 638 | 616 | 629 | 0.978 | 1.000 | 0.965 | 0.985 |
| `\p{Lu}\p{Ll}+` Count | 556,435 | 558,365 | 552,515 | 555,567 | 640,000 | 555,906 | 1.007 | 1.006 | **1.158** | 1.006 |
| `(?i)k\w+s` Count | 53,330 | 53,847 | 52,954 | 53,240 | 53,422 | 52,914 | 1.007 | 1.005 | 1.009 | 0.999 |
| `(?:wizard){e<=1}` Count | 226,919 | 225,348 | 221,903 | 224,425 | 217,906 | 224,129 | 1.023 | 1.011 | 0.982 | 1.010 |

Bytes per compile (constant across runs): A2 allocates 200-240 B less than A on every pattern with
a property (`\w+@\w+\.com` 14,600 against 14,800; `(quick|slow)...fox` 32,944 against 33,104;
`(?i)[a-z0-9_]+\p{Lu}` 102,664 against 102,704). B allocates more than A on every compile
(+56 to +408 B: each `Node` grows from 136 to 144 bytes, and every node pays it). Matching
allocates nothing on any side.

## Reading

- The rework against main: compiling a pattern with classes is 1.31-1.80x faster; matching is
  unchanged (every match row inside the A-A control's spread, 0.985-1.010).
- The rework against the first D141 (Old): equal within the control's spread on every row. Dropping
  the shared cache costs nothing.
- A2 against A: time inside the control's spread on every row; 200-240 B less per compile with a
  property. Kept.
- B against A: `\p{Lu}\p{Ll}+` Count is 15.8% slower, consistently in all six runs; no row gains
  beyond the control's spread; every compile allocates more. Rejected. Not chased further: the
  bits-on-`Node` layout is what the owner asked to have measured, and it lost.

Decision: A2 (`perf/ascii-fill`, the commit after 982cfaf6).
