using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D89: a <c>\K</c> inside a lookaround can leave a match whose start lies after its end, and the
/// text of such a match is empty.
/// </summary>
/// <remarks>
/// <c>(?=y\K)</c> over <c>yy</c> matches (1, 0): the lookahead reads the first <c>y</c> and moves
/// the reported start to 1, then puts the text position back to 0. Upstream slices the subject
/// with <c>PyUnicode_Substring</c> (<c>get_slice</c>, <c>upstream/src/_regex.c</c> line 18842),
/// which returns '' when the end is before the start, so the group's text, <c>expand</c>,
/// <c>expandf</c>, <c>sub</c> and <c>subf</c> all see ''. A replacement still copies the text
/// before the start and resumes from the end, so the subject between the two is copied twice.
/// Every expected value here is regex 2026.9.10's, measured 2026-10-15 with
/// <c>.scratch/s1016/night/small/d89_up3.py</c>. Upstream's own <c>sub</c> without a count loops
/// on these patterns (D87), so the rows use a count of 1.
/// </remarks>
public sealed class CrossedSpanExpansionTests
{
    [Test]
    [Arguments(@"(?=y\K)", "y", "y<>y")]
    [Arguments(@"(?=y\K)", "yy", "y<>yy")]
    [Arguments(@"(?=y\K)", "ayb", "ay<>yb")]
    [Arguments(@"(?r)(?<=\Ky)", "yy", "yy<>y")]
    [Arguments(@"(?r)(?<=\Ky)", "ayb", "ay<>yb")]
    public void A_template_expands_a_crossed_whole_match_as_empty(string pattern, string subject, string expected)
    {
        var regex = new FuzzyRegex(pattern);

        regex.Replace(subject, @"<\g<0>>", count: 1).Should().Be(expected);
        regex.Replace(subject, @"<\g<0>>", 1, out int replacements).Should().Be(expected);
        replacements.Should().Be(1);
        regex.ReplaceFormat(subject, "<{0}>", count: 1).Should().Be(expected);
        regex.Replace(subject, static m => "<" + m.Value + ">", count: 1).Should().Be(expected);
    }

    [Test]
    [Arguments(@"(?=y\K)", "yy", 1, 0)]
    [Arguments(@"(?r)(?<=\Ky)", "ayb", 2, 1)]
    public void A_crossed_match_reads_as_empty(string pattern, string subject, int start, int end)
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.Value.Should().BeEmpty();
        m.ValueSpan.IsEmpty.Should().BeTrue();
        m.ToString().Should().BeEmpty();
        m.Groups[0].Value.Should().BeEmpty();
        m.Captures.Select(static c => c.Value).Should().Equal("");
        m.Result(@"<\g<0>>").Should().Be("<>");
        m.ResultFormat("<{0}>").Should().Be("<>");
    }

    [Test]
    public void A_match_that_starts_before_the_last_one_ended_copies_no_text_between_them()
    {
        // The second match, (0, 2), starts before the first one's end, 1: the lookbehind's \K moved
        // its start back. regex.sub(r'a|(?<=\Ka)b', r'<\g<0>>', 'ab') is '<a><ab>'.
        new FuzzyRegex(@"a|(?<=\Ka)b")
            .Replace("ab", @"<\g<0>>")
            .Should()
            .Be("<a><ab>");
    }

    [Test]
    public void A_split_at_a_crossed_match_copies_the_text_twice()
    {
        // regex.split(r'(?=y\K)', 'yy', maxsplit=1) is ['y', 'yy'], and with the group
        // ['y', 'y', 'yy']. A control: neither piece is crossed, so it passes without the fix.
        new FuzzyRegex(@"(?=y\K)")
            .Split("yy", maxSplits: 1)
            .Should()
            .Equal("y", "yy");
        new FuzzyRegex(@"(?=(y)\K)").Split("yy", maxSplits: 1).Should().Equal("y", "y", "yy");
    }

    [Test]
    public void A_split_at_a_match_that_starts_before_the_last_one_ended_has_an_empty_piece()
    {
        // The piece between (0, 1) and (0, 2) runs from 1 back to 0.
        // regex.split(r'a|(?<=\Ka)b', 'ab') is ['', '', ''].
        new FuzzyRegex(@"a|(?<=\Ka)b")
            .Split("ab")
            .Should()
            .Equal("", "", "");
    }

    [Test]
    public void A_group_inside_the_lookahead_keeps_its_own_text()
    {
        // regex.sub(r'(?=(?P<n>y)\K)', r'<\g<n>|\g<0>>', 'yy', count=1) is 'y<y|>yy'.
        new FuzzyRegex(@"(?=(?P<n>y)\K)")
            .Replace("yy", @"<\g<n>|\g<0>>", count: 1)
            .Should()
            .Be("y<y|>yy");
    }
}
