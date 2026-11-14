# Does the Myers scan pay on short subjects? (2026-11-06)

Point-in-time record for phase 11 item 11. The checklist lives in `STATE.md`.

**Answer: yes, by a wide margin. No change.** The 2026-11-04 profile left this open: the fuzzy
literal filter's bit-parallel scan (`FuzzyLiteralFilter.ScanNextStart`, C3) took 12.6% of samples
in one short-subject trace and 0.1% in another, so it might have cost more than it saved where the
subject is short. Turning it off for short subjects makes them far slower.

## How it was measured

One experimental build of the integration branch (e9c4161a plus one line, never committed): the
filter skips the scan when fewer than `FUZZYREGEX_SCAN_MIN` UTF-16 units are left. Side A sets it
to 0 (today's behaviour), side B to 256 (no scan on any record or short sentence), and the A-A
control runs A again. One binary, so the three sides run identical code apart from that value.
Harness `.scratch/p11-myers/harness.cs`: each row 1 s of warm-up then 15 batches of about 200 ms,
median ns per call. Three rounds, each side twice per round in mirrored order, the starting side
rotated: six runs per side. Release, pinned to the performance cores, each run started once the
CPU package read 86 C or less twice, 60 s apart (readings 69-86 C). Both sides returned the same
answer digest on every row.

## Results (median of six runs, ns per call)

| Row | A (scan) | B (no scan under 256) | A-A control | B/A | Actl/A |
|---|---|---|---|---|---|
| `(?:haystack){e<=3}` Match, 64-char near miss | 968 | 56,917 | 967 | 58.78 | 0.999 |
| same with `(?e)` | 2,299 | 57,902 | 2,277 | 25.19 | 0.990 |
| same with `(?b)` | 2,760 | 58,344 | 2,763 | 21.14 | 1.001 |
| `(?i)(?:amber lantern works){e<=2}` IsMatch, 1,000 records | 440,697 | 471,121 | 444,009 | 1.069 | 1.008 |
| same, Match | 457,257 | 487,382 | 460,416 | 1.066 | 1.007 |
| `(?:needle){e<=1}` IsMatch, 1 MB | 99,061 | 99,189 | 99,484 | 1.001 | 1.004 |
| `(?:a needle in a haystack){e<=2}` Match, 1 MB | 243,377 | 250,758 | 262,178 | 1.030 | 1.077 |

## Reading

Without the scan, a short subject that holds no near match is tried at every position by the
fuzzy matcher, which is what the scan exists to prevent: 21-59 times slower on the near miss, and
6.6-6.9% slower on the records, most of which hold no phrase. The 1 MB rows do not change, as
expected (the threshold only touches their last 256 units); the last row's 3% is inside its own
A-A spread (7.7%). The 12.6% share seen in one profile was the price of that saving, not waste.
