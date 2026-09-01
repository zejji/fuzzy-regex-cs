using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Reverse;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_search_reverse</c>
/// (lines 1208-1304).
/// </summary>
/// <remarks>
/// <para>
/// Upstream repeats many of these checks once via <c>regex.findall</c> and again via
/// <c>[m[0] for m in regex.finditer(...)]</c>. Both are <c>Matches</c> here, so each such pair is
/// one test carrying both upstream assertion numbers, following the same convention as
/// <c>Ported/Anchors/SpecialEscapesAnchorTests.cs</c>.
/// </para>
/// <para>
/// The <c>(?V1)</c> and <c>(?rV1)</c> patterns are <em>not</em> folded in that way: the expected
/// value is the same as the unflagged sibling's, but the pattern text differs and the parser has to
/// accept it, which is why upstream asserts both. They get their own tests below.
/// </para>
/// </remarks>
public sealed class SearchReverseTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#1,6,8")]
    public void Matches_value_walks_backward_one_char_at_a_time() =>
        FuzzyRegex.Matches("abc", "(?r).").Select(m => m.Value).Should().Equal("c", "b", "a");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#2")]
    public void Matches_value_walks_backward_one_char_at_a_time_when_overlapped_is_requested() =>
        new FuzzyRegex("(?r).").Matches("abc", overlapped: true).Select(m => m.Value).Should().Equal("c", "b", "a");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#3")]
    public void Matches_value_walks_backward_two_chars_at_a_time_without_overlap() =>
        FuzzyRegex.Matches("abcde", "(?r)..").Select(m => m.Value).Should().Equal("de", "bc");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#4,7,9")]
    public void Matches_value_walks_backward_two_chars_at_a_time_with_overlap() =>
        new FuzzyRegex("(?r)..")
            .Matches("abcde", overlapped: true)
            .Select(m => m.Value)
            .Should()
            .Equal("de", "cd", "bc", "ab");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#5")]
    public void Matches_group_one_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("b", "a");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#5")]
    public void Matches_group_two_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("-", "-");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#5")]
    public void Matches_group_three_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("c", "b");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#10,14")]
    public void Matches_value_for_start_anchor_or_word_run_scans_forward() =>
        FuzzyRegex.Matches("foo bar", @"^|\w+").Select(m => m.Value).Should().Equal("", "foo", "bar");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#12,16")]
    public void Matches_value_for_start_anchor_or_word_run_scans_backward() =>
        FuzzyRegex.Matches("foo bar", @"(?r)^|\w+").Select(m => m.Value).Should().Equal("bar", "foo", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V1) yet; also needs anchors (^)")]
    [Property("Upstream", "RegexTests.test_search_reverse#11,15")]
    public void Matches_value_for_start_anchor_or_word_run_under_the_V1_flag_scans_forward() =>
        FuzzyRegex.Matches("foo bar", @"(?V1)^|\w+").Select(m => m.Value).Should().Equal("", "foo", "bar");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?rV1) yet; also needs right-to-left and anchors (^)")]
    [Property("Upstream", "RegexTests.test_search_reverse#13,17")]
    public void Matches_value_for_start_anchor_or_word_run_under_the_V1_flag_scans_backward() =>
        FuzzyRegex.Matches("foo bar", @"(?rV1)^|\w+").Select(m => m.Value).Should().Equal("bar", "foo", "");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#18")]
    public void Matches_value_for_two_char_runs_anchored_to_the_previous_match_end() =>
        FuzzyRegex.Matches("abcd ef", @"\G\w{2}").Select(m => m.Value).Should().Equal("ab", "cd");

    [Test]
    [Skip("needs:lookaround - the (?<=...) lookbehind has no opcode yet; also needs FuzzyRegex.Matches")]
    [Property("Upstream", "RegexTests.test_search_reverse#19")]
    public void Matches_value_for_two_char_runs_using_a_lookbehind_G_check() =>
        FuzzyRegex.Matches("abcd", @".{2}(?<=\G.*)").Select(m => m.Value).Should().Equal("ab", "cd");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#20")]
    public void Matches_is_empty_when_a_reversed_pattern_requires_a_forward_G_anchor() =>
        FuzzyRegex.Matches("abcd ef", @"(?r)\G\w{2}").Select(m => m.Value).Should().BeEmpty();

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#21")]
    public void Matches_value_when_a_reversed_pattern_places_the_G_anchor_after_the_run() =>
        FuzzyRegex.Matches("abcd ef", @"(?r)\w{2}\G").Select(m => m.Value).Should().Equal("ef");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#22")]
    public void Matches_value_for_a_star_quantified_literal_scans_forward() =>
        FuzzyRegex.Matches("qqwe", "q*").Select(m => m.Value).Should().Equal("qq", "", "", "");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#24")]
    public void Matches_value_for_a_star_quantified_literal_scans_backward() =>
        FuzzyRegex.Matches("qqwe", "(?r)q*").Select(m => m.Value).Should().Equal("", "", "qq", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V1) yet")]
    [Property("Upstream", "RegexTests.test_search_reverse#23")]
    public void Matches_value_for_a_star_quantified_literal_under_the_V1_flag_scans_forward() =>
        FuzzyRegex.Matches("qqwe", "(?V1)q*").Select(m => m.Value).Should().Equal("qq", "", "", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?rV1) yet; also needs right-to-left")]
    [Property("Upstream", "RegexTests.test_search_reverse#25")]
    public void Matches_value_for_a_star_quantified_literal_under_the_V1_flag_scans_backward() =>
        FuzzyRegex.Matches("qqwe", "(?rV1)q*").Select(m => m.Value).Should().Equal("", "", "qq", "");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#26,28")]
    public void Matches_value_is_restricted_to_the_beginning_and_length_window() =>
        new FuzzyRegex(".").Matches("abcd", beginning: 1, length: 2).Select(m => m.Value).Should().Equal("b", "c");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#30,32")]
    public void Matches_value_for_a_reversed_pattern_is_restricted_to_the_beginning_and_length_window() =>
        new FuzzyRegex("(?r).").Matches("abcd", beginning: 1, length: 2).Select(m => m.Value).Should().Equal("c", "b");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#34")]
    public void Matches_value_for_a_case_insensitive_character_class() =>
        FuzzyRegex.Matches("aB", "[ab]", FuzzyRegexOptions.IgnoreCase).Select(m => m.Value).Should().Equal("a", "B");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#35")]
    public void Matches_value_for_a_reversed_case_insensitive_character_class() =>
        FuzzyRegex
            .Matches("aB", "(?r)[ab]", FuzzyRegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Should()
            .Equal("B", "a");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#36,40")]
    public void Matches_value_for_a_reversed_two_char_repeat_without_overlap() =>
        FuzzyRegex.Matches("abc", "(?r).{2}").Select(m => m.Value).Should().Equal("bc");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#37,41")]
    public void Matches_value_for_a_reversed_two_char_repeat_with_overlap() =>
        new FuzzyRegex("(?r).{2}").Matches("abc", overlapped: true).Select(m => m.Value).Should().Equal("bc", "ab");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#38")]
    public void Matches_group_one_value_for_two_space_separated_word_groups() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(\w+) (\w+)")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("first", "third");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#38")]
    public void Matches_group_two_value_for_two_space_separated_word_groups() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(\w+) (\w+)")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("second", "fourth");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#39")]
    public void Matches_group_one_value_for_two_space_separated_word_groups_scanned_backward() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(?r)(\w+) (\w+)")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("fourth", "second");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#39")]
    public void Matches_group_two_value_for_two_space_separated_word_groups_scanned_backward() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(?r)(\w+) (\w+)")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("fifth", "third");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#42")]
    public void Matches_value_for_two_space_separated_word_groups_is_the_whole_match() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(\w+) (\w+)")
            .Select(m => m.Value)
            .Should()
            .Equal("first second", "third fourth");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#43")]
    public void Matches_value_for_two_space_separated_word_groups_scanned_backward_is_the_whole_match() =>
        FuzzyRegex
            .Matches("first second third fourth fifth", @"(?r)(\w+) (\w+)")
            .Select(m => m.Value)
            .Should()
            .Equal("fourth fifth", "second third");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#44")]
    public void Match_span_for_a_plain_literal()
    {
        Match m = FuzzyRegex.Match("abcdef", "abcdef");

        (m.Index, m.Index + m.Length).Should().Be((0, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#45")]
    public void Match_span_for_a_reversed_plain_literal()
    {
        Match m = FuzzyRegex.Match("abcdef", "(?r)abcdef");

        (m.Index, m.Index + m.Length).Should().Be((0, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#46")]
    public void Match_span_for_a_case_insensitive_literal()
    {
        Match m = FuzzyRegex.Match("ABCDEF", "(?i)abcdef");

        (m.Index, m.Index + m.Length).Should().Be((0, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#47")]
    public void Match_span_for_a_reversed_case_insensitive_literal()
    {
        Match m = FuzzyRegex.Match("ABCDEF", "(?ir)abcdef");

        (m.Index, m.Index + m.Length).Should().Be((0, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#48")]
    public void Replace_with_a_single_group_backreference_reproduces_the_subject() =>
        FuzzyRegex.Replace("abc", "(.)", @"\1").Should().Be("abc");

    [Test]
    [Property("Upstream", "RegexTests.test_search_reverse#49")]
    public void Replace_with_a_reversed_single_group_backreference_reproduces_the_subject() =>
        FuzzyRegex.Replace("abc", "(?r)(.)", @"\1").Should().Be("abc");

    // NOT PORTED: endpos=-1 forms of assertions #27, #29, #31, #33 (lines 1258-1259, 1262-1263,
    // 1267-1268, 1271-1272). A negative endpos is Python's index-from-the-end convention; here
    // len(subject) - 1 == 3, so endpos=-1 is the same call as endpos=3 once translated to our
    // beginning/length API, already covered by #26/#28 and #30/#32 above.
}
