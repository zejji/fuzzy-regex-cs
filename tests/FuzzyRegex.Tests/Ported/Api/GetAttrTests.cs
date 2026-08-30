using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Api;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_getattr</c> (lines 461-489).
/// </summary>
/// <remarks>
/// Several assertions here have no counterpart on our types and are recorded as <c>NOT PORTED</c>
/// comments in source order below: <c>Match.pos</c>, <c>Match.endpos</c> (twice each),
/// <c>Match.string</c>, <c>Match.regs</c> and <c>Match.re</c>. The bytes-pattern
/// <c>regex.compile(b"...")</c> assertion is also not ported - our port is <c>char</c>-based. The
/// final assertion (<c>p.groupindex["n"] = 0</c>, a dict mutation) is not ported either: our
/// <see cref="FuzzyRegex.GroupNames"/> is read-only.
/// </remarks>
public sealed class GetAttrTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_getattr#1")]
    public void Pattern_returns_the_compiled_text() => new FuzzyRegex("(?i)(a)(b)").Pattern.Should().Be("(?i)(a)(b)");

    [Test]
    [Property("Upstream", "RegexTests.test_getattr#2")]
    public void Options_reports_the_inline_flag_and_the_default_version()
    {
        // Upstream also ORs in regex.U; incidental for a str pattern, which is Unicode by
        // default. regex.DEFAULT_VERSION resolves to VERSION0 (regex/_main.py:443), which is our
        // Version0 - verified against the local oracle 2026-08-29: hex(regex.DEFAULT_VERSION) is
        // 0x2000, the same bit as FuzzyRegexOptions.Version0.
        new FuzzyRegex("(?i)(a)(b)")
            .Options.Should()
            .Be(FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version0);
    }

    // NOT PORTED: regex.compile(b"(?i)(a)(b)").flags - a bytes pattern; our port is char-based.

    [Test]
    [Property("Upstream", "RegexTests.test_getattr#4")]
    public void GroupNumbers_counts_the_capturing_groups() =>
        (new FuzzyRegex("(?i)(a)(b)").GroupNumbers.Count - 1).Should().Be(2);

    [Test]
    [Property("Upstream", "RegexTests.test_getattr#5")]
    public void GroupNames_are_the_group_numbers_when_none_are_named()
    {
        // Upstream's groupindex (name -> number, named groups only) is {} for this pattern; ours
        // lists every group by its number as text when none are named.
        new FuzzyRegex("(?i)(a)(b)")
            .GroupNames.Should()
            .Equal("0", "1", "2");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_getattr#6")]
    public void GroupNumberFromName_resolves_named_groups()
    {
        FuzzyRegex pat = new("(?i)(?P<first>a)(?P<other>b)");

        pat.GroupNumberFromName("first").Should().Be(1);
        pat.GroupNumberFromName("other").Should().Be(2);
    }

    // NOT PORTED: Match.pos has no counterpart (regex.match("(a)", "a").pos == 0).
    // NOT PORTED: Match.endpos has no counterpart (regex.match("(a)", "a").endpos == 1).
    // NOT PORTED: Match.pos has no counterpart (regex.search("b(c)", "abcdef").pos == 0).
    // NOT PORTED: Match.endpos has no counterpart (regex.search("b(c)", "abcdef").endpos == 6).

    [Test]
    [Skip("needs:groups - the engine has no capture support yet")]
    [Property("Upstream", "RegexTests.test_getattr#11-12")]
    public void Match_and_group_spans()
    {
        Match m = FuzzyRegex.Match("abcdef", "b(c)");

        (m.Index, m.Index + m.Length).Should().Be((1, 3));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((2, 3));
    }

    // NOT PORTED: Match.string has no counterpart (regex.match("(a)", "a").string == 'a').
    // NOT PORTED: Match.regs has no counterpart
    // (regex.match("(a)", "a").regs == ((0, 1), (0, 1))).
    // NOT PORTED: Match.re has no counterpart (repr(type(regex.match("(a)", "a").re))).
    // NOT PORTED: the Issue 14260 mutation (p.groupindex["n"] = 0) - our GroupNames is read-only.
}
