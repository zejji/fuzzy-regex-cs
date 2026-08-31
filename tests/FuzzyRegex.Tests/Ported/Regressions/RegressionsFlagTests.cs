using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about inline and global case-folding and ASCII-vs-Unicode flags.
/// </summary>
public sealed class RegressionsFlagTests
{
    // U+0419 CYRILLIC CAPITAL LETTER SHORT I.
    private const string _cyrillicShortIUpper = "\u0419";

    // U+0439 CYRILLIC SMALL LETTER SHORT I.
    private const string _cyrillicShortILower = "\u0439";

    // U+0436 CYRILLIC SMALL LETTER ZHE.
    private const string _cyrillicZhe = "\u0436";

    // U+FF19 FULLWIDTH DIGIT NINE.
    private const string _fullwidthDigitNine = "\uFF19";

    // Hg issue 204: confusion of (?aif) flags.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#240-245")]
    [Arguments("(?ui)", _cyrillicShortIUpper, _cyrillicShortILower, true)]
    [Arguments("(?ui)", _cyrillicShortILower, _cyrillicShortIUpper, true)]
    [Arguments("(?ai)", _cyrillicShortIUpper, _cyrillicShortILower, false)]
    [Arguments("(?ai)", _cyrillicShortILower, _cyrillicShortIUpper, false)]
    [Arguments("(?afi)", _cyrillicShortIUpper, _cyrillicShortILower, false)]
    [Arguments("(?afi)", _cyrillicShortILower, _cyrillicShortIUpper, false)]
    public void Scoped_case_fold_flag_controls_whether_cyrillic_short_i_variants_cross_match(
        string flagPrefix,
        string patternChar,
        string subject,
        bool expectedMatch
    ) => FuzzyRegex.MatchAtStart(subject, flagPrefix + patternChar).Success.Should().Be(expectedMatch);

    // Git issue 467: Scoped inline flags 'a', 'u' and 'L' affect global flags.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#431")]
    public void Ascii_scoped_word_char_does_not_widen_the_unscoped_word_char_that_follows_it()
    {
        Match m = FuzzyRegex.MatchAtStart("d" + _cyrillicZhe, @"(?a:\w)\w");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#432")]
    public void Ascii_scoped_word_char_followed_by_unicode_scoped_word_char_matches_both()
    {
        Match m = FuzzyRegex.MatchAtStart("d" + _cyrillicZhe, @"(?a:\w)(?u:\w)");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    // Git issue 572: Inline ASCII modifier doesn't seem to affect anything.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#475")]
    public void Unscoped_digit_class_matches_a_fullwidth_digit() =>
        FuzzyRegex.MatchAtStart(_fullwidthDigitNine, @"\d").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#476")]
    public void Ascii_scoped_digit_class_rejects_a_fullwidth_digit() =>
        FuzzyRegex.MatchAtStart(_fullwidthDigitNine, @"(?a:\d)").Success.Should().BeFalse();

    // Git issue 575: Issues with ASCII/Unicode modifiers.
    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#477")]
    public void Unflagged_digit_class_matches_both_an_ascii_and_a_fullwidth_digit() =>
        FuzzyRegex
            .Matches("9" + _fullwidthDigitNine, @"\d")
            .Select(m => m.Value)
            .Should()
            .Equal("9", _fullwidthDigitNine);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#478")]
    public void Unicode_scoped_digit_class_matches_both_an_ascii_and_a_fullwidth_digit() =>
        FuzzyRegex
            .Matches("9" + _fullwidthDigitNine, @"(?u:\d)")
            .Select(m => m.Value)
            .Should()
            .Equal("9", _fullwidthDigitNine);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#479")]
    public void Ascii_scoped_digit_class_matches_only_the_ascii_digit() =>
        FuzzyRegex.Matches("9" + _fullwidthDigitNine, @"(?a:\d)").Select(m => m.Value).Should().Equal("9");

    // `FuzzyRegexOptions` has no `A`/`ASCII` or `U`/`UNICODE` member, so a Python `flags=regex.A`
    // or `flags=regex.U` argument is ported as a leading inline `(?a)`/`(?u)` prefix on the
    // pattern text instead, with the C# options left at `None`. Verified against the local oracle
    // on 2026-08-30: `regex.findall(r'\d', '9\uFF19', flags=regex.U)` and
    // `regex.findall(r'(?u)\d', '9\uFF19')` both give `['9', '\uff19']`.
    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#480")]
    public void Global_unicode_flag_prefix_leaves_the_digit_class_matching_both_digits() =>
        FuzzyRegex
            .Matches("9" + _fullwidthDigitNine, @"(?u)\d")
            .Select(m => m.Value)
            .Should()
            .Equal("9", _fullwidthDigitNine);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#481")]
    public void Global_unicode_flag_prefix_does_not_change_an_already_unicode_scoped_digit_class() =>
        FuzzyRegex
            .Matches("9" + _fullwidthDigitNine, @"(?u)(?u:\d)")
            .Select(m => m.Value)
            .Should()
            .Equal("9", _fullwidthDigitNine);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#482")]
    public void Global_unicode_flag_prefix_does_not_widen_an_ascii_scoped_digit_class() =>
        FuzzyRegex.Matches("9" + _fullwidthDigitNine, @"(?u)(?a:\d)").Select(m => m.Value).Should().Equal("9");

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#483")]
    public void Global_ascii_flag_prefix_narrows_the_digit_class_to_the_ascii_digit() =>
        FuzzyRegex.Matches("9" + _fullwidthDigitNine, @"(?a)\d").Select(m => m.Value).Should().Equal("9");

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#484")]
    public void Global_ascii_flag_prefix_does_not_narrow_a_unicode_scoped_digit_class() =>
        FuzzyRegex
            .Matches("9" + _fullwidthDigitNine, @"(?a)(?u:\d)")
            .Select(m => m.Value)
            .Should()
            .Equal("9", _fullwidthDigitNine);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#485")]
    public void Global_ascii_flag_prefix_does_not_change_an_already_ascii_scoped_digit_class() =>
        FuzzyRegex.Matches("9" + _fullwidthDigitNine, @"(?a)(?a:\d)").Select(m => m.Value).Should().Equal("9");

    // Git issue 575, continued: \p{L} counts over every Latin-1 code point (0x00-0xFF) under the
    // cross product of an unscoped/(?a:...)/(?u:...) property and no/(?a)/(?u) global prefix.
    private static readonly string _latin1CodePoints = new([.. Enumerable.Range(0, 0x100).Select(c => (char)c)]);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#486-494")]
    [Arguments(@"\p{L}", 117)]
    [Arguments(@"(?a)\p{L}", 52)]
    [Arguments(@"(?u)\p{L}", 117)]
    [Arguments(@"(?a:\p{L})", 52)]
    [Arguments(@"(?a)(?a:\p{L})", 52)]
    [Arguments(@"(?u)(?a:\p{L})", 52)]
    [Arguments(@"(?u:\p{L})", 117)]
    [Arguments(@"(?a)(?u:\p{L})", 117)]
    [Arguments(@"(?u)(?u:\p{L})", 117)]
    public void Letter_property_count_over_latin_1_depends_on_the_effective_ascii_or_unicode_scope(
        string pattern,
        int expectedCount
    ) => FuzzyRegex.Matches(_latin1CodePoints, pattern).Count.Should().Be(expectedCount);

    // Hg issue 39: regex.search("((?i)blah)\s+\1", "blah BLAH") doesn't return None. Changed to
    // positional flags in regex 2023.12.23.
    [Test]
    [Skip("needs:inline-flags - positional (mid-pattern) inline flags are not scoped correctly yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#17")]
    public void Positional_inline_case_fold_flag_does_not_apply_to_a_later_backreference() =>
        FuzzyRegex.Match("blah BLAH", @"((?i)blah)\s+\1").Success.Should().BeFalse();

    // Hg issue 46: regex.compile("a(?x: b c )d") causes "_regex_core.error: missing )".
    [Test]
    [Skip("needs:inline-flags - scoped (?x: ...) verbose groups fail to parse")]
    [Property("Upstream", "RegexTests.test_hg_bugs#26")]
    public void Scoped_verbose_flag_group_ignores_whitespace_inside_the_group_only() =>
        FuzzyRegex.Match("abcd", "a(?x: b c )d").Value.Should().Be("abcd");
}
