using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Boundaries;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_word_boundary</c>
/// (lines 1544-1566).
/// </summary>
public sealed class WordBoundaryTests
{
    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#1")]
    public void Word_boundary_split_separates_punctuation_apostrophes_and_the_decimal_point() =>
        FuzzyRegex
            .Split("The quick (\"brown\") fox can't jump 32.3 feet, right?", @"(?V1)\b")
            .Should()
            .Equal(
                "",
                "The",
                " ",
                "quick",
                " (\"",
                "brown",
                "\") ",
                "fox",
                " ",
                "can",
                "'",
                "t",
                " ",
                "jump",
                " ",
                "32",
                ".",
                "3",
                " ",
                "feet",
                ", ",
                "right",
                "?"
            );

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#2")]
    public void Word_boundary_split_with_word_flag_treats_apostrophes_and_the_decimal_point_as_word_chars() =>
        FuzzyRegex
            .Split("The quick (\"brown\") fox can't jump 32.3 feet, right?", @"(?V1w)\b")
            .Should()
            .Equal(
                "",
                "The",
                " ",
                "quick",
                " ",
                "(",
                "\"",
                "brown",
                "\"",
                ")",
                " ",
                "fox",
                " ",
                "can't",
                " ",
                "jump",
                " ",
                "32.3",
                " ",
                "feet",
                ",",
                " ",
                "right",
                "?",
                ""
            );

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#3")]
    public void Word_boundary_split_keeps_a_double_space_between_two_words_whole() =>
        FuzzyRegex.Split("The  fox", @"(?V1)\b").Should().Equal("", "The", "  ", "fox", "");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#4")]
    public void Word_boundary_split_with_word_flag_also_keeps_a_double_space_whole() =>
        FuzzyRegex.Split("The  fox", @"(?V1w)\b").Should().Equal("", "The", "  ", "fox", "");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#5")]
    public void Word_boundary_split_treats_each_apostrophe_as_a_boundary() =>
        FuzzyRegex
            .Split("can't aujourd'hui l'objectif", @"(?V1)\b")
            .Should()
            .Equal("", "can", "'", "t", " ", "aujourd", "'", "hui", " ", "l", "'", "objectif", "");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_boundary#6")]
    public void Word_boundary_split_with_word_flag_keeps_each_contraction_whole() =>
        FuzzyRegex
            .Split("can't aujourd'hui l'objectif", @"(?V1w)\b")
            .Should()
            .Equal("", "can't", " ", "aujourd'hui", " ", "l'objectif");
}
