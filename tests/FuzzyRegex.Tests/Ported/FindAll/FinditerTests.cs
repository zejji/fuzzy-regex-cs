using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.FindAll;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_finditer</c>
/// (lines 855-857), the <c>finditer</c> half of <c>test_bug_581080</c> (lines 868-875), and
/// <c>test_bug_817234</c> (lines 877-881).
/// </summary>
/// <remarks>
/// Upstream's <c>finditer</c> returns a lazy iterator; a Python
/// <c>assertRaises(StopIteration, ...)</c> after the last item becomes an assertion on
/// <see cref="MatchCollection.Count"/> here, since <see cref="FuzzyRegex.Matches(string, string, FuzzyRegexOptions)"/> is eager.
/// </remarks>
public sealed class FinditerTests
{
    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_finditer#1")]
    public void Matches_yields_whole_match_values_in_order() =>
        FuzzyRegex.Matches("a:b::c:::d", ":+").Select(m => m.Value).Should().Equal(":", "::", ":::");

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_bug_581080#1")]
    public void Matches_first_match_span()
    {
        MatchCollection matches = FuzzyRegex.Matches("a b", @"\s");

        (matches[0].Index, matches[0].Index + matches[0].Length).Should().Be((1, 2));
    }

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_bug_581080#2")]
    public void Matches_has_exactly_one_match() => FuzzyRegex.Matches("a b", @"\s").Count.Should().Be(1);

    // NOT PORTED: the pat.scanner("a b") half of test_bug_581080 (lines 873-875) - the Scanner
    // API is deliberately not ported (see docs/PORTMAP.md).

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_bug_817234#1")]
    public void Matches_first_match_of_dot_star_spans_the_whole_subject()
    {
        MatchCollection matches = FuzzyRegex.Matches("asdf", ".*");

        (matches[0].Index, matches[0].Index + matches[0].Length).Should().Be((0, 4));
    }

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_bug_817234#2")]
    public void Matches_second_match_of_dot_star_is_the_trailing_empty_match()
    {
        MatchCollection matches = FuzzyRegex.Matches("asdf", ".*");

        (matches[1].Index, matches[1].Index + matches[1].Length).Should().Be((4, 4));
    }

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_bug_817234#3")]
    public void Matches_has_exactly_two_matches_for_dot_star() => FuzzyRegex.Matches("asdf", ".*").Count.Should().Be(2);
}
