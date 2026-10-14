using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Stage C4, capture-free matching: a call that reads no group (<c>IsMatch</c>, <c>Count</c>, span
/// <c>EnumerateMatches</c>, <c>Replace</c> with a literal) on a pattern where nothing reads a group
/// while matching does not record captures. See
/// <c>docs/plan/2026-10-11-capture-free-matching.md</c>, amendment 1.
/// </summary>
/// <remarks>
/// <para>
/// The equivalence rows compare each capture-free call with the call that builds a
/// <see cref="Match"/>, which keeps captures on: the answers must be the same whether the engine
/// records captures or not. The rows cover every kind of group reader (backreferences, group
/// conditions, group calls and recursion) and every feature near the group arms (lookarounds,
/// atomic groups, <c>\K</c>, verbs, reverse, POSIX, fuzzy, BESTMATCH, ENHANCEMATCH, branch reset,
/// named lists), so an exclusion that is deleted or too narrow turns a row red.
/// </para>
/// <para>
/// The witness rows are the answers that change if a reader's exclusion is deleted: with group 1
/// never recorded, <c>\1</c> fails and <c>(?(1)...)</c> takes its "no" branch. Every expected value
/// is upstream's (regex 2026.9.10, 2026-10-14).
/// </para>
/// </remarks>
public sealed class CaptureFreeMatchingTests
{
    [Test]
    // Groups only nothing reads.
    [Arguments(@"^(\w+)@(\w+)\.com$", "bob@example.com")]
    [Arguments(@"(\w+\s?)+$", "one two three")]
    [Arguments(@"(\w+\s?)+$", "one two three!")]
    [Arguments(@"(a+)+c", "aaaaab")]
    [Arguments(@"(a*)*b", "aaab aab b")]
    [Arguments(@"((a)|b)+", "abba cab")]
    [Arguments(@"(?|(a)|(b))+", "abab xb")]
    [Arguments(@"(?:(a)|b)+?c", "abac bc")]
    [Arguments(@"()+x", "xx")]
    [Arguments(@"(?=(a))a", "aaa")]
    [Arguments(@"(?<=(a))b", "abab")]
    [Arguments(@"(?!(a))\w", "abc")]
    [Arguments(@"(?>(a+))b", "aaab aa")]
    [Arguments(@"(a+)\Kb", "aab ab b")]
    [Arguments(@"(a)(*SKIP)(*FAIL)|(b)", "abab")]
    [Arguments(@"(a+)(*PRUNE)b|(\w)", "aaac")]
    [Arguments(@"(?r)(a)+b", "aab ab")]
    [Arguments(@"(?p)(a|ab)(c|bcd)", "abcd abc")]
    [Arguments(@"(?(DEFINE)(?<d>\d))(?&d)+", "12 3")]
    // Group calls and recursion that read no group.
    [Arguments(@"(a|b(?1))c", "abac bbac")]
    [Arguments(@"(\((?:[^()]|(?1))*\))", "(a(b)c) (()")]
    [Arguments(@"\((?:[^()]|(?R))*\)", "((x)) (")]
    // Fuzzy.
    [Arguments(@"(?:(ab)c){e<=1}", "abd xbc")]
    [Arguments(@"(cat){i<=1,s<=1}", "a cut and a cart")]
    [Arguments(@"(?b)(\w+){e<=1}x", "abcx abx")]
    [Arguments(@"(?e)(dog){e<=2}", "a dgo and a dog")]
    [Arguments(@"(?r)(ab){e<=1}", "xab ac")]
    // Every reader kind: these keep captures on, and the answers must not move either.
    [Arguments(@"(a)\1", "aa ab")]
    [Arguments(@"(?i)(a)\1", "aA")]
    [Arguments(@"(?r)\1(a)", "aa")]
    [Arguments(@"(?<n>a)\g<n>", "aa")]
    [Arguments(@"(a)?(?(1)b|c)", "ab c")]
    [Arguments(@"(?<x>a)?(?(x)b|c)", "ab")]
    [Arguments(@"(?:(a)|b)+(?(1)c|b)", "abbb")]
    [Arguments(@"(a)(?1)\1", "aaa")]
    [Arguments(@"(?=(a))\1", "a")]
    [Arguments(@"(a)(?:\1){e<=1}", "ab")]
    [Arguments(@"(a)(?:x|\1)*c", "aaaxc")]
    public void A_call_that_reads_no_group_answers_as_the_call_that_does(string pattern, string subject)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        AnswersAgree(regex, subject);
    }

    [Test]
    public void A_named_list_inside_a_group_answers_the_same_without_captures()
    {
        var words = new Dictionary<string, IReadOnlyCollection<string>> { ["w"] = ["cat", "dog"] };
        var regex = new FuzzyRegex(@"(\L<w>\s?)+", FuzzyRegexOptions.None, words);

        AnswersAgree(regex, "cat dog bird dogcat");
    }

    [Test]
    // With group 1 never recorded, '\1' would fail and '(?(1)...)' would take its 'no' branch.
    [Arguments(@"(a)\1", "aa", 0, 2)]
    [Arguments(@"(a)?(?(1)b|c)", "ab", 0, 2)]
    [Arguments(@"(?<x>a)?(?(x)b|c)", "ab", 0, 2)]
    [Arguments(@"(?i)(a)\1", "aA", 0, 2)]
    [Arguments(@"(?r)\1(a)", "aa", 0, 2)]
    [Arguments(@"(?:(a)|b)+(?(1)c|b)", "abbb", 1, 4)]
    [Arguments(@"(a)(?1)\1", "aaa", 0, 3)]
    [Arguments(@"(a)(?:\1){e<=1}", "ab", 0, 2)]
    [Arguments(@"(?=(a))\1", "a", 0, 1)]
    public void A_pattern_that_reads_a_group_while_matching_still_sees_it(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        regex.IsMatch(subject).Should().BeTrue();
        regex.Count(subject).Should().Be(1);
        Spans(regex, subject).Should().Equal((start, end - start));
        regex.Replace(subject, "X").Should().Be(string.Concat(subject.AsSpan(0, start), "X", subject.AsSpan(end)));
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"^(\w+)@(\w+)\.com$", "bob@example.com")]
    [Arguments(@"(\w+\s?)+$", "one two three")]
    [Arguments(@"((a)|b)+c", "abbac")]
    [Arguments(@"(?=(a))a(?>(b))", "ab ab")]
    public void A_call_that_reads_no_group_records_no_capture(string pattern, string subject)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        // The control: a Match records them, so the counter can see them.
        CapturesSavedBy(() => regex.Match(subject)).Should().BePositive();

        CapturesSavedBy(() => regex.IsMatch(subject)).Should().Be(0, "IsMatch reads no group");
        CapturesSavedBy(() => regex.IsMatchAtStart(subject)).Should().Be(0, "IsMatchAtStart reads no group");
        CapturesSavedBy(() => regex.Count(subject)).Should().Be(0, "Count reads no group");
        CapturesSavedBy(() => Spans(regex, subject)).Should().Be(0, "a span enumeration reads no group");
        CapturesSavedBy(() => regex.Replace(subject, "X")).Should().Be(0, "a literal replacement reads no group");
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(a)\1", "aa")]
    [Arguments(@"(a)?(?(1)b|c)", "ab")]
    public void A_call_on_a_pattern_that_reads_a_group_still_records_captures(string pattern, string subject)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        CapturesSavedBy(() => regex.IsMatch(subject)).Should().BePositive("the pattern reads group 1 while it matches");
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_replacement_that_reads_a_group_still_records_captures()
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(@"(\w+)@(\w+)", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string replaced = "";

        CapturesSavedBy(() => replaced = regex.Replace("bob@example", @"\2@\1")).Should().BePositive();
        replaced.Should().Be("example@bob");
    }

    private static void AnswersAgree(FuzzyRegex regex, string subject)
    {
        MatchCollection matches = regex.Matches(subject);
        (int, int)[] expected = [.. matches.Select(static m => (m.Index, m.Length))];

        regex.IsMatch(subject).Should().Be(regex.Match(subject).Success, "IsMatch");
        regex.IsMatchAtStart(subject).Should().Be(regex.MatchAtStart(subject).Success, "IsMatchAtStart");
        regex.IsFullMatch(subject).Should().Be(regex.FullMatch(subject).Success, "IsFullMatch");
        regex.Count(subject).Should().Be(matches.Count, "Count");
        regex
            .Count(subject, overlapped: true)
            .Should()
            .Be(regex.EnumerateMatches(subject, overlapped: true).Count(), "an overlapped Count");
        Spans(regex, subject).Should().Equal(expected, "a span enumeration");
        regex.Replace(subject, "X").Should().Be(regex.Replace(subject, static _ => "X"), "a literal Replace");
        regex
            .Replace(subject, @"<\g<0>>")
            .Should()
            .Be(regex.Replace(subject, static m => $"<{m.Value}>"), "a whole-match Replace");
    }

    private static List<(int, int)> Spans(FuzzyRegex regex, string subject)
    {
        List<(int, int)> spans = [];
        foreach (ValueMatch match in regex.EnumerateMatches(subject.AsSpan()))
        {
            spans.Add((match.Index, match.Length));
        }

        return spans;
    }

    private static long CapturesSavedBy(Action action)
    {
        long before = WorkCounter.CapturesSaved;
        action();
        return WorkCounter.CapturesSaved - before;
    }
}
