using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The fuzzy spine: one substitution, one insertion, one deletion, the counts and the change
/// positions each produces, the cost equation, reverse matching, partial matching and a nested
/// fuzzy section.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-09-13 by
/// <c>tools/probes/upstream-fuzzy-spine.py</c>, and its line is quoted beside the assertion.
/// Upstream reports change positions as codepoint indices and this port reports UTF-16 code units,
/// so the two differ only on an astral subject - which is why one is pinned below.
/// </para>
/// <para>
/// <b>No two literal characters ever sit next to each other in these patterns</b>, and that is the
/// point rather than an accident of taste. <c>Sequence.pack_characters</c>
/// (<c>upstream/regex/_regex_core.py:3526</c>) packs a run of two or more <c>Character</c> items
/// into one <c>STRING</c> node, and a fuzzy <c>STRING</c> is S39's work. So <c>(?:[ab][cd])</c> is
/// two one-character items where <c>(?:ab)</c> is one string, and only the first is in this slice.
/// </para>
/// </remarks>
public sealed class FuzzyMatchingTests
{
    [Test]
    public void A_substitution_is_counted_and_its_position_recorded()
    {
        // sub match('(?:[ab][cd][ef]){e<=1}', 'acx'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        Match m = new FuzzyRegex("(?:[ab][cd][ef]){e<=1}").MatchAtStart("acx");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(Substitutions: 1, Insertions: 0, Deletions: 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    public void An_insertion_is_counted_and_its_position_recorded()
    {
        // ins fullmatch('(?:[ab][cd]){e<=1}', 'axc'): span=(0, 3) counts=(0, 1, 0) changes=([], [1], [])
        // The insertion is in the middle of the section, so it is fuzzy_match_item's INS arm.
        Match middle = new FuzzyRegex("(?:[ab][cd]){e<=1}").FullMatch("axc");

        middle.Success.Should().BeTrue();
        (middle.Index, middle.Length).Should().Be((0, 3));
        middle.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        middle.FuzzyChanges.Insertions.Should().Equal(1);

        // ins fullmatch('(?:[ab][cd]){e<=1}', 'acx'): span=(0, 3) counts=(0, 1, 0) changes=([], [2], [])
        // The insertion is after the section, which only END_FUZZY's backtrack arm can supply.
        Match trailing = new FuzzyRegex("(?:[ab][cd]){e<=1}").FullMatch("acx");

        (trailing.Index, trailing.Length).Should().Be((0, 3));
        trailing.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        trailing.FuzzyChanges.Insertions.Should().Equal(2);
    }

    [Test]
    public void A_deletion_is_counted_and_its_position_recorded()
    {
        // del match('(?:[ab][cd][ef]){e<=1}', 'ae'): span=(0, 2) counts=(0, 0, 1) changes=([], [], [1])
        Match m = new FuzzyRegex("(?:[ab][cd][ef]){e<=1}").MatchAtStart("ae");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);

        // twodel match('(?:[ab][cd][ef][gh]){e<=2}', 'ag'): counts=(0, 0, 2) changes=([], [], [1, 2])
        // Both deletions happen at position 1; the second is reported at 2 because
        // match_fuzzy_changes (:20535) shifts each deletion by the number recorded before it, so the
        // positions read as places in a string with the missing characters put back.
        Match two = new FuzzyRegex("(?:[ab][cd][ef][gh]){e<=2}").MatchAtStart("ag");

        (two.Index, two.Length).Should().Be((0, 2));
        two.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        two.FuzzyChanges.Deletions.Should().Equal(1, 2);
    }

    [Test]
    public void Each_error_kind_can_be_constrained_on_its_own()
    {
        // s<=1 match('(?:[ab][cd][ef]){s<=1}', 'acx'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        Match sub = new FuzzyRegex("(?:[ab][cd][ef]){s<=1}").MatchAtStart("acx");
        (sub.Index, sub.Length).Should().Be((0, 3));
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

        // s<=0 match('(?:[ab][cd][ef]){s<=0}', 'acx'): None
        new FuzzyRegex("(?:[ab][cd][ef]){s<=0}")
            .MatchAtStart("acx")
            .Success.Should()
            .BeFalse();

        // i<=1 fullmatch('(?:[ab][cd]){i<=1}', 'axc'): counts=(0, 1, 0) changes=([], [1], [])
        Match ins = new FuzzyRegex("(?:[ab][cd]){i<=1}").FullMatch("axc");
        ins.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        ins.FuzzyChanges.Insertions.Should().Equal(1);

        // d<=1 match('(?:[ab][cd][ef]){d<=1}', 'ae'): counts=(0, 0, 1) changes=([], [], [1])
        Match del = new FuzzyRegex("(?:[ab][cd][ef]){d<=1}").MatchAtStart("ae");
        del.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        del.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void The_cost_equation_and_not_the_error_count_decides_which_error_is_affordable()
    {
        // cost match('(?:[ab][cd][ef]){2s+1d<=1}', 'acx'): span=(0, 2) counts=(0, 0, 1)
        //   changes=([], [], [2])
        // A substitution costs 2 against a budget of 1, so the third item cannot be substituted for
        // the 'x'. Deleting it costs 1 and matches the two-character prefix instead.
        Match m = new FuzzyRegex("(?:[ab][cd][ef]){2s+1d<=1}").MatchAtStart("acx");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(2);

        // cost match('(?:[ab][cd][ef]){2s+1d<=1}', 'ae'): span=(0, 2) counts=(0, 0, 1)
        //   changes=([], [], [1])
        Match deleted = new FuzzyRegex("(?:[ab][cd][ef]){2s+1d<=1}").MatchAtStart("ae");

        (deleted.Index, deleted.Length).Should().Be((0, 2));
        deleted.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        deleted.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void Properties_ranges_negated_classes_and_the_dot_fuzz_like_a_class()
    {
        // prop match(r'(?:\w\w\w){e<=1}', 'ab!'): counts=(1, 0, 0) changes=([2], [], [])
        Match prop = new FuzzyRegex(@"(?:\w\w\w){e<=1}").MatchAtStart("ab!");
        prop.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        prop.FuzzyChanges.Substitutions.Should().Equal(2);

        // range match('(?:[a-c][a-c]){e<=1}', 'ax'): counts=(1, 0, 0) changes=([1], [], [])
        Match range = new FuzzyRegex("(?:[a-c][a-c]){e<=1}").MatchAtStart("ax");
        range.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        range.FuzzyChanges.Substitutions.Should().Equal(1);

        // neg match('(?:[^x][^x]){e<=1}', 'ax'): counts=(1, 0, 0) changes=([1], [], [])
        Match negated = new FuzzyRegex("(?:[^x][^x]){e<=1}").MatchAtStart("ax");
        negated.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        negated.FuzzyChanges.Substitutions.Should().Equal(1);

        // dot fullmatch('(?:...){e<=1}', 'abcd'): span=(0, 4) counts=(0, 1, 0) changes=([], [3], [])
        Match dot = new FuzzyRegex("(?:...){e<=1}").FullMatch("abcd");
        (dot.Index, dot.Length).Should().Be((0, 4));
        dot.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        dot.FuzzyChanges.Insertions.Should().Equal(3);
    }

    [Test]
    public void A_zero_width_item_inside_a_fuzzy_section_can_only_be_stepped_over_by_an_insertion()
    {
        // A zero-width item passes a step of 0 to fuzzy_match_item (:10185), which rules out both
        // deletion and substitution outright (:10128, :10159), so an insertion is the only error
        // that can get past a failing assertion - and it moves the position rather than the node.

        // zw match(r'(?:\b[fg][op]){e<=1}', 'xfo'): span=(0, 3) counts=(0, 1, 0) changes=([], [0], [])
        Match boundary = new FuzzyRegex(@"(?:\b[fg][op]){e<=1}").MatchAtStart("xfo");
        (boundary.Index, boundary.Length).Should().Be((0, 3));
        boundary.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        boundary.FuzzyChanges.Insertions.Should().Equal(0);

        // zw search(r'(?:\b[fg][op]){e<=1}', 'xfo'): None
        // Searching refuses the insertion at the search anchor - fuzzy_match_item's
        // 'permit_insertion' is false there (:10214), because starting one character later is
        // cheaper - and every later start position fails the '\b' outright.
        new FuzzyRegex(@"(?:\b[fg][op]){e<=1}")
            .Match("xfo")
            .Success.Should()
            .BeFalse();

        // zw match(r'(?:[fg][op]\b){e<=1}', 'fox'): span=(0, 3) counts=(0, 1, 0) changes=([], [2], [])
        Match trailing = new FuzzyRegex(@"(?:[fg][op]\b){e<=1}").MatchAtStart("fox");
        (trailing.Index, trailing.Length).Should().Be((0, 3));
        trailing.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        trailing.FuzzyChanges.Insertions.Should().Equal(2);

        // zw match(r'(?:[fg][op]$){e<=1}', 'fox'): the same answer for an end-of-string assertion.
        Match dollar = new FuzzyRegex("(?:[fg][op]$){e<=1}").Match("fox");
        dollar.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        dollar.FuzzyChanges.Insertions.Should().Equal(2);

        // zw match(r'(?:^[fg][op]){e<=1}', 'fo'): counts=(0, 0, 0) - nothing needed fuzzing at all.
        new FuzzyRegex("(?:^[fg][op]){e<=1}")
            .MatchAtStart("fo")
            .FuzzyCounts.Total.Should()
            .Be(0);

        // zw search(r'(?:[ab]$[cd]){e<=1}', 'ac'): None. An insertion moves the position, so it
        // cannot help an assertion that is false at every position the section can reach.
        new FuzzyRegex("(?:[ab]$[cd]){e<=1}")
            .Match("ac")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_reverse_fuzzy_match_records_the_position_after_the_character_it_changed()
    {
        // rev search('(?r)(?:[ab][cd]){e<=1}', 'ax'): span=(0, 2) counts=(1, 0, 0) changes=([2], [], [])
        // Reverse: the step is -1, so 'new_text_pos - data.step' is one past the character rather
        // than at it (:10245). The substituted 'x' is at index 1 and the recorded position is 2.
        Match m = new FuzzyRegex("(?r)(?:[ab][cd]){e<=1}").Match("ax");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);

        // rev search('(?r)(?:[ab][cd][ef]){e<=1}', 'zacx'): span=(1, 4) counts=(1, 0, 0)
        //   changes=([4], [], [])
        Match later = new FuzzyRegex("(?r)(?:[ab][cd][ef]){e<=1}").Match("zacx");

        (later.Index, later.Length).Should().Be((1, 3));
        later.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        later.FuzzyChanges.Substitutions.Should().Equal(4);
    }

    [Test]
    public void A_partial_fuzzy_match_reaches_check_fuzzy_partial_and_keeps_the_errors_it_used()
    {
        // part match('(?:[ab][cd][ef][gh]){e<=1}', 'ac', partial=True): span=(0, 2) partial=True
        //   counts=(0, 0, 0)
        Match plain = new FuzzyRegex("(?:[ab][cd][ef][gh]){e<=1}").MatchAtStart("ac", partial: true);

        plain.PartialMatch.Should().BeTrue();
        (plain.Index, plain.Length).Should().Be((0, 2));
        plain.FuzzyCounts.Total.Should().Be(0);

        // part match('(?:[ab][cd][ef][gh]){e<=1}', 'acx', partial=True): span=(0, 3) partial=True
        //   counts=(1, 0, 0) changes=([2], [], [])
        Match substituted = new FuzzyRegex("(?:[ab][cd][ef][gh]){e<=1}").MatchAtStart("acx", partial: true);

        substituted.PartialMatch.Should().BeTrue();
        (substituted.Index, substituted.Length).Should().Be((0, 3));
        substituted.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        substituted.FuzzyChanges.Substitutions.Should().Equal(2);
    }

    [Test]
    public void A_nested_fuzzy_section_adds_its_errors_to_the_outer_budget()
    {
        // nest match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}', 'axeg'): span=(0, 4) counts=(1, 0, 0)
        //   changes=([1], [], [])
        // The [cd] is substituted inside the inner section; END_FUZZY adds the inner counts to the
        // outer ones and the total is still within the outer budget.
        Match m = new FuzzyRegex("(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}").MatchAtStart("axeg");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);

        // nest match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}', 'axey'): None. Two substitutions are
        // within neither budget on its own, and the outer one counts the inner error too.
        new FuzzyRegex("(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=1}")
            .MatchAtStart("axey")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_nested_section_counts_its_own_errors_from_zero_and_not_from_the_outer_ones()
    {
        // The outer section has to spend an error BEFORE the inner one is entered, which is the only
        // shape in which the inner section starting from the outer counts rather than from zero
        // changes an answer. The oracle's `fuzzy` generator does not reach it - S38's control E is
        // silent at both seeds - so it is pinned here instead; `tools/probes/fuzzy-nested-rows.py`
        // builds the same shapes as oracle rows, and 21 of its 24 diverge under that mutation.

        // match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}', 'xxeg'): span=(0, 4) counts=(2, 0, 0)
        //   changes=([0, 1], [], [])
        // The 'x' at 0 costs the OUTER section an error and the 'x' at 1 costs the INNER one its
        // only error. Had the inner started from the outer's count, it would already be spent.
        Match both = new FuzzyRegex("(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}").MatchAtStart("xxeg");

        both.Success.Should().BeTrue();
        (both.Index, both.Length).Should().Be((0, 4));
        both.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
        both.FuzzyChanges.Substitutions.Should().Equal(0, 1);

        // match('(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}', 'xceg'): counts=(1, 0, 0) changes=([0], [], [])
        // The same pattern where only the outer errs, so the inner section spends nothing.
        Match outerOnly = new FuzzyRegex("(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}").MatchAtStart("xceg");

        outerOnly.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        outerOnly.FuzzyChanges.Substitutions.Should().Equal(0);

        // match('(?:[ab][cd](?:[ef][gh]){e<=1}){e<=3}', 'xxxh'): counts=(3, 0, 0)
        //   changes=([0, 1, 2], [], [])
        // Two outer errors before the inner section, and the inner still has its own one to spend.
        Match twoBefore = new FuzzyRegex("(?:[ab][cd](?:[ef][gh]){e<=1}){e<=3}").MatchAtStart("xxxh");

        twoBefore.FuzzyCounts.Should().Be(new FuzzyCounts(3, 0, 0));
        twoBefore.FuzzyChanges.Substitutions.Should().Equal(0, 1, 2);

        // match('(?:[ab](?:[cd][ef]){s<=1}[gh]){e<=3}', 'xxxg'): None. Three substitutions are one
        // more than the two the inner section's own budget can carry between them.
        new FuzzyRegex("(?:[ab](?:[cd][ef]){s<=1}[gh]){e<=3}")
            .MatchAtStart("xxxg")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_partial_match_inside_a_nested_fuzzy_section_counts_the_outer_sections_errors_too()
    {
        // Found by the S38 wave, three rows across three seeds, read then as "report only as many
        // changes as the counts say" and FIXED IN S47 the other way round - ledger entry 11, second
        // door. The counts were the wrong half.
        //
        // `state->fuzzy_counts` is scoped to the innermost OPEN fuzzy section: FUZZY saves the
        // enclosing section's on the sstack (upstream/src/_regex.c:13137) and zeroes it
        // (:13143), END_FUZZY adds
        // the inner back into the outer on the way out (:12473-12484). A partial match returns from
        // inside the section, so none of that unwinding happens and the counter holds the inner
        // section's errors alone - while the change list, which is global, holds every one of them.
        // Upstream then reports the first `sum(counts)` entries of that list (:20522), which does
        // not merely omit a change: it presents the OUTER section's insertion as though it were the
        // INNER section's substitution.
        //
        // match(r'(?:a\w(?:b\w){e<=3}){i<=1}', 'a ba', partial=True), regex 2026.9.10:
        //   span=(0, 4) counts=(1, 0, 0) changes=([], [1], [])
        // One substitution counted, one insertion reported, and no edit script means that. This
        // port's own change list is [ins@1, sub@3] - the outer `{i<=1}` inserted the space and the
        // inner `{e<=3}` substituted the 'a' - so (1, 1, 0) is the answer, and this port now tallies
        // the counts from the changes rather than copying the state's counter. Upstream's list
        // cannot be read past `sum(counts)` entries, so whether its own holds the same two is not
        // measurable from Python; what is measurable is that the one entry it does return is an
        // insertion its own counts deny.
        Match m = new FuzzyRegex(@"(?:a\w(?:b\w){e<=3}){i<=1}").MatchAtStart("a ba", partial: true);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(3);
        m.FuzzyChanges.Insertions.Should().Equal(1);
        m.FuzzyChanges.Deletions.Should().BeEmpty();

        // fullmatch(r'(?:[^a-f](?:\s\p{L}){2i+1d+1s<=2}){e<=2,i<=1}', 'a b', partial=True):
        //   upstream span=(0, 3) counts=(1, 0, 0) changes=([], [0], [])
        Match costed = new FuzzyRegex(@"(?:[^a-f](?:\s\p{L}){2i+1d+1s<=2}){e<=2,i<=1}").FullMatch("a b", partial: true);

        costed.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        costed.FuzzyChanges.Insertions.Should().Equal(0);
        costed.FuzzyChanges.Substitutions.Should().Equal(2);

        // match(r'(?:\B[a-f](?:\Wb){e<=3}){e<=2,i<=1}', 'eab', partial=True):
        //   upstream span=(0, 3) counts=(1, 0, 0) changes=([], [0], [])
        Match boundary = new FuzzyRegex(@"(?:\B[a-f](?:\Wb){e<=3}){e<=2,i<=1}").MatchAtStart("eab", partial: true);

        boundary.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        boundary.FuzzyChanges.Insertions.Should().Equal(0);
        boundary.FuzzyChanges.Substitutions.Should().Equal(2);

        // THE CONTROL, and it is what makes the three rows above a statement about the PARTIAL exit
        // rather than about nesting. The same two nested sections and the same outer insertion, on a
        // subject the pattern COMPLETES on: END_FUZZY runs, merges the inner counts into the outer,
        // and the counter already holds the outer section's insertion with nothing here to
        // reconcile. Upstream agrees on this row, measured 2026-09-14 on regex 2026.9.10:
        //   match(r'(?:a\w(?:b\w){e<=3}){i<=1}', 'a xby') -> (0, 5) (0, 1, 0) ([], [1], [])
        Match completed = new FuzzyRegex(@"(?:a\w(?:b\w){e<=3}){i<=1}").MatchAtStart("a xby");

        completed.Success.Should().BeTrue();
        completed.PartialMatch.Should().BeFalse();
        (completed.Index, completed.Length).Should().Be((0, 5));
        completed.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        completed.FuzzyChanges.Insertions.Should().Equal(1);
    }

    [Test]
    public void A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one()
    {
        // Upstream clears the fuzzy counts at the top of `start_match` (upstream/src/_regex.c
        // :11790-11792), which the FAILURE backtrack arm reaches on every restart. Ordinarily the
        // FUZZY backtrack arm has already put them back, so the clear is invisible - but a verb
        // that cuts the backtracking drops the fuzzy frames instead of unwinding them, and then the
        // counts are still the abandoned attempt's. Found by S38's blind review.
        //
        // UPSTREAM CLEARS THE COUNTS AND NOT THE CHANGE LIST, and the two then contradict each
        // other. S47 clears both, which is ledger entry 11's first door and this port's own line -
        // a cleared count already asserts that a fresh attempt has used no errors, so it can have
        // no changes either. Upstream, regex 2026.9.10, re-run by
        // `python tools/probes/upstream-fuzzy-restart-leak.py`:
        //   search(r'(?:[ab][bc](*PRUNE)[wx]){e<=2}', 'qab') -> (1, 3) (0, 0, 1) ([0], [], [])
        // One DELETION counted and a SUBSTITUTION at 0 reported - the substitution belongs to the
        // attempt at position 0 that `(*PRUNE)` abandoned. The winning attempt at 1 matched 'ab'
        // and deleted the `[wx]` it had run out of subject for, so (0, 0, 1) with a deletion at 3
        // is the edit script, and that is what this port answers.
        Match pruned = new FuzzyRegex("(?:[ab][bc](*PRUNE)[wx]){e<=2}").Match("qab");

        pruned.Success.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((1, 2));
        pruned.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        pruned.FuzzyChanges.Substitutions.Should().BeEmpty();
        pruned.FuzzyChanges.Deletions.Should().Equal(3);

        // search(r'(?:[ab](*SKIP)[bc][wx]){e<=2}', 'qab'): upstream (1, 3) (0, 0, 1) ([0], [], [])
        Match skipped = new FuzzyRegex("(?:[ab](*SKIP)[bc][wx]){e<=2}").Match("qab");

        skipped.Success.Should().BeTrue();
        skipped.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        skipped.FuzzyChanges.Substitutions.Should().BeEmpty();
        skipped.FuzzyChanges.Deletions.Should().Equal(3);

        // THE CONTROL: the same fuzzy section and the same abandoned first attempt with no verb to
        // cut the backtracking, so the FUZZY backtrack arm unwinds the attempt at 0 the ordinary
        // way and both engines agree. Without it these two rows would pass on an engine that had
        // simply stopped recording changes.
        Match unpruned = new FuzzyRegex("(?:[ab][bc][wx]){e<=2}").Match("qab");

        unpruned.Success.Should().BeTrue();
        (unpruned.Index, unpruned.Length).Should().Be((1, 2));
        unpruned.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        unpruned.FuzzyChanges.Deletions.Should().Equal(3);
    }

    [Test]
    public void A_POSIX_search_of_a_fuzzy_pattern_answers_where_upstream_crashes()
    {
        // Upstream `regex` 2026.7.19 segfaults on this row - not an exception, the process dies:
        //   regex.search(r'(?p)(?:[ab][bc]){e<=1}', 'ax')   ->  Segmentation fault
        // Both halves are needed: without `(?p)` it answers ((0, 2), (1, 0, 0), ([1], [], [])), and
        // without the fuzzy section it answers ((0, 2), (0, 0, 0), ([], [], [])).
        // `tools/probes/upstream-posix-fuzzy-crash.py` re-runs all three; ledger entry 9.
        //
        // Only "answers rather than crashing" is asserted, and deliberately so: there is no ground
        // truth to pin a span or a count against, and the counts are not trustworthy here anyway -
        // `best_fuzzy_counts` is unported until S42, so a POSIX fuzzy match reports the counts of
        // whichever attempt ran last rather than of the best one.
        Match m = new FuzzyRegex("(?p)(?:[ab][bc]){e<=1}").Match("ax");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
    }

    [Test]
    public void Two_errors_are_reported_in_the_order_they_were_recorded()
    {
        // two match('(?:[ab][cd][ef][gh]){e<=2}', 'axey'): span=(0, 4) counts=(2, 0, 0)
        //   changes=([1, 3], [], [])
        Match m = new FuzzyRegex("(?:[ab][cd][ef][gh]){e<=2}").MatchAtStart("axey");

        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1, 3);
    }

    [Test]
    public void The_fuzzy_counts_survive_an_atomic_group_a_lookaround_and_a_conditional()
    {
        // Each of these pushes the fuzzy counts onto the backtracking stack on the way in and pops
        // them on the way out (ATOMIC :12042, LOOKAROUND :13787, CONDITIONAL :12230), so a count
        // that was not restored would show up here and nowhere else in this file.

        // atomic match('(?:(?>[ab])[cd]){e<=1}', 'ax'): counts=(1, 0, 0) changes=([1], [], [])
        Match atomic = new FuzzyRegex("(?:(?>[ab])[cd]){e<=1}").MatchAtStart("ax");
        (atomic.Index, atomic.Length).Should().Be((0, 2));
        atomic.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        atomic.FuzzyChanges.Substitutions.Should().Equal(1);

        // atomic match('(?:(?>[ab]+)[cd]){e<=1}', 'aax'): None
        new FuzzyRegex("(?:(?>[ab]+)[cd]){e<=1}")
            .MatchAtStart("aax")
            .Success.Should()
            .BeFalse();

        // look match('(?:[ab](?![cd])[ef]){e<=1}', 'ax'): counts=(1, 0, 0) changes=([1], [], [])
        Match look = new FuzzyRegex("(?:[ab](?![cd])[ef]){e<=1}").MatchAtStart("ax");
        look.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        look.FuzzyChanges.Substitutions.Should().Equal(1);

        // look match('(?:[ab](?=[cd])[cd]){e<=1}', 'ax'): None
        new FuzzyRegex("(?:[ab](?=[cd])[cd]){e<=1}")
            .MatchAtStart("ax")
            .Success.Should()
            .BeFalse();

        // cond match('(?:([ab])(?(1)[cd]|[ef])){e<=1}', 'ax'): counts=(1, 0, 0) changes=([1], [], [])
        Match conditional = new FuzzyRegex("(?:([ab])(?(1)[cd]|[ef])){e<=1}").MatchAtStart("ax");
        conditional.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        conditional.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    [Test]
    public void Each_match_of_a_scan_reports_its_own_counts_rather_than_the_running_total()
    {
        // iter finditer('(?:[ab][cd]){e<=1}', 'ac ax'):
        //   [((0, 2), (0, 0, 0), ([], [], [])), ((3, 5), (1, 0, 0), ([4], [], []))]
        MatchCollection matches = new FuzzyRegex("(?:[ab][cd]){e<=1}").Matches("ac ax");

        matches.Should().HaveCount(2);
        (matches[0].Index, matches[0].Length).Should().Be((0, 2));
        matches[0].FuzzyCounts.Total.Should().Be(0);
        (matches[1].Index, matches[1].Length).Should().Be((3, 2));
        matches[1].FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        matches[1].FuzzyChanges.Substitutions.Should().Equal(4);
    }

    [Test]
    public void An_exact_match_of_a_fuzzy_pattern_reports_no_errors_at_all()
    {
        // exact match('(?:[ab][cd]){e<=2}', 'ac'): counts=(0, 0, 0) changes=([], [], [])
        Match m = new FuzzyRegex("(?:[ab][cd]){e<=2}").MatchAtStart("ac");

        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();

        // A pattern with no fuzzy section at all answers the same way rather than throwing.
        Match exact = new FuzzyRegex("foo").MatchAtStart("foo");

        exact.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        exact.FuzzyChanges.Deletions.Should().BeEmpty();

        // And so does an unsuccessful match.
        Match none = new FuzzyRegex("(?:[ab][cd]){e<=1}").MatchAtStart("xy");

        none.Success.Should().BeFalse();
        none.FuzzyCounts.Total.Should().Be(0);
        none.FuzzyChanges.Substitutions.Should().BeEmpty();
    }

    [Test]
    public void Change_positions_on_an_astral_subject_are_UTF16_code_units_where_upstream_reports_codepoints()
    {
        // astral match('(?:.[ab]){e<=1}', '\U0001F600c'): span=(0, 2) counts=(1, 0, 0)
        //   changes=([1], [], [])
        // Upstream's span and change position are codepoint indices; U+1F600 is a surrogate pair, so
        // this port's are (0, 3) and 2.
        Match sub = new FuzzyRegex("(?:.[ab]){e<=1}").MatchAtStart("\U0001F600c");

        sub.Success.Should().BeTrue();
        (sub.Index, sub.Length).Should().Be((0, 3));
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        sub.FuzzyChanges.Substitutions.Should().Equal(2);

        // astral match('(?:[ab].[cd]){e<=1}', 'a\U0001F600'): span=(0, 2) counts=(0, 0, 1)
        //   changes=([], [], [2])
        // Upstream's deletion position is codepoint 2, which is UTF-16 index 3.
        Match del = new FuzzyRegex("(?:[ab].[cd]){e<=1}").MatchAtStart("a\U0001F600");

        del.Success.Should().BeTrue();
        (del.Index, del.Length).Should().Be((0, 3));
        del.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        del.FuzzyChanges.Deletions.Should().Equal(3);
    }

    [Test]
    public void A_search_attempt_that_fails_after_a_lookaround_leaves_nothing_behind_for_the_next_one()
    {
        // S40a, out of S40's blind review, WHICH WAS RIGHT AFTER ALL - and S47 is where its finding
        // was acted on. The review reported that this port's FuzzyCounts and FuzzyChanges
        // contradicted each other here (one substitution counted, a deletion reported) and called
        // that a defect on the row alone, before upstream is consulted. That contradiction was real
        // and inherited: upstream clears the fuzzy counts on a search restart and leaves the change
        // list, so an abandoned attempt's change displaced the winning attempt's. S40a measured the
        // one-line clear and reverted it because it reddened the two `qab` rows two tests up, which
        // pinned the same contradiction; S47 fixed both together, which is what those two rows
        // needed all along. Ledger entry 11.
        //
        // WHY THE TWO ENGINES REACHED THIS ROW DIFFERENTLY AT ALL: `$` has a `search_start_*` twin,
        // so upstream makes ONE attempt at the end of the subject and has nothing to leak. This port
        // has no prefilter until Phase 7, so it attempts 0, 1, 2 and 3 - and the attempt at 1
        // succeeds inside the lookbehind, records a deletion, then fails on the `$`. With the list
        // cleared on restart that leftover is gone and the two engines now AGREE, which is the part
        // worth noticing: fixing the leak removed a divergence here rather than adding one. Measured
        // with `python tools/probes/upstream-fuzzy-restart-leak.py`, regex 2026.9.10:
        //   search('(?<=(?:[ab][cd]){e<=1})$', 'axc') -> (3, 3) counts=(1, 0, 0) changes=([2], [], [])
        //   search('(?<=(?:abc){e<=2})$',      'ac')  -> (2, 2) counts=(1, 0, 1) changes=([1], [], [0])
        Match reversed = new FuzzyRegex("(?<=(?:[ab][cd]){e<=1})$").Match("axc");

        reversed.Success.Should().BeTrue();
        (reversed.Index, reversed.Length).Should().Be((3, 0));
        reversed.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        reversed.FuzzyChanges.Substitutions.Should().Equal(2);
        reversed.FuzzyChanges.Deletions.Should().BeEmpty();

        Match twoErrors = new FuzzyRegex("(?<=(?:abc){e<=2})$").Match("ac");

        twoErrors.Success.Should().BeTrue();
        (twoErrors.Index, twoErrors.Length).Should().Be((2, 0));
        twoErrors.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 1));
        twoErrors.FuzzyChanges.Substitutions.Should().Equal(1);
        twoErrors.FuzzyChanges.Deletions.Should().Equal(0);

        // THE CONTROLS, and they are what make the paragraph above a measurement rather than a
        // story. Every one of these agrees with upstream, and each removes exactly one ingredient:
        // the restart (a lookahead at a fixed position), the lookaround (the same fuzzy section
        // reversed on its own), and the prefilterable tail (a literal `q`/`d` instead of `$`, which
        // makes upstream walk every position too - and upstream then still agrees, because its
        // winning attempt is its first).
        Match plainReversed = new FuzzyRegex("(?r)(?:[ab][cd]){e<=1}").Match("axc");
        plainReversed.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        plainReversed.FuzzyChanges.Substitutions.Should().Equal(2);

        Match lookahead = new FuzzyRegex("^(?=(?:[ab][cd]){e<=1})").Match("axc");
        lookahead.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        lookahead.FuzzyChanges.Substitutions.Should().Equal(1);

        Match literalTail = new FuzzyRegex("(?<=(?:[ab][cd]){e<=1})q").Match("axcq");
        literalTail.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        literalTail.FuzzyChanges.Substitutions.Should().Equal(2);

        Match literalTailLonger = new FuzzyRegex("(?<=(?:ab){e<=1})d").Match("axqayd");
        literalTailLonger.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        literalTailLonger.FuzzyChanges.Substitutions.Should().Equal(5);

        // And with no lookaround at all, so the restart alone is not enough to leak: the failed
        // attempt at 0 unwinds its own change through the FUZZY backtrack arm on the way out.
        Match noLookaround = new FuzzyRegex("(?:ab){e<=1}d").Match("axbaxd");
        noLookaround.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        noLookaround.FuzzyChanges.Substitutions.Should().Equal(4);
    }

    [Test]
    public void The_reported_changes_agree_with_the_counts_on_every_shape_that_used_to_contradict_them()
    {
        // S47, ledger entry 11. `fuzzy_counts` and `fuzzy_changes` are two views of ONE edit
        // script - the documentation calls the second "a tuple of the positions of the
        // substitutions, insertions and deletions" - so the invariant is that each list's length
        // equals its own count. Upstream breaks it on every row below and this port used to
        // reproduce that; S47 fixes it here, which is the owner's inherited-bug rule (2026-09-12).
        //
        // The rows are the seven upstream contradicts itself on, gathered from the two doors that
        // reach the defect: a verb that cuts the backtracking without unwinding the fuzzy frames,
        // and a nested section whose trailing insertions are counted back out and never unrecorded.
        // `OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` applies the
        // same property to every fuzzy row of a whole wave.
        ChangesAgreeWithCounts(new FuzzyRegex("(?:[ab][bc](*PRUNE)[wx]){e<=2}").Match("qab"));
        ChangesAgreeWithCounts(new FuzzyRegex("(?:[ab](*SKIP)[bc][wx]){e<=2}").Match("qab"));
        ChangesAgreeWithCounts(new FuzzyRegex("(?<=(?:[ab][cd]){e<=1})$").Match("axc"));
        ChangesAgreeWithCounts(new FuzzyRegex("(?<=(?:abc){e<=2})$").Match("ac"));
        ChangesAgreeWithCounts(new FuzzyRegex(@"(?:a\w(?:b\w){e<=3}){i<=1}").MatchAtStart("a ba", partial: true));
        ChangesAgreeWithCounts(
            new FuzzyRegex(@"(?:[^a-f](?:\s\p{L}){2i+1d+1s<=2}){e<=2,i<=1}").FullMatch("a b", partial: true)
        );
        ChangesAgreeWithCounts(
            new FuzzyRegex(@"(?:\B[a-f](?:\Wb){e<=3}){e<=2,i<=1}").MatchAtStart("eab", partial: true)
        );
    }

    /// <summary>
    /// The S47 invariant: each change list holds exactly as many positions as its own count.
    /// </summary>
    /// <param name="m">The match to check.</param>
    private static void ChangesAgreeWithCounts(Match m)
    {
        m.Success.Should().BeTrue();
        m.FuzzyChanges.Substitutions.Count.Should().Be(m.FuzzyCounts.Substitutions, "substitutions");
        m.FuzzyChanges.Insertions.Count.Should().Be(m.FuzzyCounts.Insertions, "insertions");
        m.FuzzyChanges.Deletions.Count.Should().Be(m.FuzzyCounts.Deletions, "deletions");
    }
}
