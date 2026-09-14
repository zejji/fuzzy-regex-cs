# DRAFT - NOT FILED

Ledger entry 13. Written by S47c on 2026-09-14 against `regex` 2026.9.10.

**Nothing is filed on mrab-regex until everything else in the plan is done** (owner decision,
2026-09-12). This file is the text that would be filed, held here for the owner to approve and
for re-verification against whatever release is current when that day comes. Do not post it.

---

## BESTMATCH loses a partial match that the same compiled pattern's own `match` still finds

**Version:** regex 2026.9.10, CPython 3.14.6, Windows x64.

### Reproduction

```python
>>> import regex
>>> p = regex.compile(r'(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)')
>>> p.search('ab.', partial=True)
None
>>> p.match('ab.', 2, partial=True)
<regex.Match object; span=(2, 3), match='.', fuzzy_counts=(1, 0, 0), partial=True>
>>> regex.compile(r'(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('ab.', partial=True)
<regex.Match object; span=(0, 3), match='ab.', partial=True>
```

The same compiled pattern answers nothing from `search` and a partial from its own anchored `match`
at a position inside the searched region. Deleting `(?b)` returns the `(0, 3)` partial the flag
refused. Four conditions are each necessary on this shape: `(?b)` (`(?e)` in its place keeps the
match), a fuzzy section, a `(*SKIP)`, and `partial=True`.

The reversed form fails the same way:

```python
>>> regex.compile(r'(?b)(?r)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('.ab', partial=True)
None
>>> regex.compile(r'(?r)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('.ab', partial=True)
<regex.Match object; span=(0, 3), match='.ab', fuzzy_counts=(1, 0, 0), partial=True>
```

### Cause

A slice narrowed by `(*SKIP)` during the normal attempt leaks into the partial retry, and
`do_best_fuzzy_match`'s scan-loop guard then refuses to run at all. Traced in a `/Od /Zi` build of
2026.9.10 with `fprintf` instrumentation; line numbers are that source.

1. `_regex.c:14555` - `RE_OP_SKIP` sets `state->slice_start = state->text_pos` (`slice_end` at
   `:14553` when the node is `RE_STATUS_REVERSE`). On `'ab.'` that is `slice_start 0 -> 3`, during
   the normal, non-partial attempt, which then fails.
2. `_regex.c:18170` - `do_match`'s partial fallback restores `text_pos` alone. The second attempt
   therefore starts with `text_pos=0` and `slice=[3,3]`.
3. `_regex.c:17625` - `do_best_fuzzy_match`'s scan loop is
   `while (state->slice_start <= start_pos && start_pos <= state->slice_end)`. With `slice_start=3`
   and `start_pos=0` the guard is false, the body never executes, and `status` keeps the
   `RE_ERROR_FAILURE` it was initialised with at `:17599`. The partial is never attempted.

`do_simple_fuzzy_match` is handed the same leaked slice and still answers, because it has no such
guard - which is why the defect looks like a property of `BESTMATCH` when it is a property of the
leak plus that one loop guard.

`do_enhanced_fuzzy_match` restores the slice before every return that is not a hard error (`:18003`;
the `goto error` at `:18001` skips it). `do_best_fuzzy_match` restores it only inside its
`found_match && fewest_errors > 0` branch (`:17848`), so an attempt that merely fails leaks.

### Suggested fix

Either of these makes all the reproductions above return the match, and both leave the test suite in
`regex/tests/test_regex.py` at 101 run, 0 failures, 0 errors.

**A - restore the slice with `text_pos`, in `do_match`:**

```diff
         int partial_side;
         Py_ssize_t text_pos;
+        Py_ssize_t slice_start;
+        Py_ssize_t slice_end;
 
         partial_side = state->partial_side;
         text_pos = state->text_pos;
+        slice_start = state->slice_start;
+        slice_end = state->slice_end;
@@
             /* Fall back to the partial match as originally requested. */
             state->text_pos = text_pos;
+            state->slice_start = slice_start;
+            state->slice_end = slice_end;
```

**B - restore the slice on every exit from `do_best_fuzzy_match`**, as `do_enhanced_fuzzy_match`
already does: save `state->slice_start` and `state->slice_end` into locals at the top of the
function and put them back before each `return status`.

A is the broader reading - it also resets the non-BESTMATCH retry, which changes one answer we have
found (a `(?r)` fuzzy pattern that gains a substitution, an insertion and a deletion and a capture
it did not previously report). B is confined to `BESTMATCH`.

### How this was found

A differential test between `regex` and a C# port of it, over generated patterns. The port restores
both slice bounds at the equivalent place - it is fix A - and has done since before this mechanism
was traced, on the grounds that a search which reports nothing where its own anchored `match`
reports a partial is self-refuting whatever the ranking rule is.
