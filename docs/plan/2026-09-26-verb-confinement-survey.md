# Verbs backtracked onto inside an unfinished group: engine survey

Date: 2026-09-26. Revised twice the same day after blind reviews:

- The first review re-ran 60 rows and found five wrong claims. They are corrected, and its 29 extra
  rows plus f01-f04 are in the battery.
- The second review ran 118 rows and found two gaps in the model: called groups, and conditions with
  a negative test. It also found more Perl and Boost departures. Its 52 rows, plus four probes of my
  own and a control, are in the battery.

Question ("family B" of the random verb grid): when a later failure *inside* an atomic group,
possessive repeat or lookaround backtracks onto a `(*PRUNE)` or `(*SKIP)` in that same group, before
the group has finished, what happens? And once the group has finished, is the verb confined?

Terms used throughout:

- **Attempt fails**: the verb has its ordinary meaning. `(*PRUNE)` ends the attempt at this start
  position; `(*SKIP)` ends it and the next attempt starts where the verb was reached.
- **Confined**: the verb makes only the group fail, and matching carries on outside the group (the
  next alternative, a zero-iteration fallback, and so on).
- **Unfinished / finished**: whether the group had reached its end on the path that backtracked onto
  the verb.

A worked example. `(?>a(*PRUNE)b)|a` over `ac`: at position 0 the group matches `a`, passes the verb,
then `b` fails against `c` and backtracking reaches the verb. Confined: the group fails, the second
alternative matches `a`, result (0,1). Attempt fails: position 0 is abandoned, position 1 (`c`) cannot
match either branch, result None.

## Answer in brief

PCRE2 has one consistent model, the same in its interpreter (with or without start optimisations) and
in JIT with start optimisations off. When backtracking reaches a `(*PRUNE)` or `(*SKIP)`, it unwinds
to the innermost enclosing construct of one of three kinds. If there is none, the whole attempt fails.

1. **Negative assertion** (lookahead or lookbehind, standalone or as a condition): the assertion
   becomes true, and no further branches are tried.
2. **Conditional's assertion test**: the condition becomes false if the assertion is positive, and
   true if it is negative. (A negative test is also covered by target 1.)
3. **Called group** (subroutine call or recursion): the call fails and backtracking continues at the
   outer level. A `(*SKIP)` position recorded inside the call is discarded (sr1, sr3).

Atomic groups, possessive repeats and positive lookarounds are transparent to all of this while
unfinished, however they are nested or quantified. Once a group has finished, backtracking never
re-enters it, so a verb inside it can never be reached. That is the only sense in which PCRE2
"confines" a verb.

How the other engines differ, over 118 rows:

- **Upstream and the port** instead unwind to the innermost enclosing atomic group, possessive
  repeat, lookaround or conditional test of any kind. They treat called groups as transparent. From
  a48b9d3 (ledger 45), 64 rows would change under PCRE2's model (section 6).
- **Main and upstream** also let a `(*SKIP)` in a *finished* atomic group move the next start (f01).
  Ledger 45 fixes this.
- **Perl 5.42 and 5.38** (identical) agree with PCRE2 on the plain cases but depart on 28 of the 118
  rows, in ten kinds of construct (section 3). They include every quantified group, most nested
  negative assertions, and called groups, which Perl treats as transparent, as the port does. A
  29th row, cl1r, differs even without the verb, so it is not a verb difference.
- **Boost** confines in positive lookarounds and treats called groups as transparent. It fails the
  attempt for groups nested in a negative lookahead, leaks verbs out of finished lookaheads, and lets
  a `(*SKIP)` position escape a negative lookahead that becomes true or a conditional test that
  becomes false (sn1, sn2, sc1).

## 1. Upstream: what it says and how it works

README.rst, lines 204-211 (upstream submodule at 7dd71c1, regex 2026.9.10), quoted exactly:

```
Added ``(*PRUNE)``, ``(*SKIP)`` and ``(*FAIL)`` (`Hg issue 153 <https://github.com/mrabarnett/mrab-regex/issues/153>`_)
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^

``(*PRUNE)`` discards the backtracking info up to that point. When used in an atomic group or a lookaround, it won't affect the enclosing pattern.

``(*SKIP)`` is similar to ``(*PRUNE)``, except that it also sets where in the text the next attempt to match will start. When used in an atomic group or a lookaround, it won't affect the enclosing pattern.

``(*FAIL)`` causes immediate backtracking. ``(*F)`` is a permitted abbreviation.
```

The only related changelog entry is 2026.8.30, "Git issue 613: (*SKIP) inside an atomic group, plus
an equality-only scan stop". Issue 613 (filed by the maintainer, "Reported by Claude and Ada Logics")
is a heap over-read, where the `(*SKIP)` slice move survived an atomic group's unwind. It is a
memory-safety fix and says nothing about which semantics is intended.

Issue 153 (the feature request, 2015) is the design record. The requester cites PCRE's documentation
as clearer than Perl's, and suggests the refinements can be ignored "since the verbs are experimental
and not consistent between PCRE and Perl". The maintainer's summary (2015-09-11T20:16:56Z):

> A (*SKIP) in a lookaround won't affect the enclosing pattern, but one in the outermost pattern
> _will_ affect where the regex will attempt to match next if the entire pattern fails to match

Every case discussed in the thread is a lookaround that finished, or one whose body never reached the
verb. The unfinished case was never compared with PCRE or Perl.

How `_regex.c` implements it (upstream/src/_regex.c):

- `push_bstack` (:2590) pushes the backtrack-stack size onto a separate "pstack". It is called at the
  start of every attempt in `basic_match` (:11780), and at `RE_OP_ATOMIC` (:12050),
  `RE_OP_CONDITIONAL` (:12238) and `RE_OP_LOOKAROUND` (:13787).
- `RE_OP_PRUNE` (:13894) and `RE_OP_SKIP` (:14544) call `top_bstack` (:2811) when the verb *runs*.
  That cuts the backtrack stack back to the innermost pstack mark, so the next backtrack fails the
  innermost such construct as a unit.
- `RE_OP_SKIP` also moves `slice_start` at once (:14551-14555). The atomic unwind does not restore
  the slice, so a `(*SKIP)` in a finished atomic group still moves the next start (f01).
  A lookaround does restore the slice, so the same verb in a finished lookahead does not (f02).
- A group call pushes no pstack mark, so a verb in a called group cuts through the call to the
  enclosing construct or the attempt (rc1-rc5).

**Verdict: deliberate and documented, but not weighed against PCRE or Perl for this case.**

- For: confinement is stated twice in the README; it was written when the feature was added
  (2015.09.14); and it follows directly from the design, which gives each construct its own scope.
- Against: the README sentence does not tell a finished group from an unfinished one; it does not
  mention negative assertions, where PCRE2's rule differs from upstream's only when groups are nested
  (n03, n04); and nothing in issue 153 shows the unfinished case was tested against another engine.

The port follows upstream exactly: `TopBstack` (src/FuzzyRegex/Engine/Matcher.cs:3083), the Prune arm
(:8594) and the Skip arm (:9255).

## 2. Survey

### Engines and versions

All runs 2026-09-26 on this machine. Drivers, batteries and raw outputs are in the session scratchpad
(`scratchpad/verb-b/`: `battery_v4.tsv` with all 118 rows, `run_py.py`, `run_perl.pl`, `run_boost.cpp`,
`run_onig.py`, `run_port.ps1`, `run_port_st.ps1`, `v4_*.txt`, `res_v4.json`). The reviewers' files are
in `scratchpad/rv-verb/` and `scratchpad/rv-verb2/`.

| Engine | Version | Verbs | How run |
| --- | --- | --- | --- |
| Port | main e690479, Debug build 20:50 | PRUNE, SKIP, FAIL | PowerShell, `FuzzyRegex.Match` |
| Port, ledger 45 | maint/skip-timing a48b9d3, Debug build 20:54 | same | same |
| mrab `regex` | 2026.9.10, Python 3.14.7 | PRUNE, SKIP, FAIL | `regex.search` |
| PCRE2 | 10.47 2025-10-21 (Git for Windows libpcre2-8-0.dll) | all | ctypes, five modes: interpreter; interpreter with NO_START_OPTIMIZE; interpreter with NO_AUTO_POSSESS; JIT; JIT with NO_START_OPTIMIZE |
| Perl | 5.42.3 (Cygwin), 5.38.2 (Ubuntu WSL) | all | battery read from a file, so no argv mangling |
| Boost.Regex | git a640597 (2026-05-22), BOOST_RE_VERSION 600, standalone header-only, g++ 13.3 | PRUNE, SKIP, THEN, COMMIT, FAIL, ACCEPT | `regex_search`, perl syntax |
| Oniguruma | onigurumacffi 1.5.0 wheel | `(*FAIL)`, plus a `(*SKIP)` callout with a different meaning | `search` |
| fancy-regex | 0.19.2 (Rust 1.98, WSL) | parses verbs, runs only FAIL | `Regex::find` |
| Python `re` | 3.14.7 | none | `re.search` |
| Ruby (Onigmo) | 3.2.3 | none | `Regexp#match` |
| Java | OpenJDK 26.0.2.1 | none | `Pattern.compile` |
| .NET | 10.0.12 | none | `Regex` |

The engines with no verbs reject `(*PRUNE)` at compile time. The messages were:

- Python `re`: "nothing to repeat at position 6"
- Ruby: "target of repeat operator is not specified"
- Java: "Dangling meta character '*'"
- .NET: "Quantifier '*' following nothing"
- Oniguruma: "undefined callout name"
- fancy-regex: "Regex uses currently unimplemented feature: Backtracking control verbs other than 'fail'"

RE2 and ICU have no verbs. Oniguruma's `(*SKIP)` "Advance[s] the position where the current matching
fails and the next search begins to the current position. It has no effect on the current matching"
(doc/CALLOUTS.BUILTIN). It never prunes, so it cannot vote on this question.

**PCRE2 modes.** On all 118 rows the interpreter gives the same answer with or without
NO_START_OPTIMIZE, and with NO_AUTO_POSSESS. JIT with NO_START_OPTIMIZE also gives the same answer.
Only default JIT differs, on two rows, one of which has no group at all:

- ap1 `a+(?>(*PRUNE)x)|a` over aab
- ap3 `a+(*PRUNE)x|a` over aab

Default JIT gives (1,2) on both; every other mode, Perl and Boost give None. pcre2api, "PCRE2_START_OPTIMIZE_OFF", says start-up
optimisations "may change the outcome of a matching operation" for items such as `(*COMMIT)`. So the
no-optimisation semantics is PCRE2's defined model, and it is the "PCRE2" column below.

### Results

Columns:

- **up**: upstream `regex`. It agrees with the port's main build on every row.
- **main**, **a48b**: the port, on main and on the ledger 45 commit.
- **PCRE2**: the no-optimisation model. Default JIT differs only on ap1 and ap3.
- **Perl**: 5.42.3. 5.38.2 is identical on every row that was run on both.
- **Boost**.

Bold marks a departure from the PCRE2 column. ERR is a compile error.

Unfinished atomic group, possessive repeat, positive lookaround:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| b01 | `(?>aa(*SKIP)x(*PRUNE)y)\|a` | aaxz | **(0,1)** | **(0,1)** | (1,2) | (1,2) | **None** |
| b02 | `(?>a(*PRUNE)b)\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| b03 | `(?>aa(*SKIP)b)\|a` | aaca | **(0,1)** | **(0,1)** | (3,4) | (3,4) | (3,4) |
| b04 | `(?>(*SKIP)ab)\|a` | ac | **(0,1)** | **(0,1)** | None | None | **ERR** |
| sk1 | `(?>a(*SKIP)b)\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| sk2 | `x\|(?>a(*SKIP)ab)\|a` | aac | **(0,1)** | **(0,1)** | None | None | None |
| x06 | `a(?>(*SKIP)b)\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| alt1 | `(?>a(*PRUNE)b\|ac)\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| ap2 | `(?>a+(*PRUNE)b)\|a` | aac | **(0,1)** | **(0,1)** | None | None | None |
| b05 | `(?:a(*PRUNE)b)++\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| b06 | `(?:aa(*SKIP)b)++\|a` | aaca | **(0,1)** | **(0,1)** | (3,4) | (3,4) | (3,4) |
| alt2 | `(?:a(*PRUNE)b\|ac)++\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| b07 | `(?=a(*PRUNE)b)..\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| b08 | `(?=aa(*SKIP)b)aa\|a` | aaca | **(0,1)** | **(0,1)** | (3,4) | (3,4) | **(0,1)** |
| alt3 | `(?=a(*PRUNE)b\|ac)..\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| b09 | `a.(?<=a(*PRUNE)b)\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| lb1 | `(?<=a(*SKIP)x)c\|c` | abc | **(2,3)** | **(2,3)** | None | None | **(2,3)** |
| lb2 | `(?<=a(*PRUNE)x)c\|c` | abc | **(2,3)** | **(2,3)** | None | None | **(2,3)** |
| ap1 | `a+(?>(*PRUNE)x)\|a` | aab | **(0,1)** | **(0,1)** | None | None | None |

In b09, lb1 and lb2 the port's answer differs from PCRE2's only because of direction. Upstream runs
the lookbehind body right to left and fails before reaching the verb, so those rows do not change
under PCRE2's model (section 6).

Nested groups, none of them negative:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| b12 | `(?>(?>a(*PRUNE)b)\|ac)\|a` | ac | **(0,2)** | **(0,2)** | None | None | None |
| b12b | `(?>(?>a(*SKIP)ab)\|aa)\|a` | aac | **(0,2)** | **(0,2)** | None | None | None |
| n01 | `(?=(?>a(*PRUNE)b))..\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| n02 | `(?>(?=a(*PRUNE)b)..)\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |

Quantified groups. A possessive quantifier or an enclosing atomic group removes the zero-iteration
fallback, so pos1, pos3, x07g and x08 agree everywhere:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| x07 | `(?>a(*PRUNE)b)?a` | ac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| x07c | `(?>a(*PRUNE)b){0,1}a` | ac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| x07d | `(?>a(*PRUNE)b)*a` | ac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| x07e | `(?:(?>a(*PRUNE)b))?a` | ac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| x07f | `(?=a(*PRUNE)b)?a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| x07b | `(?>a(*PRUNE)b)??a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| p03 | `(?:(?>a(*PRUNE)b)\|)a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| rep1 | `(?>(?:a(*PRUNE))*b)\|a` | aac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| rep2 | `(?>(?:a(*SKIP))*b)\|a` | aaca | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| rep3 | `(?:(?:a(*PRUNE))*b)++\|a` | aac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| pos1 | `(?:a(*PRUNE)b)?+a` | ac | None | None | None | None | None |
| pos3 | `(?:a(*PRUNE)b){0,1}+a` | ac | None | None | None | None | None |
| x07g | `(?>(?:a(*PRUNE)b)?)a` | ac | None | None | None | None | None |
| x08 | `(?:a(*PRUNE)b)*+a` | ac | None | None | None | None | None |

Negative lookarounds and conditional tests, alone and nested. nl1 and nl1r are the same test written
for each lookbehind direction: PCRE2, Perl and Boost run a lookbehind body left to right, upstream
right to left, so each engine reaches the verb only in its own row. Read them as a pair:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| b10 | `(?!a(*PRUNE)b\|a)a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| nl1 | `..(?<!a(*PRUNE)b\|ac)` | ac | None | None | (0,2) | (0,2) | (0,2) |
| nl1r | `..(?<!x(*PRUNE)c\|ac)` | ac | (0,2) | (0,2) | None | None | None |
| b11 | `(?(?=a(*PRUNE)b)ab\|a)` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| c1 | `(?(?=(?>a(*PRUNE)b))ab\|a)` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| n03 | `(?!(?>a(*PRUNE)b)\|a)a` | ac | **None** | **None** | (0,1) | (0,1) | **None** |
| n04 | `(?!(?=a(*PRUNE)b)\|a)a` | ac | **None** | **None** | (0,1) | (0,1) | **None** |
| nl2 | `(?!(?>(?=a(*PRUNE)b)))a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | **None** |
| n05 | `(?>(?!a(*PRUNE)b\|a)a)\|a` | ac | (0,1) | (0,1) | (0,1) | **None** | (0,1) |
| c2 | `(?>(?(?=a(*PRUNE)b)ab\|a))\|x` | ac | (0,1) | (0,1) | (0,1) | **None** | (0,1) |

nl2 agrees with PCRE2 in the port only by accident. The verb is confined to the inner lookahead, and
the answer comes out the same because nothing else in the negative body could match. c1, c2, nl1,
nl1r and nl2 were run on Perl 5.42 and 5.38 (identical) and on the a48b9d3 build; on those rows,
main equals a48b9d3, because they contain no `(*SKIP)`.

Finished groups:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| b14 | `(?>a(*PRUNE))b\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| x02 | `(?>a(*SKIP))b\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| f01 | `(?>aa(*SKIP))x` | aaax | **None** | (1,4) | (1,4) | (1,4) | **None** |
| f03 | `(?>aa(*SKIP))x\|b` | aaab | (3,4) | (3,4) | (3,4) | (3,4) | (3,4) |
| b15 | `(?=a(*SKIP))ab\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | **None** |
| x03 | `(?=a(*PRUNE))ab\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | **None** |
| x04 | `(?=a(*SKIP))..\|a` | ac | (0,2) | (0,2) | (0,2) | (0,2) | (0,2) |
| f02 | `(?=aa(*SKIP))aax` | aaax | (1,4) | (1,4) | (1,4) | (1,4) | **None** |
| f04 | `(?=aa(*SKIP))...\|b` | aab | (0,3) | (0,3) | (0,3) | (0,3) | (0,3) |

Controls and verbs mrab lacks:

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| b13 | `(?>a(*FAIL))\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| f1 | `(?>a(*FAIL)\|a)\|b` | ab | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| b18 | `(?:a(*PRUNE)b)\|a` | ac | None | None | None | None | None |
| ap3 | `a+(*PRUNE)x\|a` | aab | None | None | None | None | None |
| ap4 | `a+(*SKIP)x\|a` | aab | None | None | None | None | None |
| x01 | `aa(*SKIP)x(*PRUNE)y\|a` | aaxz | **None** | (1,2) | (1,2) | (1,2) | **None** |
| lb3 | `b(?<=a(*SKIP)xb)\|b` | ab | (1,2) | (1,2) | (1,2) | (1,2) | (1,2) |
| b09r | `a.(?<=x(*PRUNE)c)\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| b16 | `(?>a(*COMMIT)b)\|a` | ac | ERR | ERR | None | None | None |
| b17 | `(?>a(*THEN)b)\|a` | ac | ERR | ERR | (0,1) | (0,1) | (0,1) |

Boost's b01, x01 and f01 answers come from applying `(*SKIP)` when it is *passed*, the timing
upstream had before ledger 45. Boost also rejects b04 and `(?:(*SKIP)ab)|a` with "The complexity of
matching the regular expression exceeded predefined bounds".

Rows from the second blind review (52), plus four `(*SKIP)`-in-a-called-group probes (sr0-sr3) and a
control. cl1/cl1r, cn2 and lbs1 are lookbehind rows where the engines' directions differ; see the note
under the negative-lookaround table. cl1r also differs in Perl without the verb: `..(?(?<=xb|b)b|z)`
over abz gives (0,3) in Perl and None in PCRE2. Boost rejects the conditional lookbehinds (cl1, cl1r,
cn2) with "Invalid lookbehind assertion".

| id | pattern | subject | up, main | a48b | PCRE2 | Perl | Boost |
| --- | --- | --- | --- | --- | --- | --- | --- |
| d1 | `(?>(?>(?>a(*PRUNE)b)))\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| d2 | `(?!(?>(?=(?>a(*PRUNE)b)))\|a)a` | ac | **None** | **None** | (0,1) | (0,1) | **None** |
| d3 | `(?>(?!(?>a(*PRUNE)b)\|a)a)\|x` | ac | **None** | **None** | (0,1) | **None** | **None** |
| d4 | `(?=(?!(?=a(*PRUNE)b)\|a))a\|ac` | ac | **(0,2)** | **(0,2)** | (0,1) | **None** | **(0,2)** |
| d5 | `(?=(?>(?!(?=a(*PRUNE)b)\|a)))a\|ac` | ac | **(0,2)** | **(0,2)** | (0,1) | **None** | **None** |
| nb1 | `(?<!(?=a(*PRUNE)b)a\|a)c` | ac | **None** | **None** | (1,2) | (1,2) | **None** |
| nb2 | `(?<!(?>(?=a(*PRUNE)b)a)\|a)c` | ac | **None** | **None** | (1,2) | (1,2) | **None** |
| nb3 | `(?<!(?>(?>(?=a(*PRUNE)b)a))\|a)c` | ac | **None** | **None** | (1,2) | (1,2) | **None** |
| pb1 | `(?<=(?!a(*PRUNE)b\|a)a)c` | ac | (1,2) | (1,2) | (1,2) | **None** | (1,2) |
| pb2 | `(?<=(?=a(*PRUNE)b)a)c\|c` | ac | **(1,2)** | **(1,2)** | None | None | **(1,2)** |
| pb3 | `(?>.(?<=a(*PRUNE)b))\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| cl1 | `..(?(?<=a(*PRUNE)c\|b)b\|z)` | abz | **None** | **None** | (0,3) | (0,3) | **ERR** |
| cl1r | `..(?(?<=x(*PRUNE)b\|b)b\|z)` | abz | **(0,3)** | **(0,3)** | None | **(0,3)** | **ERR** |
| cn1 | `(?(?!a(*PRUNE)b\|a)a\|ac)` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| cn2 | `..(?(?<!a(*PRUNE)b\|a)b\|c)` | aab | **None** | **None** | (0,3) | (0,3) | **ERR** |
| cn3 | `(?(?=(?!a(*PRUNE)b\|a))a\|ac)` | ac | (0,1) | (0,1) | (0,1) | **(0,2)** | (0,1) |
| cn4 | `(?!(?(?=a(*PRUNE)b)ab\|a))a\|ac` | ac | (0,2) | (0,2) | (0,2) | **(0,1)** | (0,2) |
| cb1 | `(?(?=a)a(*PRUNE)b\|a)\|a` | ac | None | None | None | None | None |
| cb2 | `(?(?=a)ab\|a(*PRUNE)b)\|a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| va1 | `(?!x\|a(*PRUNE)b\|a)a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| va2 | `(?!a(*SKIP)b\|a)a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| va3 | `(?=(?:a(*PRUNE)b\|ac))..\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| va4 | `(?=x\|a(*PRUNE)b\|ac)..\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| lz1 | `(?>(?:a(*PRUNE))*?b)\|a` | aac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| lz2 | `(?>(?:a(*PRUNE))+?b)\|a` | aac | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| lz3 | `(?>a(*PRUNE)b)??c\|a` | ac | **(0,1)** | **(0,1)** | (1,2) | (1,2) | (1,2) |
| lz4 | `(?>(?:a(*SKIP))*?c)\|a` | aab | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| gr1 | `(?>(?:a(*SKIP))+b)\|a` | aaca | **(0,1)** | **(0,1)** | None | **(0,1)** | None |
| gr2 | `(?>(?>a(*PRUNE)b)*)c\|a` | ac | **(0,1)** | **(0,1)** | (1,2) | (1,2) | (1,2) |
| gr3 | `(?:(?>a(*PRUNE)b)\|a)c` | ac | **(0,2)** | **(0,2)** | None | None | None |
| ps1 | `(?:a(*PRUNE)b\|a)*+c\|a` | ac | **(0,1)** | **(0,1)** | (1,2) | (1,2) | (1,2) |
| ps2 | `(?:a(*SKIP)b\|a)++\|a` | aac | **(0,1)** | **(0,1)** | None | None | None |
| ps3 | `(?:(?>a(*PRUNE)b)\|a)++c\|a` | ac | **(0,2)** | **(0,2)** | None | None | None |
| lbs1 | `c(?<=a(*SKIP)xc)\|c` | abc | **(2,3)** | **(2,3)** | None | None | **(2,3)** |
| lbs2 | `(?<=a(*SKIP)xb)c\|b` | axbc | (2,3) | (2,3) | (2,3) | (2,3) | (2,3) |
| ss1 | `(?=(*SKIP)ab)\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| ss2 | `(?!(*SKIP)x\|b)b` | b | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| fl1 | `(?=a(*PRUNE)(*FAIL))a\|a` | ac | **(0,1)** | **(0,1)** | None | None | **(0,1)** |
| fl2 | `(?!a(*PRUNE)(*FAIL)\|a)a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| fl3 | `(?>a(*FAIL)\|a(*PRUNE)(*F)\|a)\|x` | ac | None | None | None | None | None |
| fl4 | `(?!a(*FAIL)\|b)a` | ac | (0,1) | (0,1) | (0,1) | (0,1) | (0,1) |
| nn1 | `(?!(?!a(*PRUNE)b\|a))a` | ac | None | None | None | **(0,1)** | None |
| sn0 | `(?!(?=aa(*SKIP)b)\|a)a` | aac | **None** | **None** | (0,1) | (0,1) | **None** |
| sn1 | `(?!aa(*SKIP)b)aax` | aaax | (1,4) | (1,4) | (1,4) | (1,4) | **None** |
| sn2 | `(?!aa(*SKIP)b)ax` | aaxx | (1,3) | (1,3) | (1,3) | (1,3) | **None** |
| sc1 | `(?(?=aa(*SKIP)b)aab\|a)x` | aaxx | (1,3) | (1,3) | (1,3) | **None** | **None** |
| cg1 | `(?>(a(*PRUNE)b))\|a` | ac | **(0,1)** | **(0,1)** | None | None | None |
| rc1 | `(?1)\|a\|(a(*PRUNE)b)` | ac | **None** | **None** | (0,1) | **None** | **None** |
| rc2 | `(?&g)\|a\|(?<g>a(*PRUNE)b)` | ac | **None** | **None** | (0,1) | **None** | **None** |
| rc3 | `(?1)c\|a\|(a(*SKIP)ab)` | aac | **None** | **None** | (0,1) | **None** | **None** |
| rc4 | `(?:(?1)\|x)\|a\|(a(*PRUNE)b)` | ac | **None** | **None** | (0,1) | **None** | **None** |
| rc5 | `a(?R)?b(*PRUNE)c\|a` | aabbx | **None** | **None** | (0,1) | **None** | **None** |
| cn1c | `(?(?!ab\|a)a\|ac)` | ac | (0,2) | (0,2) | (0,2) | (0,2) | (0,2) |
| sr0 | `aa(*SKIP)x\|ab` | aab | None | None | None | None | None |
| sr1 | `(?1)\|ab(?(DEFINE)(aa(*SKIP)x))` | aab | **None** | **None** | (1,3) | **None** | **None** |
| sr2 | `(?1)\|ab(?(DEFINE)(aa(*PRUNE)x))` | aab | (1,3) | (1,3) | (1,3) | (1,3) | (1,3) |
| sr3 | `(?1)x\|a\|(aa(*SKIP)b)` | aac | **None** | **None** | (0,1) | **None** | **None** |

### Documentation

- **PCRE2**, pcre2pattern 10.47, "Verbs that act after backtracking": "if there is a subsequent match
  failure, causing a backtrack to the verb, a failure is forced ... However, when one of these verbs
  appears inside an atomic group or in an atomic lookaround assertion that is true, its effect is
  confined to that group, because once the group has been matched, there is never any backtracking
  into it." Confinement is a consequence of the group having finished, not a scope rule.
- **PCRE2**, "Backtracking verbs in assertions": "The other backtracking verbs are not treated
  specially if they appear in a standalone positive assertion. In a conditional positive assertion,
  backtracking (from within the assertion) into (*COMMIT), (*SKIP), or (*PRUNE) causes the condition
  to be false. However, for both standalone and conditional negative assertions, backtracking into
  (*COMMIT), (*SKIP), or (*PRUNE) causes the assertion to be true, without considering any further
  alternative branches." So a negative assertion used as a condition makes the condition *true*:
  cn1 `(?(?!a(*PRUNE)b|a)a|ac)` over ac gives (0,1), and the control without the verb gives (0,2).
  Rows b07, b08, b10, b11, cn1, n03, n04, n05, c1 and c2 all match this text. The text does not say
  whether nested groups count; the measured answer is that they are transparent.
- **PCRE2**, "Backtracking verbs in subroutines": "(*COMMIT), (*SKIP), and (*PRUNE) cause the
  subroutine match to fail when triggered by being backtracked to in a group called as a subroutine.
  There is then a backtrack at the outer level." And: "Perl's treatment of the other verbs in
  subroutines is different in some cases." Rows rc1-rc5, sr1 and sr3 match. In sr1
  `(?1)|ab(?(DEFINE)(aa(*SKIP)x))` over aab, PCRE2 gives (1,3): the SKIP position from the failed
  call is dropped, and the next attempt starts one character on. Perl, Boost, upstream and the port
  give None, because the SKIP ends the attempt and moves the start to 2.
- **PCRE2**, pcre2api, "PCRE2_START_OPTIMIZE_OFF": "Disabling start-up optimizations may change the
  outcome of a matching operation." That explains default JIT on ap1 and ap3.
- **Perl**, perlre 5.42 `(*PRUNE)`: "should B not match, then no further backtracking will take
  place, and the pattern will fail outright at the current starting position." perlre gives no
  exception for groups, quantifiers or subroutines. Its departures in section 3 therefore go beyond
  its own text.
- **Boost**, syntax_perl.qbk: "(*PRUNE) Has no effect unless backtracked onto, in which case all the
  backtracking information prior to this point is discarded", and "There may also be detail
  differences in behaviour between this library and Perl, not least because Perl's behaviour is
  rather under-documented and often somewhat random in how it behaves in practice."

## 3. The model, construct by construct

"Unwinds to X" means backtracking onto the verb abandons everything back to X. X then fails, succeeds
or ends the attempt, as stated in the cell.

| Construct the verb is in (innermost first) | PCRE2 (no-optimisation model) | Upstream, port (a48b9d3) | Perl 5.42/5.38 | Boost |
| --- | --- | --- | --- | --- |
| Top level, no group | attempt fails | same | same | same |
| Unfinished atomic group, possessive repeat, alone or nested in each other | transparent: attempt fails (b02-b06, b12, d1, cg1, alt1-2, ps2-3, gr3) | innermost group fails | as PCRE2 | as PCRE2 |
| Unfinished positive lookahead or lookbehind, alone or nested | transparent: attempt fails (b07, b08, alt3, va3-4, ss1, fl1, n01-2, pb2) | assertion false | as PCRE2 | assertion false |
| Negative lookaround, verb directly inside | assertion true, no further branches (b10, va1-2, fl2, nl1) | same (nl1r) | as PCRE2 | as PCRE2, but a SKIP position leaks out when the assertion becomes true (sn1, sn2) |
| Atomic or positive lookaround nested in a negative lookaround | transparent: negative assertion true (n03, n04, nl2, d2, nb1-3, sn0) | innermost group fails (None on n03, n04, d2, nb1-3, sn0) | as PCRE2 | attempt fails (n03, n04, nl2, d2, nb1-3, sn0) |
| Negative lookaround nested in a negative lookaround | inner assertion true (nn1) | same | **outer assertion true** (nn1) | as PCRE2 |
| Conditional, positive test | condition false (b11, c1) | same | as PCRE2, but a SKIP position leaks out when the condition is false (sc1) | as PCRE2, but a SKIP position leaks out (sc1) |
| Conditional, negative test | condition true (cn1; control cn1c gives (0,2)) | same | as PCRE2 | as PCRE2 |
| Negative lookaround or conditional nested in a positive group (atomic, lookahead, lookbehind) | stops at the negative or conditional (n05, c2, d3, d4, d5, pb1) | same answers on n05, c2, pb1; differs on d3-d5 only through the innermost-group rule | **escapes it** (n05, c2, d3, d4, d5, pb1) | as PCRE2 except d3-d5 |
| Negative lookaround in a conditional test | stops at the negative (cn3) | same | **escapes to the condition** (cn3) | as PCRE2 |
| Conditional nested in a negative lookaround | stops at the conditional (cn4) | same | **escapes to the negative** (cn4) | as PCRE2 |
| Verb in a conditional's branch, not its test | transparent (cb1, cb2) | same | same | same |
| Atomic group under a non-possessive quantifier, then a later failure | attempt fails (x07, x07c-e, lz3, gr2) | group fails, fallback taken | **confined** under `?`, `{0,1}`, `*` (x07, x07c-e) | as PCRE2 |
| Verb under `*`, `*?`, `+` or `+?` inside an atomic group or possessive repeat | attempt fails (rep1-3, lz1-2, lz4, gr1, ps1) | group fails | **confined** (rep1-3, lz1-2, lz4, gr1) | as PCRE2 |
| Positive lookahead under `?` | attempt fails (x07f) | assertion false | as PCRE2 | assertion false |
| Called group (subroutine or recursion) | the call fails; backtrack at the outer level; SKIP position discarded (rc1-5, sr1, sr3) | **transparent: attempt fails** | **transparent** | **transparent** |
| Finished atomic group, possessive repeat | verb unreachable (b14, x02, f03) | same on a48b9d3; main moves the next start for SKIP (f01) | as PCRE2 | SKIP moves the next start (f01) |
| Finished lookaround | verb unreachable (b15, x03, x04, f02, f04) | same | as PCRE2 | **leaks** (b15, x03, f02) |

What this shows:

- PCRE2 is the only engine consistent across every cell. Its three targets are all constructs whose
  failure has a meaning of its own: a negative assertion's failure is its success; a condition's
  failure picks the other branch; a called group's failure returns to the caller.
- Perl departs in ten cells:
  - quantified groups;
  - a negative lookahead or conditional nested in a positive group;
  - a negative lookahead nested in a negative one or in a conditional test;
  - a conditional nested in a negative lookahead;
  - SKIP in a condition that turns out false;
  - called groups.

  perlre says nothing about any of these, and pcre2pattern itself notes that "Perl's treatment of the
  other verbs in subroutines is different in some cases".
- Boost departs in the lookaround, nested-negative, SKIP-leak, called-group and finished-lookahead
  cells, and its documentation disclaims exact Perl compatibility.
- On called groups, PCRE2 stands alone: Perl, Boost, upstream and the port all treat them as
  transparent. PCRE2's rule for them is documented and deliberate.

## 4. Principles

**The case for PCRE2's model.**

- A verb means "if backtracking ever reaches me, stop this attempt" (perlre, pcre2pattern, Boost).
- An atomic group, possessive repeat or positive lookaround means "once I have matched, never
  backtrack into me". While it is still matching, that promise has not taken effect, so backtracking
  onto the verb is an ordinary backtrack and the verb should do what it always does. Confinement
  after the group finishes follows from atomicity; it is not a separate rule.
- The exceptions are exactly the constructs whose failure is itself an answer:
  - a negative assertion, whose failure is its success;
  - a condition, whose failure picks the other branch;
  - a called group, whose failure is a return to the caller, much like a function. Compare Prolog's
    cut, which removes choices only back to its own predicate call.
- Quantifiers change nothing: a `?` offers a fallback, but the verb forbids backtracking to it.

The verbs come from Perl 5.10, and PCRE is where most users learn them. Upstream's feature request
cites PCRE's documentation and rexegg.

**The case for upstream's model.** Each atomic group, possessive repeat, lookaround and conditional
test becomes a scope for verbs. That is composable: wrapping a verb-using subpattern in `(?>...)`
guarantees it cannot abort the caller. It is simple to implement (one stack of marks) and the README
documents it. No other engine measured behaves this way. Perl confines only under quantifiers, and
Boost only in positive lookarounds; neither documents it. Oddly, upstream does *not* scope called
groups, the one place PCRE2 does.

**What users would expect.** Someone who knows the verbs from Perl or PCRE expects the attempt to
fail inside atomic groups and positive lookarounds; both engines agree there. The finer cells
(nested negatives, quantifiers, called groups) are where Perl and PCRE2 part. There PCRE2 is the one
engine that documents its rule and follows it.

## 5. Performance

Today confinement costs nothing extra: `TopBstack` cuts to the innermost mark, and the construct's own
backtrack entry does the rest.

What PCRE2's model needs in the port:

1. **Target.** Each verb's unwind target is its innermost enclosing negative lookaround, conditional
   test or called group, else the attempt. The first two are known statically, so the compiler can
   give the verb node its target kind, or a separate opcode when it sits inside a transparent
   construct. A called group is dynamic, because the call site is what counts. That needs a mark
   pushed at group-call entry (`CallRef`) in patterns whose called groups contain verbs. Verb-free
   patterns and top-level verbs keep today's code path.
2. **Timing.** `(*PRUNE)` would push a one-byte backtrack entry when it runs inside a transparent
   construct, as `(*SKIP)` already does since a48b9d3. The new backtrack arm cuts the stacks to the
   target mark. It runs at most once per attempt, per assertion evaluation or per call.
3. **Unwind duties.** The jump bypasses the backtrack arms of the constructs it crosses, so it must do
   what those arms would have done:
   - restore the slice, which a lookaround widens to the whole text (Matcher.cs:8588);
   - call `CloseCallsAbove`;
   - hand over to the target construct exactly as its own failure path does today;
   - for a called group, discard any SKIP position recorded inside it (sr1).
   `FAILURE` already resets captures, guards and the three stacks when the target is the attempt.
4. **Lookbehind SKIP.** A `(*SKIP)` in a lookbehind can record a position left of the attempt start.
   PCRE2 treats that as `(*PRUNE)`. The existing slice clamp in `FAILURE` should cover it, but it
   needs a test.

Estimated cost:

- Verb-free patterns: zero.
- Patterns with verbs: one push per verb executed inside a transparent construct, one mark per call
  when the called group has verbs, and one stack cut per backtrack onto a verb.
- Net: neutral to slightly faster, because attempts end sooner (b12 and b12b try two more branches
  under confinement).

The failure memo is not affected: `Optimiser.KeepFailureMemosSound` already turns it off for any
pattern with `(*PRUNE)` or `(*SKIP)` (PatternObject.cs:64-72). Code size is roughly 80 to 130 lines
in Matcher.cs plus compiler support. The oracle needs a switch like `SkipMovesTheSliceWhenItRuns`.

## 6. Recommendation

Options:

- **A. Keep upstream's model and pin it as a divergence.**
  - Pros: no code change; upstream-compatible; composable scopes; matches the README.
  - Cons: no other engine agrees in the atomic, possessive and nested cells, and PCRE2 and Perl both
    disagree in the positive lookaround cells; patterns ported from PCRE, PHP or Perl change meaning
    silently (220 of 10,000 grid rows); inconsistent with ledger 45, which already adopted "a verb
    acts when backtracked onto" on PCRE2's and Perl's authority.
- **B. Adopt PCRE2's no-optimisation model in full, with the three targets.**
  - Pros: the only consistent model measured; documented by pcre2pattern; identical across PCRE2's
    interpreter and JIT once start optimisations are off; completes ledger 45; zero cost for
    verb-free patterns.
  - Cons: diverges from upstream and its README; about 80 to 130 lines of careful unwind code; the
    called-group cell diverges from Perl, Boost and upstream, all of which agree with each other
    there.
- **B-minus-calls. PCRE2's model with called groups left transparent.** This matches Perl, Boost and
  upstream on the rc and sr rows, and saves the call-site mark. But it has no single reference: no
  engine combines PCRE2's other cells with transparent calls, and PCRE2's own documentation describes
  the called-group target.
- **C. A hybrid** following Perl's quantified-group confinement or Boost's lookaround confinement.
  Neither is documented, and each contradicts its engine's own general rule. Reject.

Recommendation: **B in full**, as a deliberate divergence with its own ledger entry, built on
a48b9d3 (ledger 45), which it depends on for the SKIP timing.

- Every cell has one documented reference, PCRE2, and a clear rule.
- In the atomic, possessive and positive-lookaround cells Perl agrees too.
- The called-group cell is the one judgement call. The owner may prefer B-minus-calls because Perl,
  Boost and upstream agree there; it is flagged for that decision.

Next steps: pin every row in the section 3 table as a test, and draft an upstream report for the
ledger (README wording, PCRE2 and Perl comparison) for owner approval, not for filing now.

**Port rows that change under B**, measured from a48b9d3 (64 rows; each goes from the "a48b" column
to the "PCRE2" column, except b09r, noted below):

- Unfinished atomic group or possessive repeat: b01, b02, b03, b04, b05, b06, sk1, sk2, x06, alt1,
  alt2, ap1, ap2, b12, b12b, d1, cg1, gr3, ps2, ps3
- Unfinished positive lookaround: b07, b08, alt3, va3, va4, ss1, fl1, n01, n02, pb2
- Groups nested in a negative lookaround: n03, n04, d2, nb1, nb2, nb3, sn0
- Negative lookaround inside a positive group: d3, d4, d5
- Quantified: p03, x07, x07c, x07d, x07e, x07f, rep1, rep2, rep3, lz1, lz2, lz3, lz4, gr1, gr2, ps1
- Called groups (drop these for B-minus-calls): rc1, rc2, rc3, rc4, rc5, sr1, sr3
- Lookbehind, in the port's direction: b09r goes from (0,1) to None

**Correction to the previous revision.** The earlier list included b09, lb1 and lb2. They do not
change. Upstream runs a lookbehind body right to left (the maintainer in issue 153: "Lookbehinds
should be read in reverse order"), so in those rows the port fails before it reaches the verb. The
same holds for nl1, nl1r, cl1, cl1r, cn2 and lbs1, which differ from PCRE2 only because of direction.
b09r is the reverse case: PCRE2 never reaches its verb but the port does, so under B the port's
answer becomes None.

x01 and f01 also change from main, but ledger 45 already changes them. Every one of the 118 rows
follows the section 3 table.

## Addendum 2026-09-29: quantifiers that can exit empty (D31)

D31 was reported as a bug: `(?m)(?> |(?>(*SKIP)\ba|ab)\w(?:\Z|.))*+(?>\B|(?>aa\m|(*SKIP)a)(?:a*?|\X)(?: ?+|a$))?`
matched against '\n' is None in the port and (0,0) upstream. It is this survey's model at work, not
a defect. The shrunk repro is `((?>(*SKIP)\$))?`: backtracking onto the verb inside the unfinished
atomic group ends the attempt, and the `?` around the group does not catch it. With
`PatternObject.VerbsAreConfinedToTheInnermostGroup` set, the port gives upstream's (0,0) on every row
below, so the oracle's `verb-unwinds-through-unfinished-groups` entry covers them.

Search answers. PCRE2 10.47 via pip `pcre2` 0.7.1, the same with the JIT and with the interpreter
plus NO_START_OPTIMIZE; upstream is regex 2026.9.10; Perl is 5.42.3.

| Row | Pattern | Subject | PCRE2 | Perl | Upstream | Port |
|-----|---------|---------|-------|------|----------|------|
| q1 | `(?>(*SKIP)a)?` | 'b' | None | (0,0) | (0,0) | None |
| q2 | `(?>(*PRUNE)a)?` | 'b' | None | (0,0) | (0,0) | None |
| q3 | `(?>(*SKIP)a)*` | 'ab' | None | (0,1) | (0,1) | None |
| q4 | `(?>(*SKIP)a)*+` | 'ab' | None | None | (0,1) | None |
| q5 | `(?>(*PRUNE)a)*` | 'ab' | None | (0,1) | (0,1) | None |
| q6 | `(?>(?>(*SKIP)a))?` | 'b' | None | (0,0) | (0,0) | None |
| q7 | `((?>(*SKIP)\$))?` | 'b' | None | (0,0) | (0,0) | None |
| q8 | `(?>(*SKIP)a)?` | 'ab' | (0,1) | (0,1) | (0,1) | (0,1) |
| q9 | `(?:(*SKIP)a)?` | 'b' | None | (0,0) | None | None |
| q10 | D31's row without `\m` (PCRE2 has none) | '\n' | None | None | (0,0) | None |

Perl is not consistent with itself here: `(?>(*PRUNE)a)?` over 'b' is (0,0), while
`(?>(*PRUNE)a)*+` over the same text is None. It also gives (0,0) on q9, where no atomic group is
involved and upstream agrees with PCRE2, against perlre's own rule that backtracking onto (*SKIP)
fails the match at the current start. So Perl's `?` does not carry a verb's failure outward. PCRE2
follows its documentation on every row, so the rows are pinned to PCRE2 in `VerbScopeTests`, as the
rest of this survey is.
