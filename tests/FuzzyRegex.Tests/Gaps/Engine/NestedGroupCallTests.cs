using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A call runs the called group with the call's own direction and fuzziness, and so does every
/// call that group makes in turn (D40).
/// </summary>
/// <remarks>
/// <para>
/// A group called from somewhere with other features than its definition is compiled again as a
/// copy with the caller's features (<c>upstream/regex/_regex_core.py</c>:4420-4457). The calls
/// inside that copy kept the reference resolved for where they are written, so a copy compiled
/// exact went on to call the fuzzy compile of the next group. With no fuzzy section in force, the
/// first fuzzy item that failed to match read a null section: a
/// <see cref="NullReferenceException"/> here, and a segmentation fault in upstream on every
/// crashing row below (regex 2026.9.10, measured 2026-09-30). The other way round, a fuzzy copy
/// called the exact compile of the next group, and lost the matches its errors would have found.
/// </para>
/// <para>
/// Every expected answer is the one upstream gives for the same pattern with the calls written
/// out as the bodies they call, which is what a call means; each control is quoted beside it.
/// </para>
/// </remarks>
public sealed class NestedGroupCallTests
{
    private static void ShouldMatch(Match m, int index, int length, FuzzyCounts counts)
    {
        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }

    // The minimal witness. The call outside the section runs g4, and so g3, exactly.
    //   search('a(?:(?P<g4>a)){s<=1}(?P<g3>a)', 'ba')   None
    //   search('a(?:(?P<g4>a)){s<=1}(?P<g3>a)', 'aba')  (0, 3) with (1, 0, 0), and so does the
    //   called form below, which upstream answers without crashing
    [Test]
    public void A_call_from_outside_a_fuzzy_section_runs_the_groups_it_calls_exactly()
    {
        var regex = new FuzzyRegex("(?&g4)(?:(?P<g4>(?&g3))){s<=1}(?P<g3>a)");

        regex.Match("ba").Success.Should().BeFalse();
        ShouldMatch(regex.Match("aba"), 0, 3, new FuzzyCounts(1, 0, 0));
    }

    // The row the defect was found on (D10's blind review). With the conditional's call written
    // out, upstream answers None:
    //   search('(?P<g1>.{1,2}(?(?!(?:..){e<=1}a)(?:aab){e<=1}b|a.*))(?<=(?P<g3>a)(?:(?P>g1))*).'
    //          '(?:(?P<g4>(?:..){e<=1}(?P>g3))){s<=1}', 'baa')   None
    [Test]
    public void The_found_row_answers_no_match()
    {
        new FuzzyRegex(
            "(?P<g1>.{1,2}(?(?!(?&g4))(?:aab){e<=1}b|a.*))(?<=(?P<g3>a)(?:(?P>g1))*)."
                + "(?:(?P<g4>(?:..){e<=1}(?P>g3))){s<=1}"
        )
            .Match("baa")
            .Success.Should()
            .BeFalse();
    }

    // A conditional's test calls g4 from outside the section.
    //   search('(?(?!.a)x|a)(?:(?P<g4>.(?&g3))){s<=1}(?P<g3>a)?', s)
    //   'aa' None, 'ab' None, 'xab' None, 'xaa' None, 'aab' (0, 3) with (1, 0, 0)
    [Test]
    public void A_conditional_test_that_calls_a_group_runs_it_exactly()
    {
        var regex = new FuzzyRegex("(?(?!(?&g4))x|a)(?:(?P<g4>.(?&g3))){s<=1}(?P<g3>a)?");

        foreach (string subject in (string[])["aa", "ab", "xab", "xaa"])
        {
            regex.Match(subject).Success.Should().BeFalse(subject);
        }

        ShouldMatch(regex.Match("aab"), 0, 3, new FuzzyCounts(1, 0, 0));
    }

    // A lookbehind calls g4 from outside the section.
    //   search('.(?<=a)(?:(?P<g4>a)){s<=1}(?P<g3>a)', 'baba')  (1, 4) with (1, 0, 0)
    //   search('.(?<=a)(?:(?P<g4>a)){s<=1}(?P<g3>a)', 'bab')   None
    [Test]
    public void A_lookbehind_that_calls_a_group_runs_it_exactly_and_backwards()
    {
        var regex = new FuzzyRegex(".(?<=(?&g4))(?:(?P<g4>(?&g3))){s<=1}(?P<g3>a)");

        ShouldMatch(regex.Match("baba"), 1, 3, new FuzzyCounts(1, 0, 0));
        regex.Match("bab").Success.Should().BeFalse();
    }

    // DIVERGES FROM UPSTREAM, which answers None on both called forms: its fuzzy copy of g4 (and
    // of the whole pattern) calls the exact compile of the next group.
    //   search('(?P<g3>a)(?P<g4>a)(?:(?&g4)){s<=1}', 'aab')      (0, 3) with (1, 0, 0)
    //   search('(?P<g>a)(?:b(?:(?R)){s<=1}|ca)', 'abacb')        (0, 5) with (1, 0, 0)
    [Test]
    public void A_call_inside_a_fuzzy_section_runs_the_groups_it_calls_fuzzily()
    {
        ShouldMatch(
            new FuzzyRegex("(?P<g3>a)(?P<g4>(?&g3))(?:(?&g4)){s<=1}").Match("aab"),
            0,
            3,
            new FuzzyCounts(1, 0, 0)
        );
        ShouldMatch(
            new FuzzyRegex("(?P<g>a)(?:b(?:(?R)){s<=1}|c(?&g))").Match("abacb"),
            0,
            5,
            new FuzzyCounts(1, 0, 0)
        );
    }

    // D42. A lookaround's body is exact inside a fuzzy section, so a call in it runs its group
    // exactly, as the body written out does. DIVERGES FROM UPSTREAM, which runs the call fuzzily
    // and answers (0, 2) with one substitution on both called forms (regex 2026.9.10, 2026-09-30):
    //   search('(?:(?=ab)..){s<=1}', 'xb')    None
    //   search('(?:..(?<=ab)){s<=1}', 'xb')   None
    //   search('(?:(?!ab)..){s<=1}', 'ab')    None, and the called form agrees
    [Test]
    public void A_call_inside_a_lookaround_in_a_fuzzy_section_runs_exactly()
    {
        new FuzzyRegex("(?:(?=(?&g))..){s<=1}(?P<g>ab)?").Match("xb").Success.Should().BeFalse();
        new FuzzyRegex("(?:..(?<=(?&g))){s<=1}(?P<g>ab)?").Match("xb").Success.Should().BeFalse();
        new FuzzyRegex("(?:(?!(?&g))..){s<=1}(?P<g>ab)?").Match("ab").Success.Should().BeFalse();
    }

    // D43. A conditional's lookbehind test runs backwards, so a call in it runs its group backwards,
    // as the test written out does. DIVERGES FROM UPSTREAM, which runs the call forwards and
    // answers None on both called forms (regex 2026.9.10, 2026-09-30):
    //   search('bc(?(?<=bc)x|y)', 'bcxzzz')   (0, 3)
    //   search('bc(?(?<!bc)x|y)', 'bcyzzz')   (0, 3)
    // The 'zzz' keeps the subject longer than the call's inflated minimum width (GroupCallTests),
    // which would refuse 'bcx' before matching starts, in both engines.
    [Test]
    public void A_call_in_a_conditional_lookbehind_test_runs_backwards()
    {
        ShouldMatch(new FuzzyRegex("bc(?(?<=(?&g))x|y)(?P<g>bc)?").Match("bcxzzz"), 0, 3, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("bc(?(?<!(?&g))x|y)(?P<g>bc)?").Match("bcyzzz"), 0, 3, new FuzzyCounts(0, 0, 0));
    }
}
