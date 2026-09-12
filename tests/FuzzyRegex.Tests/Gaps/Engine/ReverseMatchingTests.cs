using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The <c>_REV</c> opcodes at match time: the <c>(?r)</c> flag, every consuming opcode's backward
/// twin, and the backward codepoint step over UTF-16.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value is upstream's, recorded against regex 2026.7.19 on 2026-09-01 with
/// <c>regex.match(pattern, subject)</c> over exactly the pattern text below, and quoted beside the
/// assertion. <b><c>regex.match</c> and not <c>regex.search</c></b>: upstream's
/// <c>search_start</c> prefilter, which this port defers to Phase 7, screens start positions with
/// predicates its own matcher does not use, so <c>search</c> can answer differently from
/// <c>match</c> on the same pair (S22's closing notes).
/// </para>
/// <para>
/// Where upstream's span is in codepoints and ours in UTF-16 code units - every astral row - both
/// are quoted, so a translation slip cannot hide behind a comment that agrees with the code.
/// </para>
/// <para>
/// These are gap tests because upstream's own suite reaches almost none of this. Its
/// <c>test_search_reverse</c> assertions go overwhelmingly through <c>findall</c> and
/// <c>finditer</c>, which are S25, so S23 unskipped eleven ported tests in total and not one of
/// them is astral. The two rows that matter most are the last two of
/// <see cref="Reverse_backreference_span"/>: a <c>REF_GROUP_FLD_REV</c> whose <em>captured</em>
/// text expands on folding is the one cell S22 recorded that the oracle wave cannot be trusted to
/// reach, and it is exactly the shape upstream's 2026.5.9 fix was about ("Reverse matching with
/// full unicode casefolding could lead to out-of-range string indexes",
/// <c>upstream/changelog.txt</c>).
/// </para>
/// <para>
/// <b>A backreference has to be written before its group to run at all under <c>(?r)</c>.</b>
/// Reverse execution takes the sequence from the right, so <c>(?r)(.)\1</c> reaches <c>\1</c>
/// while the group is still empty and never matches - verified against upstream, which answers
/// <c>None</c> for it. That is why every backreference row below reads <c>\1(...)</c>, which is
/// also the shape upstream's own reverse tests use.
/// </para>
/// </remarks>
public sealed class ReverseMatchingTests
{
    /// <summary>
    /// Every consuming <c>_REV</c> opcode, in four blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One character</b>: <c>ANY_REV</c>, <c>ANY_ALL_REV</c>, <c>CHARACTER_REV</c>,
    /// <c>PROPERTY_REV</c>, <c>RANGE_REV</c> and the <c>SET_*_REV</c> family, each anchored at the
    /// far end because that is where <c>(?r)</c> starts.
    /// </para>
    /// <para>
    /// <b>A string</b>: <c>STRING_REV</c>, <c>STRING_IGN_REV</c> and <c>STRING_FLD_REV</c>. The
    /// folding rows are the ones where the subject character's folding is longer than itself, so
    /// <c>STRING_FLD_REV</c>'s two sides advance at different rates - the only thing that tells it
    /// apart from <c>STRING_IGN_REV</c>.
    /// </para>
    /// <para>
    /// <b>A repeat</b>: <c>count_one</c>'s reverse walk and both <c>*_REPEAT_ONE</c> retreat paths,
    /// over subjects whose characters are two code units wide.
    /// </para>
    /// <para>
    /// <b>Zero width</b>: the boundary and anchor predicates have no <c>_REV</c> twin at all - they
    /// inspect both sides of a position, so direction cannot change their answer - and these rows
    /// pin that claim rather than leaving it asserted in a comment. <c>\G</c> is the exception that
    /// proves it: it matches where the search <em>started</em>, which under <c>(?r)</c> is the far
    /// end of the subject.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="index">The expected index, in UTF-16 code units.</param>
    /// <param name="length">The expected length, in UTF-16 code units.</param>
    [Test]
    // --- One backward character ------------------------------------------------------------
    // upstream: regex.match('(?r)a', 'xa').span() == (1, 2)
    [Arguments("(?r)a", "xa", 1, 1)]
    // upstream: regex.match('(?r).', 'a\U0001F600').span() == (1, 2) codepoints
    [Arguments("(?r).", "a\U0001F600", 1, 2)]
    // upstream: regex.match('(?rs).', 'a\n').span() == (1, 2)
    [Arguments("(?rs).", "a\n", 1, 1)]
    // upstream: regex.match('(?r)\U0001F600a', 'x\U0001F600a').span() == (1, 3) codepoints
    [Arguments("(?r)\U0001F600a", "x\U0001F600a", 1, 3)]
    // upstream: regex.match('(?r)[a-c]', 'zb').span() == (1, 2)
    [Arguments("(?r)[a-c]", "zb", 1, 1)]
    // upstream: regex.match('(?r)[^a-c]', 'az').span() == (1, 2)
    [Arguments("(?r)[^a-c]", "az", 1, 1)]
    // upstream: regex.match(r'(?r)\w', '-a').span() == (1, 2)
    [Arguments(@"(?r)\w", "-a", 1, 1)]
    // upstream: regex.match(r'(?r)\p{Lu}', 'aB').span() == (1, 2)
    [Arguments(@"(?r)\p{Lu}", "aB", 1, 1)]
    // upstream: regex.match('(?r)\U0001F600', '\U0001F600').span() == (0, 1) codepoints
    [Arguments("(?r)\U0001F600", "\U0001F600", 0, 2)]
    // The backward step has to clear a whole surrogate pair to reach the 'a' before it.
    // upstream: regex.match('(?r)a', '\U0001F600a').span() == (1, 2) codepoints
    [Arguments("(?r)a", "\U0001F600a", 2, 1)]
    // --- A backward string -----------------------------------------------------------------
    // upstream: regex.match('(?ri)ABC', 'xabc').span() == (1, 4)
    [Arguments("(?ri)ABC", "xabc", 1, 3)]
    // A pattern of two characters against one subject character that folds to both of them.
    // upstream: regex.match('(?rfi)ss', 'aß').span() == (1, 2)
    [Arguments("(?rfi)ss", "aß", 1, 1)]
    // ... and the other way round: one pattern character against two subject characters. The
    // parser folds the pattern to 'ss' first, so this is the same walk seen from the other side.
    // upstream: regex.match('(?rfi)ß', 'ass').span() == (1, 3)
    [Arguments("(?rfi)ß", "ass", 1, 2)]
    // U+FB06 LATIN SMALL LIGATURE ST folds to 'st'.
    // upstream: regex.match('(?rfi)st', 'aﬆ').span() == (1, 2)
    [Arguments("(?rfi)st", "aﬆ", 1, 1)]
    // A repeated string, so STRING_REV runs more than once and each run has to start where the
    // last one left off.
    // upstream: regex.match('(?r)(?:ab)+', 'xabab').span() == (1, 5)
    [Arguments("(?r)(?:ab)+", "xabab", 1, 4)]
    // --- A backward repeat -----------------------------------------------------------------
    // Two characters, four code units - the count is in characters and the span is not.
    // upstream: regex.match('(?r).{2}', 'a\U0001F600\U0001D518').span() == (1, 3) codepoints
    [Arguments("(?r).{2}", "a\U0001F600\U0001D518", 1, 4)]
    // upstream: regex.match('(?r)\U0001F600*', 'a\U0001F600\U0001F600').span() == (1, 3)
    // codepoints
    [Arguments("(?r)\U0001F600*", "a\U0001F600\U0001F600", 1, 4)]
    // Lazy, so the repeat consumes nothing it does not have to.
    // upstream: regex.match('(?r)\U0001F600*?x', '\U0001F600\U0001F600x').span() == (2, 3)
    // codepoints
    [Arguments("(?r)\U0001F600*?x", "\U0001F600\U0001F600x", 4, 1)]
    // upstream: regex.match('(?r).+', '\U0001F600\U0001F600').span() == (0, 2) codepoints
    [Arguments("(?r).+", "\U0001F600\U0001F600", 0, 4)]
    // upstream: regex.match('(?r)a+', 'baaa').span() == (1, 4)
    [Arguments("(?r)a+", "baaa", 1, 3)]
    // upstream: regex.match('(?r)a+?', 'baaa').span() == (3, 4)
    [Arguments("(?r)a+?", "baaa", 3, 1)]
    // --- Zero width ------------------------------------------------------------------------
    // upstream: regex.match(r'(?r)\bfoo\b', 'a foo').span() == (2, 5)
    [Arguments(@"(?r)\bfoo\b", "a foo", 2, 3)]
    // upstream: regex.match(r'(?r)\Bo', 'foo').span() == (2, 3)
    [Arguments(@"(?r)\Bo", "foo", 2, 1)]
    // upstream: regex.match(r'(?r)\G', 'abc').span() == (3, 3)
    [Arguments(@"(?r)\G", "abc", 3, 0)]
    // upstream: regex.match('(?r)^a', 'a').span() == (0, 1)
    [Arguments("(?r)^a", "a", 0, 1)]
    // upstream: regex.match('(?r)a$', 'a').span() == (0, 1)
    [Arguments("(?r)a$", "a", 0, 1)]
    // upstream: regex.match(r'(?r)\Aab', 'ab').span() == (0, 2)
    [Arguments(@"(?r)\Aab", "ab", 0, 2)]
    // upstream: regex.match(r'(?r)ab\Z', 'ab').span() == (0, 2)
    [Arguments(@"(?r)ab\Z", "ab", 0, 2)]
    public void Reverse_match_span(string pattern, string subject, int index, int length)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        (m.Index, m.Length).Should().Be((index, length));
    }

    /// <summary>
    /// A backward backreference: <c>REF_GROUP_REV</c>, <c>REF_GROUP_IGN_REV</c> and
    /// <c>REF_GROUP_FLD_REV</c>, with the group's span asserted as well as the whole match's so
    /// that the two sides of the comparison are both pinned.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="index">The expected index of the whole match, in UTF-16 code units.</param>
    /// <param name="length">Its expected length, in UTF-16 code units.</param>
    /// <param name="groupIndex">The expected index of group 1.</param>
    /// <param name="groupLength">Its expected length.</param>
    [Test]
    // upstream: regex.match(r'(?r)\1(.)', 'abb').span() == (1, 3), .span(1) == (2, 3)
    [Arguments(@"(?r)\1(.)", "abb", 1, 2, 2, 1)]
    // upstream: regex.match(r'(?ri)\1(\w)', 'xaA').span() == (1, 3), .span(1) == (2, 3)
    [Arguments(@"(?ri)\1(\w)", "xaA", 1, 2, 2, 1)]
    // A surrogate pair on both sides of the comparison.
    // upstream: regex.match(r'(?r)\1(\U0001F600)', '\U0001F600\U0001F600').span() == (0, 2)
    // codepoints, .span(1) == (1, 2) codepoints
    [Arguments(@"(?r)\1(\U0001F600)", "\U0001F600\U0001F600", 0, 4, 2, 2)]
    // The captured text folds to itself, but the *referenced* text does not: 'ss' against 'ß'.
    // upstream: regex.match(r'(?rfi)\1(ss)', 'ßß').span() == (0, 2), .span(1) == (1, 2)
    [Arguments(@"(?rfi)\1(ss)", "ßß", 0, 2, 1, 1)]
    // The captured side is the one that expands: group 1 captures 'ss' and the reference matches
    // the 'ss' before it, both folded from a pattern written 'ß'. This is the cell S22
    // recorded that the oracle wave does not reliably reach.
    // upstream: regex.match(r'(?rfi)\1(ß)', 'ssss').span() == (0, 4), .span(1) == (2, 4)
    [Arguments(@"(?rfi)\1(ß)", "ssss", 0, 4, 2, 2)]
    // One captured character standing for two referenced ones.
    // upstream: regex.match(r'(?rfi)\1(.)', 'ßss').span() == (1, 3), .span(1) == (2, 3)
    [Arguments(@"(?rfi)\1(.)", "ßss", 1, 2, 2, 1)]
    public void Reverse_backreference_span(
        string pattern,
        string subject,
        int index,
        int length,
        int groupIndex,
        int groupLength
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        (m.Index, m.Length).Should().Be((index, length));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((groupIndex, groupLength));
    }

    /// <summary>
    /// A reversed match anchored inside a slice: upstream's <c>pos</c>/<c>endpos</c>, which are our
    /// <c>beginning</c>/<c>length</c>. Under <c>(?r)</c> the match starts at <c>endpos</c> and runs
    /// leftward, so <c>^</c> does not match at <c>pos</c> - the open-start, closed-end rule.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="beginning">The slice start, in UTF-16 code units.</param>
    /// <param name="length">The slice length, in UTF-16 code units.</param>
    /// <param name="expectedIndex">The expected index, or -1 for no match.</param>
    /// <param name="expectedLength">The expected length.</param>
    [Test]
    // upstream: regex.match('(?r).', 'abcd', pos=1, endpos=3).span() == (2, 3)
    [Arguments("(?r).", "abcd", 1, 2, 2, 1)]
    // upstream: regex.match('(?r)a+', 'baaab', pos=1, endpos=4).span() == (1, 4)
    [Arguments("(?r)a+", "baaab", 1, 3, 1, 3)]
    // The slice ends just past the surrogate pair, so the backward step starts at code unit 3 and
    // lands on 1.
    // upstream: regex.match('(?r).', 'a\U0001F600b', pos=0, endpos=2).span() == (1, 2) codepoints
    [Arguments("(?r).", "a\U0001F600b", 0, 3, 1, 2)]
    // '^' is the start of the *string*, which 'pos' does not move.
    // upstream: regex.match('(?r)^a', 'xa', pos=1, endpos=2) is None
    [Arguments("(?r)^a", "xa", 1, 1, -1, -1)]
    public void Reverse_sliced_span(
        string pattern,
        string subject,
        int beginning,
        int length,
        int expectedIndex,
        int expectedLength
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(subject, beginning, length);

        if (expectedIndex < 0)
        {
            m.Success.Should().BeFalse();
            return;
        }

        (m.Index, m.Length).Should().Be((expectedIndex, expectedLength));
    }

    /// <summary>
    /// A reversed pattern that cannot match answers so, rather than running off the front of the
    /// subject.
    /// </summary>
    [Test]
    public void Reverse_match_that_cannot_succeed_fails_cleanly() =>
        // upstream: regex.match('(?r)x', 'abc') is None
        FuzzyRegex.MatchAtStart("abc", "(?r)x").Success.Should().BeFalse();

    // DIVERGES FROM UPSTREAM. PERMANENT: the port is right and upstream has a bug. A change here is
    // a regression - see docs/plan/2026-09-12-divergence-research.md and the S33 closing notes.
    [Test]
    public void A_reverse_fullmatch_of_a_repeat_over_a_narrowed_slice_succeeds_here()
    {
        // Raised by the S32 blind review as an aside - it reproduces with and without `(?p)`, so it
        // is not POSIX's - and settled by S33 by reading upstream's three `match_all` checks.
        //
        //   try_match's RE_OP_SUCCESS arm    upstream/src/_regex.c:7829   text_pos > text_start
        //   basic_match's SUCCESS opcode                          :15167   text_pos != slice_start
        //   the search loop, after a match                        :11880   text_pos == slice_start
        //
        // Two of the three bound a reversed fullmatch by `slice_start`; only the prefilter predicate
        // bounds it by `text_start`, which is always 0. A GENERAL repeat is the one construct that
        // asks `try_match` whether its tail could match, so it is told the match may not end where
        // the slice does, gives up, and the whole fullmatch fails. Measured 2026-09-12,
        // .scratch/up-rev-fullmatch.py, and every prediction of that reading held:
        //
        //   compile(r'(?r)(ab)+').fullmatch('xabz', 1, 3)  -> None      ; this port matches (1, 3)
        //   compile(r'(?r)(ab)+').fullmatch('abz', 0, 2)   -> (0, 2)    ; pos 0, so the bounds agree
        //   compile(r'(?r)(ab)+').match('xabz', 1, 3)      -> (1, 3)    ; no `match_all`, agrees
        //   compile(r'(ab)+').fullmatch('xabz', 1, 3)      -> (1, 3)    ; forward, slice_end IS
        //                                                                 text_end, so agrees
        //   compile(r'(?r)a+').fullmatch('xaaz', 1, 3)     -> (1, 3)    ; a *_ONE repeat, agrees
        //   compile(r'(?r)(a)+').fullmatch('xaaz', 1, 3)   -> None      ; general repeat, one-char
        //                                                                 body, still fails
        //
        // Upstream fullmatching 'ab' against a slice that is exactly 'ab' cannot be None, and its own
        // `match` on the same slice says (1, 3). Reported: docs/plan/upstream-reports/2026-09-12-draft.md.
        Match m = new FuzzyRegex("(?r)(ab)+").FullMatch("xabz", beginning: 1, length: 2);

        m.Success.Should().BeTrue("upstream answers None here and this port matches");
        (m.Index, m.Length).Should().Be((1, 2));

        // The same defect's second symptom, found by the S33 `partial-sliced` wave at seed 31, row
        // 756: the match is not lost, it is flagged as a partial. `try_match` refuses the tail, the
        // lazy repeat's body answers PARTIAL from its own string test at the slice edge, and the
        // repeat returns that partial instead of the complete zero-width match its slow path finds.
        //
        //   compile(r'(?r)([\p{L}\p{N}])??', F|I).fullmatch('AAB', 2, 2)  -> (2, 2) partial True
        //   ... .match('AAB', 2, 2)                                       -> (2, 2) partial False
        //   ... .search('AAB', 2, 2)                                      -> (2, 2) partial False
        //   ... .fullmatch('AAB', 0, 0)                                   -> (0, 0) partial False
        //   compile(r'([\p{L}\p{N}])??', F|I).fullmatch('AAB', 2, 2)      -> (2, 2) partial False
        //
        // Measured 2026-09-12, .scratch/row756.py. A complete match is not a partial one, and
        // upstream's own other three doors agree with this port.
        Match zeroWidth = new FuzzyRegex(
            @"([\p{L}\p{N}])??",
            FuzzyRegexOptions.RightToLeft | FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase
        ).FullMatch("AAB", beginning: 2, length: 0, partial: true);

        zeroWidth.Success.Should().BeTrue();
        (zeroWidth.Index, zeroWidth.Length).Should().Be((2, 0));
        zeroWidth.PartialMatch.Should().BeFalse("upstream calls this complete match a partial one");
    }
}
