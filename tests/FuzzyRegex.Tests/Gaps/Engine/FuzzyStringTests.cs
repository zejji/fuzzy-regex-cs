using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Fuzzy matching of the multi-character items: <c>STRING</c> and its case-folding and reverse
/// forms, <c>REF_GROUP</c> and its forms, and the two <c>*_REPEAT_ONE</c> loops whose tail is
/// fuzzy.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-09-13 by
/// <c>tools/probes/upstream-fuzzy-strings.py</c>, and its line is quoted beside the assertion.
/// </para>
/// <para>
/// Where <see cref="FuzzyMatchingTests"/> has to avoid two adjacent literals, this file exists to
/// use them: <c>Sequence.pack_characters</c> (<c>upstream/regex/_regex_core.py:3526</c>) packs a run
/// of two or more <c>Character</c> items into one <c>STRING</c> node, so <c>(?:abc)</c> is a single
/// string item and every pattern below reaches an arm S39 ports.
/// </para>
/// </remarks>
public sealed class FuzzyStringTests
{
    [Test]
    public void A_string_can_be_matched_with_a_substitution()
    {
        // sub match('(?:abc){e<=1}', 'axc'): span=(0, 3) counts=(1, 0, 0) changes=([1], [], [])
        Match m = new FuzzyRegex("(?:abc){e<=1}").MatchAtStart("axc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(Substitutions: 1, Insertions: 0, Deletions: 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    [Test]
    public void A_string_can_be_matched_with_a_deletion()
    {
        // del match('(?:abc){e<=1}', 'ac'): span=(0, 2) counts=(0, 0, 1) changes=([], [], [1])
        Match m = new FuzzyRegex("(?:abc){e<=1}").MatchAtStart("ac");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void An_insertion_after_a_complete_string_is_fuzzy_inserts_work()
    {
        // Only 'fuzzy_insert' (:10346), called at :14765 once the whole string has matched, can
        // supply this: the section's items are all used up, so nothing else is left to fuzz.
        // trailing-ins fullmatch('(?:abc){e<=1}', 'abcx'): span=(0, 4) counts=(0, 1, 0) changes=([], [3], [])
        Match m = new FuzzyRegex("(?:abc){e<=1}").FullMatch("abcx");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(3);
    }

    [Test]
    public void A_substitution_is_preferred_to_an_insertion_inside_a_string()
    {
        // The type order is SUB, INS, DEL, so a subject that could be read either way is read as a
        // substitution and the match is three characters rather than four.
        // ins match('(?:abc){e<=1}', 'abxc'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        Match m = new FuzzyRegex("(?:abc){e<=1}").MatchAtStart("abxc");

        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);
    }

    [Test]
    public void A_search_does_not_insert_at_the_search_anchor()
    {
        // 'permit_insertion' is false at the anchor, so the search moves on one character and
        // matches exactly rather than paying for a leading insertion.
        // leading-ins search('(?:abc){e<=1}', 'xabc'): span=(1, 4) counts=(0, 0, 0) changes=([], [], [])
        Match m = new FuzzyRegex("(?:abc){e<=1}").Match("xabc");

        (m.Index, m.Length).Should().Be((1, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void A_reverse_string_fuzzes_from_the_other_end()
    {
        // rev-sub match('(?r)(?:abc){e<=1}', 'axc'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        // The change is at 2 and not at 1: a reverse walk reaches the failing character from the
        // right, so 'new_text_pos - step' lands one character the other way.
        Match sub = new FuzzyRegex("(?r)(?:abc){e<=1}").MatchAtStart("axc");
        (sub.Index, sub.Length).Should().Be((0, 3));
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        sub.FuzzyChanges.Substitutions.Should().Equal(2);

        // rev-ins match('(?r)(?:abc){e<=1}', 'abxc'): span=(0, 4) counts=(0, 1, 0) changes=([], [3], [])
        Match ins = new FuzzyRegex("(?r)(?:abc){e<=1}").MatchAtStart("abxc");
        (ins.Index, ins.Length).Should().Be((0, 4));
        ins.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        ins.FuzzyChanges.Insertions.Should().Equal(3);

        // rev-del match('(?r)(?:abc){e<=1}', 'ac'): span=(0, 2) counts=(0, 0, 1) changes=([], [], [1])
        Match del = new FuzzyRegex("(?r)(?:abc){e<=1}").MatchAtStart("ac");
        (del.Index, del.Length).Should().Be((0, 2));
        del.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        del.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void A_case_insensitive_string_fuzzes_the_same_way()
    {
        // ign-sub match('(?i)(?:abc){e<=1}', 'AxC'): span=(0, 3) counts=(1, 0, 0) changes=([1], [], [])
        Match forward = new FuzzyRegex("(?i)(?:abc){e<=1}").MatchAtStart("AxC");
        (forward.Index, forward.Length).Should().Be((0, 3));
        forward.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        forward.FuzzyChanges.Substitutions.Should().Equal(1);

        // ign-rev-sub match('(?ri)(?:abc){e<=1}', 'AxC'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        Match reverse = new FuzzyRegex("(?ri)(?:abc){e<=1}").MatchAtStart("AxC");
        (reverse.Index, reverse.Length).Should().Be((0, 3));
        reverse.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        reverse.FuzzyChanges.Substitutions.Should().Equal(2);
    }

    [Test]
    public void A_multi_character_fold_costs_nothing_when_it_matches()
    {
        // The ss of STRASSE answers for the single pattern character folded from U+00DF, and that
        // is one exact comparison and not two substitutions.
        // fld-exact match('(?fi)(?:stra\xdfe){e<=1}', 'STRASSE'): span=(0, 7) counts=(0, 0, 0) changes=([], [], [])
        Match m = new FuzzyRegex("(?fi)(?:straße){e<=1}").MatchAtStart("STRASSE");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 7));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void An_error_next_to_a_multi_character_fold_is_counted_once()
    {
        // fld-sub match('(?fi)(?:stra\xdfe){e<=1}', 'STRASSX'): span=(0, 7) counts=(1, 0, 0) changes=([6], [], [])
        Match after = new FuzzyRegex("(?fi)(?:straße){e<=1}").MatchAtStart("STRASSX");
        (after.Index, after.Length).Should().Be((0, 7));
        after.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        after.FuzzyChanges.Substitutions.Should().Equal(6);

        // fld-inside match('(?fi)(?:stra\xdfe){e<=1}', 'STRASXE'): span=(0, 7) counts=(1, 0, 0) changes=([5], [], [])
        // The error is INSIDE the folding of the subject's own 'S'/'X' pair, which is the case only
        // 'fuzzy_match_string_fld' can reach - 'folded_pos' moves and 'text_pos' does not.
        Match inside = new FuzzyRegex("(?fi)(?:straße){e<=1}").MatchAtStart("STRASXE");
        (inside.Index, inside.Length).Should().Be((0, 7));
        inside.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        inside.FuzzyChanges.Substitutions.Should().Equal(5);

        // fld-st match('(?fi)(?:ﬆx){e<=1}', 'STY'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        // U+FB06, the st ligature, folds to two characters in the PATTERN this time.
        Match ligature = new FuzzyRegex("(?fi)(?:ﬆx){e<=1}").MatchAtStart("STY");
        (ligature.Index, ligature.Length).Should().Be((0, 3));
        ligature.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        ligature.FuzzyChanges.Substitutions.Should().Equal(2);

        // fld-rev match('(?fir)(?:stra\xdfe){e<=1}', 'STRASSX'): span=(0, 7) counts=(1, 0, 0) changes=([7], [], [])
        Match reverse = new FuzzyRegex("(?fir)(?:straße){e<=1}").MatchAtStart("STRASSX");
        (reverse.Index, reverse.Length).Should().Be((0, 7));
        reverse.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        reverse.FuzzyChanges.Substitutions.Should().Equal(7);
    }

    [Test]
    public void A_backreference_can_be_matched_with_one_error()
    {
        // ref-sub match('(abc)(?:\\1){e<=1}', 'abcabx'): span=(0, 6) counts=(1, 0, 0) changes=([5], [], [])
        Match sub = new FuzzyRegex(@"(abc)(?:\1){e<=1}").MatchAtStart("abcabx");
        sub.Success.Should().BeTrue();
        (sub.Index, sub.Length).Should().Be((0, 6));
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        sub.FuzzyChanges.Substitutions.Should().Equal(5);

        // ref-del match('(abc)(?:\\1){e<=1}', 'abcab'): span=(0, 5) counts=(0, 0, 1) changes=([], [], [5])
        Match del = new FuzzyRegex(@"(abc)(?:\1){e<=1}").MatchAtStart("abcab");
        (del.Index, del.Length).Should().Be((0, 5));
        del.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        del.FuzzyChanges.Deletions.Should().Equal(5);

        // ref-ign match('(?i)(abc)(?:\\1){e<=1}', 'abcABX'): span=(0, 6) counts=(1, 0, 0) changes=([5], [], [])
        Match ign = new FuzzyRegex(@"(?i)(abc)(?:\1){e<=1}").MatchAtStart("abcABX");
        (ign.Index, ign.Length).Should().Be((0, 6));
        ign.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        ign.FuzzyChanges.Substitutions.Should().Equal(5);

        // ref-rev match('(?r)(?:\\1){e<=1}(abc)', 'abxabc'): span=(0, 6) counts=(1, 0, 0) changes=([3], [], [])
        Match rev = new FuzzyRegex(@"(?r)(?:\1){e<=1}(abc)").MatchAtStart("abxabc");
        (rev.Index, rev.Length).Should().Be((0, 6));
        rev.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        rev.FuzzyChanges.Substitutions.Should().Equal(3);
    }

    [Test]
    public void A_backreference_folds_both_sides_before_fuzzing()
    {
        // ref-fld match('(?fi)(stra\xdfe)(?:\\1){e<=1}', 'stra\xdfeSTRASSX'): span=(0, 13) counts=(1, 0, 0) changes=([12], [], [])
        // Six subject characters on the right answer for five on the left, so the two foldings run
        // at different speeds and only 'fuzzy_match_group_fld' can charge the mismatch once.
        Match m = new FuzzyRegex("(?fi)(straße)(?:\\1){e<=1}").MatchAtStart("straßeSTRASSX");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 13));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(12);
    }

    [Test]
    public void A_fuzzy_repeat_next_to_a_string_still_answers()
    {
        // greedy-one2 search('(?:[a-c]+x){e<=1}', 'zabcy'): span=(1, 5) counts=(1, 0, 0) changes=([4], [], [])
        // The repeat itself is a GREEDY_REPEAT and not a GREEDY_REPEAT_ONE - see
        // RepeatTests.No_repeat_one_node_in_the_corpus_has_a_fuzzy_test_node for why that matters.
        Match searched = new FuzzyRegex("(?:[a-c]+x){e<=1}").Match("zabcy");
        (searched.Index, searched.Length).Should().Be((1, 4));
        searched.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        searched.FuzzyChanges.Substitutions.Should().Equal(4);

        // lazy-one2 match('(?:a*?x){e<=1}', 'aay'): span=(0, 1) counts=(1, 0, 0) changes=([0], [], [])
        Match star = new FuzzyRegex("(?:a*?x){e<=1}").MatchAtStart("aay");
        (star.Index, star.Length).Should().Be((0, 1));
        star.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        star.FuzzyChanges.Substitutions.Should().Equal(0);
    }

    [Test]
    public void A_change_position_inside_a_string_is_a_UTF16_index()
    {
        // astral match('(?:\U0001f600bc){e<=1}', '\U0001f600bx'): span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        // Upstream counts in codepoints, so its span is (0, 3) and its change is at 2; this port
        // counts in UTF-16 code units, where U+1F600 is a surrogate pair, so both move up by one.
        Match m = new FuzzyRegex("(?:\U0001F600bc){e<=1}").MatchAtStart("\U0001F600bx");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(3);
    }
}
