using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Splitting;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_931848</c> (lines 863-866).
/// </summary>
/// <remarks>
/// Upstream's pattern is a Python string literal <c>".。．｡"</c>, which Python
/// decodes into four literal characters (full stop, ideographic full stop, fullwidth full stop,
/// halfwidth ideographic full stop) before the regex engine ever sees it. The character class is
/// built here from the same four code points by numeric cast, rather than as an escaped string
/// literal, so the intended code points are unambiguous in source.
/// </remarks>
public sealed class Bug931848Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_931848#1")]
    public void Split_on_a_character_class_of_four_full_stop_variants()
    {
        string pattern = "[" + (char)0x002E + (char)0x3002 + (char)0xFF0E + (char)0xFF61 + "]";

        new FuzzyRegex(pattern).Split("a.b.c").Should().Equal("a", "b", "c");
    }
}
