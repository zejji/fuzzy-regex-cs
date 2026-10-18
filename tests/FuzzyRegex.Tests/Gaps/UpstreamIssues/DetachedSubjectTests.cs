using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Tests.Ported;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.UpstreamIssues;

/// <summary>
/// Upstream issue 619, fixed in 2026.9.19 (commit 85e568c): after <c>Match.detach_string()</c>,
/// <c>expand()</c> crashed the interpreter and <c>expandf()</c> raised, because
/// <c>match_expand</c> and <c>capture_str</c> read <c>match->string</c>, which
/// <c>detach_string</c> sets to <c>NULL</c>.
/// </summary>
/// <remarks>
/// <para>
/// Measured 2026-10-18 against <c>regex</c> 2026.9.10, on <c>regex.match(r'(a)|(b)', 'a')</c>:
/// before detaching, <c>expand(r'[\1][\2]')</c> and <c>expandf('[{1}][{2}]')</c> both give
/// <c>'[a][]'</c>; after <c>detach_string()</c>, <c>expand</c> ends the process with a segmentation
/// fault and <c>expandf</c> raises <c>SystemError: null argument to internal routine</c>. Group 2
/// is there so that both paths need the empty default value <c>capture_str</c> slices from the
/// subject.
/// </para>
/// <para>
/// This port cannot have the fault. It has no <c>detach_string</c> (<c>docs/PORTMAP.md</c>, the
/// <c>Match.detach_string</c> row: the .NET GC needs no hint to free a subject), and every
/// <see cref="Capture"/>, and so every <see cref="Match"/> and <see cref="Group"/>, holds its
/// subject in a <c>readonly</c> field set once in the constructor (<c>src/FuzzyRegex/Match.cs</c>,
/// <c>Capture._subject</c>). The nearest .NET equivalent of detaching is the caller dropping its
/// own reference to the subject, which these tests do before asking for the expansion; they pin
/// the answer upstream gives on a match that was never detached.
/// </para>
/// </remarks>
public sealed class DetachedSubjectTests
{
    [Test]
    public void Result_still_expands_after_the_caller_drops_the_subject()
    {
        Match m = MatchOnASubjectNobodyElseHolds();

        m.Result(@"[\1][\2]").Should().Be("[a][]");
    }

    [Test]
    public void ResultFormat_gives_an_unmatched_group_its_empty_default_after_the_caller_drops_the_subject()
    {
        Match m = MatchOnASubjectNobodyElseHolds();

        m.ResultFormat("[{1}][{2}]").Should().Be("[a][]");
        m.Groups[2].Value.Should().BeEmpty();
    }

    /// <summary>
    /// Matches against a subject built at run time, so no literal in the assembly keeps it alive,
    /// and collects garbage once the only reference left is the match's own.
    /// </summary>
    private static Match MatchOnASubjectNobodyElseHolds()
    {
        Match m = BuildMatch();
#pragma warning disable S1215 // Freeing whatever nothing else holds is the .NET analogue of detach_string.
        GC.Collect();
#pragma warning restore S1215
        GC.WaitForPendingFinalizers();
        return m;

        static Match BuildMatch() => Upstream.MatchAtStart(new string('a', 1), "(a)|(b)");
    }
}
