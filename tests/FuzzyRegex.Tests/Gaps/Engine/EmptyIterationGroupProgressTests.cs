using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// In exact matching, a repeat iteration that read no text but changed the span of a group the
/// pattern tests (by a conditional or a backreference) counts as progress, so the repeat goes round
/// again. Upstream's rule, kept on purpose (known defect D12, decided 2026-09-28).
/// </summary>
/// <remarks>
/// <para>
/// END_GREEDY_REPEAT and END_LAZY_REPEAT go round again if the text position moved or
/// <c>capture_change</c> moved (<c>upstream/src/_regex.c:12550-12553</c>, <c>:12787-12793</c>); a
/// referenced group bumps it only when its span changes (<c>same_span_as_group</c>, <c>:12726-12728</c>).
/// The port's check is <c>src/FuzzyRegex/Engine/Matcher.cs</c> at "Have we advanced through the text
/// or has a capture group change?".
/// </para>
/// <para>
/// Perl, PCRE2, .NET and Python <c>re</c> look at the position only, so over <c>c</c>
/// <c>^(?:(?(1)c|z)|())*$</c> finds nothing there: the first pass sets group 1 and reads nothing, the
/// loop stops, and <c>c</c> is never read. The same engines match <c>^(?:(?(1)c|z)|()){2,}$</c>, so
/// they break the rule that <c>X*</c> matches whatever <c>X{2,}</c> matches. Upstream, the port and
/// Onigmo answer every quantifier form alike. The survey is
/// <c>docs/plan/2026-09-26-empty-iteration-survey.md</c>, "Decision: a changed tested group is
/// progress"; every expected value below is <c>regex</c> 2026.9.10, run with
/// <c>tools/probes/empty-iteration-survey/capture_progress.py</c> on 2026-09-28.
/// </para>
/// </remarks>
public sealed class EmptyIterationGroupProgressTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    [Test]
    [Arguments(@"^(?:(?(1)c|z)|())*$", "c", 0, 1, "")]
    [Arguments(@"(?:(?(1)c|z)|())*$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|())+$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|()){0,5}$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|())*$", "cc", 0, 2, "")]
    [Arguments(@"^(?:()|(?(1)c|z))*$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|())*?$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|())+?$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|())*+$", "c", 0, 1, "")]
    [Arguments(@"^(?>(?:(?(1)c|z)|())*)$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?:(?(1)c|z)|())*)*$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?:(?(1)c|z)|())+)*$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?(1)c|z)|(()))*$", "c", 0, 1, "")]
    [Arguments(@"^(?:\1c|())*$", "c", 0, 1, "")]
    [Arguments(@"^(?:(?=(c))|\1)*$", "c", 0, 1, "c")]
    [Arguments(@"^(?:\1|(?=(c)))*$", "c", 0, 1, "c")]
    [Arguments(@"^(?:\1|(?=(c)))*?$", "c", 0, 1, "c")]
    [Arguments(@"^(?:\1|(?=(c)))*+$", "c", 0, 1, "c")]
    [Arguments(@"^(?:(?:\1|(?=(c)))*)*$", "c", 0, 1, "c")]
    [Arguments(@"^(?:\1|(?=(c)))+$", "c", 0, 1, "c")]
    public void An_empty_iteration_that_changed_a_tested_group_lets_the_repeat_go_round_again(
        string pattern,
        string subject,
        int start,
        int end,
        string group1
    )
    {
        // Each of these needs a second pass that the first, empty, pass made possible. Perl, PCRE2,
        // .NET and re answer most rows with no match; the survey table has each engine's answers.
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.Groups[1].Value.Should().Be(group1);
    }

    [Test]
    [Arguments(@"^(?:(?(2)c|z)|(a)|())*$")]
    public void A_group_the_empty_pass_sets_is_tested_by_number_not_by_the_first_group(string pattern)
    {
        // V1 search(r'^(?:(?(2)c|z)|(a)|())*$', 'c'): span=(0, 1), group 1 unset.
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match("c");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    [Arguments(@"^(?:(?(1)c|z)|())")]
    [Arguments(@"^(?:\1|(?=(c)))")]
    public void Star_matches_whatever_a_higher_minimum_matches(string body)
    {
        // The law the position-only engines break: over 'c', {2,} and + match, so * must too.
        foreach (string quantifier in new[] { "*", "+", "{1,}", "{2,}", "{0,5}", "*?" })
        {
            new FuzzyRegex(body + quantifier + "$", FuzzyRegexOptions.None, _timeout)
                .Match("c")
                .Success.Should()
                .BeTrue(quantifier);
        }
    }

    [Test]
    [Arguments(@"^(?:(c)|())*$", "c", 0, 1)]
    [Arguments(@"(?:(b|))*", "bba", 0, 2)]
    [Arguments(@"^(?:(?=(c))|c)*$", "c", 0, 1)]
    [Arguments(@"^(?:(?(1)c|z)|())*$", "", 0, 0)]
    public void An_empty_iteration_that_changed_only_an_untested_group_stops_the_repeat(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        // Controls: every engine surveyed agrees on these (Perl, PCRE2, re, .NET, Java, Node,
        // Onigmo and regex). A group nothing tests is not progress.
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }
}
