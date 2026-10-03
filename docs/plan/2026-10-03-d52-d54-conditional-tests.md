# D52 and D54: a conditional's lookaround test (design note, 2026-10-03)

Branch `maint/d52-d54-conditionals`, plan item 3 of `2026-09-30-correctness-plan.md`. Both defects
sit in the one construct, `(?(test)yes|no)` with a lookaround as the test, and the grid below
measures them together and apart.

## D52: a fuzzy budget removes an exact match

**Witness.** `(?:(?(?=a)ab|b)){s<=1}` searched over 'b' is None here and upstream; without the
budget it is (0, 1).

**Cause.** The test is compiled with the enclosing section's fuzziness:
`LookAroundConditional.CompileCore` called `Subpattern.Compile(Behind, fuzzy)`
(`src/FuzzyRegex/Parsing/Nodes.cs:4401` on main), ported from `upstream/regex/_regex_core.py:3267`.
A plain lookaround compiles its body exact (`Nodes.cs:4242`, upstream `:3201`). So `(?=a)` holds at
'b' by a substitution, the yes branch `ab` cannot fit, and the test is atomic: `END_CONDITIONAL`
drops the test's backtrack points (`Matcher.cs` forward arm, upstream `_regex.c:12356-12384`), so
the exact reading, which would take the no branch, is never tried. Nothing in `_regex.c` is wrong
on its own; the fuzzy flags on the test's nodes are the whole cause.

**Fix.** Compile the test exact, and fix its calls exact (`FixGroups`), as a plain lookaround does.
A fuzzy section written inside the test, `(?(?=(?:ab){e<=1})...)`, still compiles fuzzy, because
the inner section sets its own fuzziness. The branches keep the section's fuzziness. With the test
exact, its answer is the same with and without the budget, and each branch can only gain matches,
so the budget can only add matches.

**Ablation for the oracle.** `Info.UpstreamFuzzyConditionTest` (compile time, through
`FuzzyRegex.WithDefaultVersion`), read as `LookAroundConditional.UpstreamFuzzyTest`.

## D54: captures of a failed negative test

**Witness.** `(?(?!(a))x|\1b)` over 'ab' is None here and upstream; PCRE2 10.47 and Perl 5.42.3
answer (0, 2) with group 1 = 'a' (survey 2026-10-03, `tools/matrix/survey.py`; the .NET BCL drops
them in both forms). Owner ruling 2026-09-30 on OPEN-2: keep them.

**Cause.** `Matcher`'s forward `END_CONDITIONAL` arm, negative case (main `Matcher.cs:10239-10270`,
upstream `_regex.c:12423-12432`), pops the capture change, fuzzy state, repeats and captures saved
at `CONDITIONAL` before going to the no branch.

**Fix.** The arm still restores the fuzzy state and repeats, then leaves the captures as the test
left them, re-pushes the repeats and fuzzy state (now equal to the saved ones) and the saved
capture change on top of the saved captures, and pushes `END_CONDITIONAL`. The block is then
exactly what the positive case leaves, so the existing backtrack arm restores everything when the
no branch fails. `PatternObject.DropNegativeTestCaptures` (oracle only) runs upstream's arm.

**Scope chosen: captures only, not errors.** The ruling's stated equivalence,
`(?(?!X)A|B)` = `(?(?=X)B|A)`, would also keep the errors of a fuzzy section written inside X,
because the positive twin keeps them. That contradicts D45 (ledger entry 32), whose pinned test
`FuzzyBestMatchTests.Bestmatch_does_not_count_the_errors_of_a_section_that_was_thrown_away`
asserts that a failed negative condition's section errors are thrown away. Keeping them moved that
test from (1, 3) to (0, 1) on `(?b)(?:(?(?!(?:a){e<=1})c|d)c|(?:bc){e<=1})` over 'cdcx'. So the
fix keeps the captures, which is what the ruling names, and leaves the errors where D45 put them.
Witness of the remaining asymmetry: `(?(?!((?:ab){e<=1}))b|\1)` over 'ax' is (0, 2) with no error,
group 1 = (0, 2); the positive twin `(?(?=((?:ab){e<=1}))\1|b)` counts one substitution. This is
reported to the owner as an open question, not built around.

## Constructs that share the code path

| Construct | D52 | D54 |
|---|---|---|
| Lookbehind tests | same compile call (`Behind` only picks direction) | same arm |
| Positive tests | changed: the test compiles exact | unchanged (already keeps captures) |
| Group-reference conditions `(?(1)...)` | different node (`Conditional`), unaffected | unaffected |
| Reverse `(?r)` | same; the test's direction is its own | same arm |
| Partial | the test is exact, so it reads the edge as a plain lookaround does | same arm |
| BESTMATCH, ENHANCEMATCH | ranking sees the exact test's (zero) errors | unchanged |
| Captures inside fuzzy sections | captures in the test are exact now | kept |
| Calls inside the test | fixed exact (`FixGroups`) | a call's own captures still go on return (D51) |

## Grid

6,000 rows (`.scratch/d52d54/gen.py`, seed 52): lookahead and lookbehind tests, positive and
negative, eleven test bodies with and without groups, eleven branch forms including
backreferences into the test, fourteen wrappers (no budget, `{e<=1}`, `{s<=1}`, `{i<=1}`, `{d<=1}`,
`{e<=2}`, repeats, alternations), flags none, `(?b)`, `(?e)` and `(?r)`, search, match, fullmatch
and finditer, 10% partial. Port answers on a Debug build through `tools/matrix/port-runner.cs`
(20M step cap, 2 s per call, 20 s per row, 2 GB heap limit), with the failed-call and repeat
memos forced off and on. Upstream answered three ways (`.scratch/d52d54/up.py`): U as released, A
with the test patched exact, J = A on the row's positive twin (the D54 ruling's equivalence).

Rows per construct: negative lookahead 1,795, negative lookbehind 1,833, positive lookahead 1,220,
positive lookbehind 1,152; reverse 963, BESTMATCH 1,032, ENHANCEMATCH 972, partial 581. No row
asserted, hit the cap or timed out, and the memo variants agree with the base answer on every row.

| Change versus main | Rows |
|---|---|
| D52 alone (D54 ablated) | 1,097 (all in fuzzy rows: 281, 299, 275, 242 by the four test kinds) |
| D54 alone (D52 ablated) | 922 (94 and 63 of them in exact rows) |
| Both | 1,494 (reverse 261, BESTMATCH 214, ENHANCEMATCH 224, partial 147) |

Every changed row is accounted for:

- 1,417 now equal J exactly.
- 68 equal J once an already pinned divergence is ablated as the oracle ablates it (`askPinned`):
  ledger entry 44 on 44 rows, entry 42 on 13, rule H' on 10, the doubled-insertion guard on 1.
- 9 differ from J only because the positive twin swaps two groups that are both in the branches
  (`(?(?!a)(b)|(b))`); each equals A, and its kept test captures follow the ruling.

Thirty rows that equalled J on main differ from it now; all 30 are among the 77 above. Positive conditionals outside fuzzy sections
changed on 0 rows, as they must. On the port itself, every negative row answers as its positive
twin except the 17 group-swap rows.

The 102 changed rows with no fuzzy section or fuzzy flag were surveyed (`tools/matrix/survey.py`,
PCRE2 10.47, Perl 5.42.3, .NET 10, node, re): 61 match PCRE2 and Perl exactly; 38 neither can
express (reverse or partial); 3 match PCRE2 where Perl differs (two finditer rows where Perl's
empty-match iteration differs on main too, and `(?(?<!(ab|a))\1b|)` over 'baxa', where Perl's
variable-length lookbehind answers None and PCRE2 (2, 2) with group 1 (1, 2), as here).

## Oracle

Two entries, sitting right after D51's, each keyed on its own switch alone (as D51's is), and every
other named ablation now also switches both fixes off (`OracleComparer.Run`), because upstream has
both behaviours on every row; without that, `group-call-runs-with-its-call-sites-features` lost its
example row 8, `(?P<g1>(?P<g2>(?(?<!(?P>g3))bb|b)b))(?:(?P<g3>b)){e<=1}`. Seeds 7, 4242 and
20260927: GREEN, diverge 0; the D54 entry claims 2 wave rows at each seed and the D52 entry none
(the wave generators put no conditional inside a fuzzy section).

## Performance

D52 compiles fewer fuzzy nodes, never more. D54 adds a pop-and-push of the repeats and fuzzy state
on the one path where a negative test's body matched, which already popped them; the positive path
and every pattern without a lookaround conditional run the same instructions as before.
