using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// What leftmost-longest matching does that the ported suite does not pin: the compile-time option
/// as well as the inline <c>(?p)</c>, that leftmost still beats longest, what happens to the
/// captures of the shorter match that lost, the reverse arm of <c>check_posix_match</c>, and the
/// flag through the operations other than <c>match</c>.
/// </summary>
/// <remarks>
/// <para>
/// The eight ported tests (<c>Ported/Regressions/RegressionsPosixMatchingTests.cs</c>) are all
/// forward, all through the inline <c>(?p)</c>, and all a <c>match</c> or a <c>sub</c>. Everything
/// here is a case they leave open, and every expected value was measured against <c>regex</c>
/// 2026.7.19 on 2026-09-12 and is quoted beside the assertion.
/// </para>
/// <para>
/// None of it is derivable by reading upstream's C. <c>check_posix_match</c>
/// (<c>upstream/src/_regex.c:11602</c>) compares two lengths and says nothing about which captures
/// survive; that is <c>save_best_match</c>'s snapshot and <c>restore_best_match</c>'s copy-back
/// acting together, and the repeated-group case below is the only assertion that tells them apart.
/// </para>
/// </remarks>
public sealed class PosixMatchingTests
{
    [Test]
    public void The_compile_time_option_and_the_inline_flag_are_the_same_flag()
    {
        // regex.compile(r'a|ab|abc', regex.POSIX).search('abcd')     -> (0, 3)
        // regex.compile(r'(?p)a|ab|abc').search('abcd')              -> (0, 3)
        FuzzyRegex.Match("abcd", "a|ab|abc", FuzzyRegexOptions.Posix).Value.Should().Be("abc");
        FuzzyRegex.Match("abcd", "(?p)a|ab|abc").Value.Should().Be("abc");

        // Without it, leftmost-first takes the first alternative: -> (0, 1)
        FuzzyRegex.Match("abcd", "a|ab|abc").Value.Should().Be("a");
    }

    [Test]
    public void Leftmost_still_beats_longest()
    {
        // The whole name is leftmost-*longest*: the length only decides between matches that start
        // in the same place, because the FAILURE backtrack case returns the best match rather than
        // advancing the search once one has been found.
        // regex.compile(r'(?p)b|aaa').search('xbyaaa')   -> (1, 2), not the longer (3, 6)
        Match m = FuzzyRegex.Match("xbyaaa", "(?p)b|aaa");

        (m.Index, m.Length).Should().Be((1, 1));
    }

    [Test]
    public void A_group_the_shorter_match_set_is_unset_in_the_longer_one()
    {
        // This is what restore_best_match exists for. Without the capture copy-back the overall span
        // would still be right and group 1 would still be holding the losing branch's text.
        // regex.compile(r'(?p)(a)|(ab)').search('ab')  -> (0, 2), groups [(-1,-1), (0,2)], lastindex 2
        Match posix = FuzzyRegex.Match("ab", "(?p)(a)|(ab)");

        (posix.Index, posix.Length).Should().Be((0, 2));
        posix.Groups[1].Success.Should().BeFalse();
        posix.Groups[2].Value.Should().Be("ab");
        posix.LastGroupNumber.Should().Be(2);

        // regex.compile(r'(a)|(ab)').search('ab')      -> (0, 1), groups [(0,1), (-1,-1)], lastindex 1
        Match plain = FuzzyRegex.Match("ab", "(a)|(ab)");

        (plain.Index, plain.Length).Should().Be((0, 1));
        plain.Groups[1].Value.Should().Be("a");
        plain.Groups[2].Success.Should().BeFalse();
        plain.LastGroupNumber.Should().Be(1);
    }

    [Test]
    public void A_repeated_group_keeps_only_the_best_matchs_capture_list()
    {
        // The sharpest test of the snapshot: the winning path went round the loop twice through the
        // SECOND alternative, so group 1 must have no captures at all - not the ones the losing
        // single-'a' path recorded on the way.
        // regex.compile(r'(?p)(?:(a)|(ab))+').search('abab')
        //   -> (0, 4), spans(1) [], spans(2) [(0,2), (2,4)], lastindex 2
        Match posix = FuzzyRegex.Match("abab", "(?p)(?:(a)|(ab))+");

        (posix.Index, posix.Length).Should().Be((0, 4));
        posix.Groups[1].Success.Should().BeFalse();
        posix.Groups[1].Captures.Should().BeEmpty();
        posix.Groups[2].Captures.Select(static c => c.Value).Should().Equal("ab", "ab");
        posix.LastGroupNumber.Should().Be(2);

        // regex.compile(r'(?:(a)|(ab))+').search('abab')
        //   -> (0, 1), spans(1) [(0,1)], spans(2) [], lastindex 1
        Match plain = FuzzyRegex.Match("abab", "(?:(a)|(ab))+");

        (plain.Index, plain.Length).Should().Be((0, 1));
        plain.Groups[1].Captures.Select(static c => c.Value).Should().Equal("a");
        plain.Groups[2].Captures.Should().BeEmpty();
    }

    [Test]
    public void A_reversed_pattern_measures_its_length_the_other_way_round()
    {
        // The 'state->reverse' arm of check_posix_match (:11610-11613), which no ported test reaches:
        // matching right to left, 'text_pos' is the START of the match and 'match_pos' its end, so
        // the length is 'match_pos - text_pos' rather than the other way about.
        // regex.compile(r'(?r)(?p)a|ba|cba').search('xcba')  -> (1, 4)
        // regex.compile(r'(?r)a|ba|cba').search('xcba')      -> (3, 4)
        FuzzyRegex.Match("xcba", "(?r)(?p)a|ba|cba").Value.Should().Be("cba");
        FuzzyRegex.Match("xcba", "(?r)a|ba|cba").Value.Should().Be("a");
    }

    [Test]
    public void A_reversed_pattern_restores_the_reversed_best_matchs_captures()
    {
        // Both halves at once, and the capture spans are not a mirror of the forward case: going
        // right to left the winning loop closes group 2 at (0, 2) where the forward run closes it at
        // (2, 4).
        // regex.compile(r'(?r)(?p)(?:(a)|(ba))+').search('baba')
        //   -> (0, 4), groups [(-1,-1), (0,2)], lastindex 2
        Match posix = FuzzyRegex.Match("baba", "(?r)(?p)(?:(a)|(ba))+");

        (posix.Index, posix.Length).Should().Be((0, 4));
        posix.Groups[1].Success.Should().BeFalse();
        (posix.Groups[2].Index, posix.Groups[2].Length).Should().Be((0, 2));
        posix.LastGroupNumber.Should().Be(2);

        // regex.compile(r'(?r)(?:(a)|(ba))+').search('baba')
        //   -> (3, 4), groups [(3,4), (-1,-1)], lastindex 1
        Match plain = FuzzyRegex.Match("baba", "(?r)(?:(a)|(ba))+");

        (plain.Index, plain.Length).Should().Be((3, 1));
        (plain.Groups[1].Index, plain.Groups[1].Length).Should().Be((3, 1));
        plain.Groups[2].Success.Should().BeFalse();
    }

    [Test]
    public void The_flag_reaches_every_operation_and_not_just_match()
    {
        // regex.compile(r'(?p)a|ab').findall('abab')      -> ['ab', 'ab']
        FuzzyRegex.Matches("abab", "(?p)a|ab").Select(static m => m.Value).Should().Equal("ab", "ab");
        FuzzyRegex.Matches("abab", "a|ab").Select(static m => m.Value).Should().Equal("a", "a");

        // regex.compile(r'(?p)a|ab').split('xabaty')      -> ['x', '', 'ty']
        FuzzyRegex.Split("xabaty", "(?p)a|ab").Should().Equal("x", "", "ty");

        // regex.compile(r'(?p)a|ab').sub('-', 'abab')     -> '--'
        FuzzyRegex.Replace("abab", "(?p)a|ab", "-").Should().Be("--");

        // regex.compile(r'(?p)a|ab').fullmatch('ab')      -> (0, 2)
        FuzzyRegex.FullMatch("ab", "(?p)a|ab").Value.Should().Be("ab");

        // A fullmatch that only the longer alternative can satisfy is the case where the POSIX hook
        // has to run *after* the match_all check, not before it: 'a' reaches SUCCESS first and is
        // rejected for not spanning the slice, so it must never be saved as the best match.
        FuzzyRegex.FullMatch("ab", "a|ab").Value.Should().Be("ab");
    }

    [Test]
    public void A_zero_width_best_match_is_still_a_match()
    {
        // Nothing longer exists, so the first match found is also the best one - the arm where
        // check_posix_match saves once and never improves on it.
        // regex.compile(r'(?p)a*').search('bbb')   -> (0, 0)
        Match m = FuzzyRegex.Match("bbb", "(?p)a*");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 0));
    }

    [Test]
    public void Posix_matching_is_exhaustive_and_a_timeout_still_applies()
    {
        // POSIX does not stop at the first match, so a pattern that was cheap at first-match can be
        // expensive here - measured on 2026-09-12 at 15ms against 17.9s for one wave row in a Debug
        // build, the same shape upstream itself takes 0.69s over. MatchTimeout is therefore worth
        // more under this flag than without it, and it is still honoured: the cancellation check is
        // in the backtrack loop as well as the advance loop.
        var regex = new FuzzyRegex(@"(?p)(a|a|aa)*b", FuzzyRegexOptions.None, System.TimeSpan.FromMilliseconds(50));

        Action act = () => regex.Match(new string('a', 40));

        act.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();
    }
}
