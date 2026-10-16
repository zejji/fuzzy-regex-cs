using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D88: a <c>\K</c> inside a construct that cuts the backtracking stack (a lookaround, a
/// condition's test, an atomic group, a possessive repeat) is undone when matching backtracks past
/// the construct, as every other effect of a failed path is.
/// </summary>
/// <remarks>
/// <para>
/// <c>\K</c> moves the reported start and pushes an entry that moves it back on backtracking. A
/// construct that succeeds throws away every entry its body pushed, that one included, so upstream
/// (<c>regex</c> 2026.9.10) lets the moved start outlive the path that moved it. Two things follow.
/// A later alternative reports a start it never set: <c>(?=a\K)x|a</c> over 'a' is (1, 1), where
/// the first alternative failed and the answer should be <c>a</c>'s, (0, 1). And when the whole
/// attempt fails, the search steps on from the moved start and skips start positions:
/// <c>(?=ab\K)x|b</c> over 'ab' is None upstream, which finds (1, 2) when told to start at 1.
/// </para>
/// <para>
/// The port's rule (owner's semantic-decision rule, 2026-10-16, DECISIONS): backtracking past the
/// construct puts the start back. Ruby 3.2.3, the one surveyed engine that accepts <c>\K</c> in a
/// lookaround, answers every lookaround row here the same way; PCRE2 10.47 and Ruby do so for
/// atomic groups and possessive repeats, where Perl 5.42.3 keeps upstream's answer. Perl and PCRE2
/// refuse <c>\K</c> in a lookaround. A match whose path does not backtrack past the construct is
/// unchanged, and so is a verb's cut: <c>a\K(*PRUNE)x|a</c> stays None, as in upstream and PCRE2.
/// Evidence: <c>.scratch/s1016/night/small/d88-analysis.md</c> and
/// <c>.scratch/s1016/night/d88-review/review.md</c>.
/// </para>
/// </remarks>
public sealed class KeepAcrossCutTests
{
    private static (int, int) Span(Match m) => (m.Index, m.Index + m.Length);

    // Upstream's answer is in the comment after each row.
    [Test]
    [Arguments(@"(?=ab\K)x|b", "ab", 1, 2)] // None: the failed attempt at 0 skipped 1
    [Arguments(@"(?=a\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?=ab\K)ac|ab", "ab", 0, 2)] // (2, 2)
    [Arguments(@"ax(?<=\Kx)y|axz", "axz", 0, 3)] // (1, 3)
    [Arguments(@"(?:(?=a\K)x)?a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?=a\K(?=b\K))x|a", "ab", 0, 1)] // nested \Ks, one cut each
    [Arguments(@"(?=a\Kb\K)x|ab", "ab", 0, 2)] // two \Ks in one body
    public void A_positive_lookaround_backtracked_past_puts_the_start_back(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // A negative lookaround whose body matched has failed, so the body's \K belongs to a path that
    // was rejected outright.
    [Test]
    [Arguments(@"(?!a\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?!ab\K)|b", "ab", 1, 1)] // None
    [Arguments(@"a(?<!a\K)x|ab", "ab", 0, 2)] // (1, 2)
    public void A_negative_lookaround_whose_body_matched_leaves_the_start_alone(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // `(?(?=ab)x|b)` gives (1, 2): the \K in the test must not change that.
    [Test]
    public void A_condition_backtracked_past_puts_the_start_back()
    {
        Span(new FuzzyRegex(@"(?(?=ab\K)x|b)").Match("ab")).Should().Be((1, 2)); // None
    }

    // Perl 5.42.3 keeps upstream's answer here; PCRE2 10.47 and Ruby 3.2.3 give the port's.
    [Test]
    [Arguments(@"(?>a\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?:a\K)++x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?>a\K)?a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?>a\K|)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?>a\Kb)c|ab", "ab", 0, 2)] // (1, 2)
    [Arguments(@"(?>a\K(?>b\K))x|ab", "ab", 0, 2)] // (2, 2)
    public void An_atomic_group_backtracked_past_puts_the_start_back(string pattern, string subject, int start, int end)
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // Paths that never backtrack past the construct keep the \K, crossed spans included.
    [Test]
    [Arguments(@"(?=a\K)a", "a", 1, 1)]
    [Arguments(@"(?>a\K)b", "ab", 1, 2)]
    [Arguments(@"foo\Kbar", "foobar", 3, 6)]
    [Arguments(@"a(?=b\K)", "ab", 2, 1)]
    [Arguments(@"(?=a\K)(?:x|a)", "a", 1, 1)]
    [Arguments(@"ax(?<=\Kx)z", "axz", 1, 3)]
    [Arguments(@"(?!a\Kb)a", "ac", 0, 1)]
    [Arguments(@"foo(?=bar\K)", "foobar", 6, 3)]
    [Arguments(@"(?=foo\Kbar)|foo", "foobaz", 0, 3)]
    [Arguments(@"(?>a\K)a", "aa", 1, 2)]
    [Arguments(@"a\K(?=b)|ab", "ab", 1, 1)]
    public void A_path_that_keeps_the_construct_keeps_its_K(string pattern, string subject, int start, int end)
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    [Test]
    public void A_capture_beside_the_K_is_unchanged()
    {
        Match m = new FuzzyRegex(@"(?=(a)\K)(?:\1x|a)").Match("a");

        Span(m).Should().Be((1, 1));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 1));
    }

    [Test]
    public void A_condition_that_fails_with_its_K_undone_still_fails()
    {
        new FuzzyRegex(@"(?(?=a\K)ax|ab)").Match("ab").Success.Should().BeFalse();
    }

    // A verb abandons the attempt rather than backtracking past anything, so its cut is left
    // alone: upstream and PCRE2 10.47 both answer None.
    [Test]
    [Arguments(@"a\K(*PRUNE)x|a")]
    [Arguments(@"(?=a\K)(*PRUNE)x|a")]
    public void A_verb_does_not_undo_a_K(string pattern)
    {
        new FuzzyRegex(pattern).Match("a").Success.Should().BeFalse();
    }

    // A verb the group's cut threw away never acted, so this is the plain atomic case. Upstream
    // (1, 1); PCRE2 10.47 (0, 1).
    [Test]
    public void A_verb_discarded_with_the_atomic_group_does_not_keep_the_K()
    {
        Span(new FuzzyRegex(@"(?>a\K(*PRUNE))x|a").Match("a")).Should().Be((0, 1));
    }

    // A reversed pattern runs right to left, so the construct the alternative backtracks past sits
    // on the right of `x`. Upstream's answer is in the comment.
    [Test]
    [Arguments(@"(?r)x(?<=\Ka)|a", "a", 0, 1)] // (0, 0)
    [Arguments(@"(?r)x(?<!\Ka)|a", "a", 0, 1)] // (0, 0)
    [Arguments(@"(?r)x(?>\Ka)|a", "a", 0, 1)] // (0, 0)
    [Arguments(@"(?r)x(?:\Ka)++|a", "a", 0, 1)] // (0, 0)
    [Arguments(@"(?r)x(?<=\Kab)|b", "ab", 1, 2)] // (1, 0)
    [Arguments(@"(?r)(?(?<=\Kab)x|a)", "ab", 0, 1)] // None
    [Arguments(@"(?r)a(?>\Ka)", "aa", 0, 1)] // control: unchanged
    public void A_reversed_construct_backtracked_past_puts_the_start_back(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // A fuzzy body is cut the same way, and a negative lookaround in a fuzzy section tries an
    // insertion in front of itself before failing (ledger entry 50), which must not carry the
    // body's \K forwards. Upstream's answer is in the comment.
    [Test]
    [Arguments(@"(?=(?:ab){e<=1}\K)x|b", "ab", 1, 2)] // None
    [Arguments(@"(?=(?:a){e<=1}\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?>(?:a){e<=1}\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?!(?:a){e<=1}\K)x|a", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?e)(?=a\K)x|(?:a){e<=1}", "a", 0, 1)] // (1, 1)
    [Arguments(@"(?r)x(?<=\K(?:a){e<=1})|a", "a", 0, 1)] // (0, 0)
    [Arguments(@"(?r)x(?<!\K(?:a){e<=1})|a", "a", 0, 1)] // (0, 0)
    public void A_fuzzy_construct_backtracked_past_puts_the_start_back(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // The \K reached through a group call or inside a capture in the body. Upstream (1, 2), (1, 1).
    [Test]
    [Arguments(@"(?>(?1)b)x|ab|(a\K)", "ab", 0, 2)]
    [Arguments(@"(?=(a\K))x|a", "a", 0, 1)]
    public void A_K_inside_a_call_or_a_capture_is_undone_too(string pattern, string subject, int start, int end)
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // A negative condition whose body matched takes the no branch with what the body did, as D54
    // keeps its captures, so `(?(?!a\K)x|ab)` answers as `(?(?=a\K)ab|x)`, (1, 2), upstream's
    // answer too. Backtracking past the condition still puts the start back: upstream (1, 2).
    [Test]
    public void A_negative_condition_keeps_its_K_until_backtracked_past()
    {
        Span(new FuzzyRegex(@"(?(?!a\K)x|ab)").Match("ab")).Should().Be((1, 2));
        Span(new FuzzyRegex(@"(?(?!a\K)x|ac)|ab").Match("ab")).Should().Be((0, 2));
    }

    [Test]
    public void Anchored_and_partial_matches_put_the_start_back()
    {
        Span(new FuzzyRegex(@"(?=a\K)x|a").MatchAtStart("a")).Should().Be((0, 1)); // (1, 1)
        Span(new FuzzyRegex(@"(?>a\K)x|a").FullMatch("a")).Should().Be((0, 1)); // (1, 1)
        Span(new FuzzyRegex(@"(?=a\K)x|a").Match("a", partial: true)).Should().Be((0, 1)); // (1, 1)

        // Attempt 0 fails for every continuation, since x cannot sit where b\Z needs b, so the
        // attempt at 1 is reached whatever follows and b there is complete. Before D88 the
        // discarded \K moved the next start, which made it a partial, pinned until now in
        // PartialVerbRuleTests. Upstream 2026.9.10 answers a partial at (2, 2).
        Match touched = new FuzzyRegex(@"a(?=b\Z)(?>\K)x|b").Match("ab", partial: true);
        touched.PartialMatch.Should().BeFalse();
        Span(touched).Should().Be((1, 2));
        Span(new FuzzyRegex(@"a(?=b\Z)(?>\K)x|b").Match("ab")).Should().Be((1, 2)); // None

        // The partial path did not fail, so its \K stands: (1, 1) as upstream.
        Match partial = new FuzzyRegex(@"(?>a\K)x|ab").Match("a", partial: true);
        partial.PartialMatch.Should().BeTrue();
        Span(partial).Should().Be((1, 1));
    }

    [Test]
    public void A_scan_finds_the_start_a_failed_attempt_used_to_skip()
    {
        new FuzzyRegex(@"(?=ab\K)x|b").EnumerateMatches("ab").Select(Span).Should().Equal((1, 2)); // []
        new FuzzyRegex(@"(?=y\K)").EnumerateMatches("yy").Take(3).Select(Span).Should().Equal((1, 0), (2, 1));
        new FuzzyRegex(@"(?=a\K)x|a").EnumerateMatches("aa").Select(Span).Should().Equal((0, 1), (1, 2));
        new FuzzyRegex(@"(?r)x(?<=\Ka)|a").EnumerateMatches("aa").Select(Span).Should().Equal((1, 2), (0, 1));
        new FuzzyRegex(@"(?>a\K)x|a")
            .EnumerateMatches("aa", overlapped: true)
            .Select(Span)
            .Should()
            .Equal((0, 1), (1, 2)); // [(1, 1)]
        new FuzzyRegex(@"(?!a\K)x|a").Split("aa").Should().Equal("", "", ""); // ['a', 'a', '']
        new FuzzyRegex(@"(?(?=ab\K)x|b)").Replace("abab", @"<\g<0>>").Should().Be("a<b>a<b>"); // 'aba<b>'
    }

    // The scan the analysis predicted: the second attempt's empty match at 0 is banned, so it
    // backtracks into `y` with the start put back. Upstream yields (1, 0) for ever.
    [Test]
    public void The_ban_after_a_crossed_match_backtracks_into_the_other_alternative()
    {
        new FuzzyRegex(@"(?=y\K)|y").EnumerateMatches("y").Take(3).Select(Span).Should().Equal((1, 0), (0, 1));
        new FuzzyRegex(@"(?=y\K)|y")
            .EnumerateMatches("y", overlapped: true)
            .Take(3)
            .Select(Span)
            .Should()
            .Equal((1, 0));
    }
}
