using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bigcharset</c> (lines 524-537).
/// </summary>
public sealed class BigCharsetTests
{
    // Upstream's second assertion repeats the first with an explicit regex.UNICODE flag, which is
    // incidental here (str patterns are Unicode by default and the flag is not surfaced on
    // FuzzyRegexOptions); folded into one test.
    [Test]
    [Property("Upstream", "RegexTests.test_bigcharset#1-2")]
    public void Set_of_two_high_codepoints_captures_the_matching_one() =>
        FuzzyRegex.MatchAtStart("∢", "([∢∣])").Groups[1].Value.Should().Be("∢");

    [Test]
    [Property("Upstream", "RegexTests.test_bigcharset#3")]
    public void Findall_dot_reproduces_a_string_of_high_codepoints()
    {
        const string subject = "eèéêëēěė";

        string joined = string.Concat(FuzzyRegex.Matches(subject, ".").Select(static m => m.Value));

        joined.Should().Be(subject);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bigcharset#4")]
    public void Findall_big_set_reproduces_a_string_of_high_codepoints()
    {
        const string subject = "eèéêëēěė";

        string joined = string.Concat(FuzzyRegex.Matches(subject, "[eèéêëēěė]").Select(static m => m.Value));

        joined.Should().Be(subject);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bigcharset#5")]
    public void Findall_alternation_of_high_codepoints_reproduces_the_string()
    {
        const string subject = "eèéêëēěė";

        string joined = string.Concat(FuzzyRegex.Matches(subject, "e|è|é|ê|ë|ē|ě|ė").Select(static m => m.Value));

        joined.Should().Be(subject);
    }
}
