using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D92: after a <c>\K</c>, a <c>(*PRUNE)</c> or <c>(*SKIP)</c> that ends the attempt must not make
/// the search skip start positions. The next attempt steps on from where the failed attempt began.
/// </summary>
/// <remarks>
/// <para>
/// <c>a\K(*PRUNE)b</c> over 'aab' is the smallest case. The attempt at 0 matches <c>a</c>, and
/// <c>\K</c> moves the reported start to 1. The verb then throws away the backtracking entries,
/// including the one that would move the start back, and <c>b</c> fails at 1. Upstream
/// (<c>regex</c> 2026.9.10) steps on from the reported start, 1, so its next attempt is at 2 and
/// the match at 1 is never tried: it answers None. Perl 5.42.3, PCRE2 10.47 and Boost.Regex answer
/// (2, 3). pcre2pattern says a <c>(*PRUNE)</c> that ends an attempt lets the search "advance to
/// the next starting character" as usual, and <c>\K</c> changes what the match reports, not where
/// the attempt began.
/// </para>
/// <para>
/// <c>(*SKIP)</c> moves the next start to where the verb was reached, which a <c>\K</c> does not
/// change. A skip position that is not ahead of the attempt is ignored, and the search bumps along
/// from the attempt. PCRE2 and Boost agree on every row here. Perl ignores the skip on two rows
/// (<c>aa\K(*SKIP)b</c> and <c>a\Ka(*SKIP)b</c> over 'aaab' give (3, 4) and (2, 4) there, None in
/// PCRE2 and Boost); the port follows PCRE2, the reference for verbs. Evidence:
/// <c>.scratch/s1016/d92/</c>.
/// </para>
/// </remarks>
public sealed class KeepBeforeVerbTests
{
    private static (int, int) Span(Match m) => (m.Index, m.Index + m.Length);

    private static (int, int)[] Spans(FuzzyRegex regex, string subject, bool overlapped = false) =>
        [.. regex.Matches(subject, overlapped: overlapped).Select(Span)];

    // Upstream answers None to every row.
    [Test]
    [Arguments(@"a\K(*PRUNE)b", "aab", 2, 3)]
    [Arguments(@"a\K(*SKIP)b", "aab", 2, 3)]
    [Arguments(@".\Kb*(*SKIP)ab", "xxab", 2, 4)]
    [Arguments(@"a\Ka(*PRUNE)b", "aaab", 2, 4)]
    [Arguments(@"a\K(*PRUNE)x|b", "ab", 1, 2)]
    [Arguments(@"a\K(*SKIP)x|b", "ab", 1, 2)]
    public void A_search_tries_the_start_after_the_failed_attempt(string pattern, string subject, int start, int end)
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }

    // The skip position, not the \K, decides the next start: the attempt at 0 reaches the verb at 2,
    // so no attempt starts at 1. PCRE2, Boost and upstream answer None; Perl does not skip.
    [Test]
    [Arguments(@"aa\K(*SKIP)b", "aaab")]
    [Arguments(@"a\Ka(*SKIP)b", "aaab")]
    public void A_skip_still_moves_the_next_start_to_the_verb(string pattern, string subject)
    {
        new FuzzyRegex(pattern).Match(subject).Success.Should().BeFalse();
    }

    [Test]
    [Arguments(@"a\K(*PRUNE)b")]
    [Arguments(@"a\K(*SKIP)b")]
    public void Matches_finds_every_match(string pattern)
    {
        // Upstream []; PCRE2, Perl and Boost (2, 3) and (5, 6).
        Spans(new FuzzyRegex(pattern), "aabaab").Should().Equal((2, 3), (5, 6));
    }

    [Test]
    public void An_overlapped_scan_finds_every_match()
    {
        // Upstream []. Each failed attempt steps one from where it began.
        Spans(new FuzzyRegex(@"a\K(*PRUNE)b"), "aabaab", overlapped: true).Should().Equal((2, 3), (5, 6));
    }

    // A reversed pattern runs right to left, so these mirror the forward rows: the verb and \K sit
    // to the left of the item they follow. Upstream None, [] and (0, 2).
    [Test]
    public void A_reversed_search_tries_the_start_after_the_failed_attempt()
    {
        Span(new FuzzyRegex(@"(?r)b(*PRUNE)\Ka").Match("baa")).Should().Be((0, 1));
        Spans(new FuzzyRegex(@"(?r)b(*PRUNE)\Ka"), "baabaa").Should().Equal((3, 4), (0, 1));
        Span(new FuzzyRegex(@"(?r)ba(*SKIP)\K.").Match("baxx")).Should().Be((0, 2));
    }

    [Test]
    public void A_partial_search_tries_the_start_after_the_failed_attempt()
    {
        // The attempt at 1 runs out of text after `a\Ka`, so it is the partial, with the start \K
        // reported, as upstream reports `a\Kab` over 'aa' as (1, 2). Upstream (3, 3); PCRE2, which
        // reports a partial from where its attempt began, (1, 3).
        Match m = new FuzzyRegex(@"a\K(*PRUNE)ab").Match("aaa", partial: true);

        m.PartialMatch.Should().BeTrue();
        Span(m).Should().Be((2, 3));
    }

    [Test]
    public void A_match_every_continuation_reaches_is_complete()
    {
        // Attempt 0 fails for every continuation, since x cannot sit where b\Z needs b, so the
        // attempt at 1 is reached whatever follows and b there is complete. Before D92 the \K moved
        // the next start past 1, which made it a partial, pinned until now in PartialVerbRuleTests.
        // Upstream 2026.9.10 answers a partial at (2, 2), and None without partial matching.
        Match m = new FuzzyRegex(@"a(?=b\Z)\K(*PRUNE)x|b").Match("ab", partial: true);

        m.PartialMatch.Should().BeFalse();
        Span(m).Should().Be((1, 2));
        Span(new FuzzyRegex(@"a(?=b\Z)\K(*PRUNE)x|b").Match("ab")).Should().Be((1, 2));
    }

    [Test]
    public void A_fuzzy_section_after_the_verb_does_not_change_where_the_search_goes_next()
    {
        // The attempt at 0 spends its substitution on the 'a' at 1 and then fails at 'b'. The
        // attempt at 1 matches exactly. Upstream None and [].
        var regex = new FuzzyRegex(@"a\K(*PRUNE)(?:b){s<=1}x");

        Span(regex.Match("aabx")).Should().Be((2, 4));
        Spans(regex, "aabx").Should().Equal((2, 4));
    }

    // Controls, green before and after: every engine gives these.
    [Test]
    [Arguments(@"a(*PRUNE)b", "aab", 1, 3)]
    [Arguments(@"a\Kb", "aab", 2, 3)]
    [Arguments(@".b*(*SKIP)ab", "xxab", 1, 4)]
    [Arguments(@"a+\K(*PRUNE)b", "aaab", 3, 4)]
    [Arguments(@"(?:a\K(*PRUNE))+b", "aab", 2, 3)]
    public void Controls(string pattern, string subject, int start, int end)
    {
        Span(new FuzzyRegex(pattern).Match(subject)).Should().Be((start, end));
    }
}
