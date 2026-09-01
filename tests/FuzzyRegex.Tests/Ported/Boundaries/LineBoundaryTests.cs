using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Boundaries;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_line_boundary</c>
/// (lines 1568-1608): which characters end a line for <c>.</c>, <c>^</c> and <c>$</c>, with and
/// without the WORD flag and with and without MULTILINE.
/// </summary>
public sealed class LineBoundaryTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#1")]
    public void Dot_plus_findall_stops_at_a_bare_newline() =>
        FuzzyRegex.Matches("Line 1\nLine 2\n", @".+").Select(m => m.Value).Should().Equal("Line 1", "Line 2");

    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#2")]
    public void Dot_plus_findall_does_not_stop_at_a_bare_carriage_return() =>
        FuzzyRegex.Matches("Line 1\rLine 2\r", @".+").Select(m => m.Value).Should().Equal("Line 1\rLine 2\r");

    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#3")]
    public void Dot_plus_findall_stops_before_the_newline_in_a_crlf_pair() =>
        FuzzyRegex.Matches("Line 1\r\nLine 2\r\n", @".+").Select(m => m.Value).Should().Equal("Line 1\r", "Line 2\r");

    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#4")]
    public void Dot_plus_findall_with_word_flag_stops_at_a_bare_newline() =>
        FuzzyRegex.Matches("Line 1\nLine 2\n", @"(?w).+").Select(m => m.Value).Should().Equal("Line 1", "Line 2");

    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#5")]
    public void Dot_plus_findall_with_word_flag_also_stops_at_a_bare_carriage_return() =>
        FuzzyRegex.Matches("Line 1\rLine 2\r", @"(?w).+").Select(m => m.Value).Should().Equal("Line 1", "Line 2");

    [Test]
    [Property("Upstream", "RegexTests.test_line_boundary#6")]
    public void Dot_plus_findall_with_word_flag_stops_before_the_newline_in_a_crlf_pair() =>
        FuzzyRegex.Matches("Line 1\r\nLine 2\r\n", @"(?w).+").Select(m => m.Value).Should().Equal("Line 1", "Line 2");

    [Test]
    [Arguments("abc", 0)]
    [Arguments("\nabc", null)]
    [Arguments("\rabc", null)]
    [Property("Upstream", "RegexTests.test_line_boundary#7-9")]
    public void Caret_matches_only_at_the_very_start_of_the_subject(string subject, int? expectedStart)
    {
        Match m = FuzzyRegex.Match(subject, @"^abc");

        m.Success.Should().Be(expectedStart is not null);
        if (expectedStart is not null)
        {
            m.Index.Should().Be(expectedStart.Value);
        }
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("\nabc", null)]
    [Arguments("\rabc", null)]
    [Property("Upstream", "RegexTests.test_line_boundary#10-12")]
    public void Caret_with_word_flag_still_matches_only_at_the_very_start(string subject, int? expectedStart)
    {
        Match m = FuzzyRegex.Match(subject, @"(?w)^abc");

        m.Success.Should().Be(expectedStart is not null);
        if (expectedStart is not null)
        {
            m.Index.Should().Be(expectedStart.Value);
        }
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("abc\n", 0)]
    [Arguments("abc\r", null)]
    [Property("Upstream", "RegexTests.test_line_boundary#13-15")]
    public void Dollar_matches_at_the_end_or_just_before_a_trailing_newline(string subject, int? expectedStart)
    {
        Match m = FuzzyRegex.Match(subject, @"abc$");

        m.Success.Should().Be(expectedStart is not null);
        if (expectedStart is not null)
        {
            m.Index.Should().Be(expectedStart.Value);
        }
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("abc\n", 0)]
    [Arguments("abc\r", 0)]
    [Property("Upstream", "RegexTests.test_line_boundary#16-18")]
    public void Dollar_with_word_flag_also_matches_just_before_a_trailing_carriage_return(
        string subject,
        int expectedStart
    )
    {
        Match m = FuzzyRegex.Match(subject, @"(?w)abc$");

        m.Success.Should().BeTrue();
        m.Index.Should().Be(expectedStart);
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("\nabc", 1)]
    [Arguments("\rabc", null)]
    [Property("Upstream", "RegexTests.test_line_boundary#19-21")]
    public void Multiline_caret_matches_after_a_newline_but_not_after_a_carriage_return(
        string subject,
        int? expectedStart
    )
    {
        Match m = FuzzyRegex.Match(subject, @"(?m)^abc");

        m.Success.Should().Be(expectedStart is not null);
        if (expectedStart is not null)
        {
            m.Index.Should().Be(expectedStart.Value);
        }
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("\nabc", 1)]
    [Arguments("\rabc", 1)]
    [Property("Upstream", "RegexTests.test_line_boundary#22-24")]
    public void Multiline_caret_with_word_flag_also_matches_after_a_carriage_return(string subject, int expectedStart)
    {
        Match m = FuzzyRegex.Match(subject, @"(?mw)^abc");

        m.Success.Should().BeTrue();
        m.Index.Should().Be(expectedStart);
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("abc\n", 0)]
    [Arguments("abc\r", null)]
    [Property("Upstream", "RegexTests.test_line_boundary#25-27")]
    public void Multiline_dollar_matches_before_a_newline_but_not_before_a_carriage_return(
        string subject,
        int? expectedStart
    )
    {
        Match m = FuzzyRegex.Match(subject, @"(?m)abc$");

        m.Success.Should().Be(expectedStart is not null);
        if (expectedStart is not null)
        {
            m.Index.Should().Be(expectedStart.Value);
        }
    }

    [Test]
    [Arguments("abc", 0)]
    [Arguments("abc\n", 0)]
    [Arguments("abc\r", 0)]
    [Property("Upstream", "RegexTests.test_line_boundary#28-30")]
    public void Multiline_dollar_with_word_flag_also_matches_before_a_carriage_return(string subject, int expectedStart)
    {
        Match m = FuzzyRegex.Match(subject, @"(?mw)abc$");

        m.Success.Should().BeTrue();
        m.Index.Should().Be(expectedStart);
    }
}
