using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// The pattern-level properties S07 turned on, in the cases upstream's own suite does not reach.
/// </summary>
/// <remarks>
/// Upstream expresses group numbering through <c>Pattern.groupindex</c>, a dictionary of the
/// <i>named</i> groups only, so nothing upstream pins what
/// <see cref="FuzzyRegex.GroupNames"/> should say about an unnamed group, what
/// <see cref="FuzzyRegex.GroupNumberFromName"/> should return for a name that is not there, or
/// how a group nested inside itself is numbered. Those are this port's decisions, so they are
/// pinned here as gap tests.
/// </remarks>
public sealed class PatternPropertyTests
{
    [Test]
    public void A_pattern_with_no_groups_still_has_group_zero()
    {
        FuzzyRegex pattern = new("abc");

        pattern.GroupNumbers.Should().Equal(0);
        pattern.GroupNames.Should().Equal("0");
        pattern.GroupNameFromNumber(0).Should().Be("0");
    }

    [Test]
    public void Named_and_unnamed_groups_are_listed_by_ascending_number()
    {
        FuzzyRegex pattern = new("(a)(?<second>b)(c)");

        pattern.GroupNumbers.Should().Equal(0, 1, 2, 3);
        pattern.GroupNames.Should().Equal("0", "1", "second", "3");
        pattern.GroupNameFromNumber(2).Should().Be("second");
        pattern.GroupNumberFromName("second").Should().Be(2);
    }

    [Test]
    public void Both_spellings_of_a_named_group_produce_the_same_numbering()
    {
        // Upstream accepts Python's (?P<name>...) and Perl's (?<name>...) and treats them
        // identically (upstream/regex/_regex_core.py lines 868-882 and 946-961).
        new FuzzyRegex("(?P<a>x)")
            .GroupNumberFromName("a")
            .Should()
            .Be(1);
        new FuzzyRegex("(?<a>x)").GroupNumberFromName("a").Should().Be(1);
    }

    [Test]
    public void A_non_capturing_group_takes_no_number()
    {
        FuzzyRegex pattern = new("(?:a)(b)");

        pattern.GroupNumbers.Should().Equal(0, 1);
        pattern.GroupNumberFromName("1").Should().Be(-1, "only named groups are in the index");
    }

    [Test]
    public void An_unknown_group_name_is_minus_one_rather_than_a_throw()
    {
        // Upstream raises KeyError from pattern.groupindex[name]; a .NET caller asking "is there
        // such a group" should not have to catch. The built-in Regex.GroupNumberFromName has the
        // same contract.
        new FuzzyRegex("(a)")
            .GroupNumberFromName("nope")
            .Should()
            .Be(-1);
    }

    [Test]
    public void An_out_of_range_group_number_is_rejected()
    {
        Action byNumber = static () => new FuzzyRegex("(a)").GroupNameFromNumber(2);

        byNumber.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("number");
    }

    [Test]
    public void A_named_group_nested_inside_itself_keeps_one_public_number()
    {
        // upstream/regex/_regex_core.py lines 4391-4397: the inner occurrence gets a negative
        // private alias, and only the outer, positive number is public. Compiling this at all is
        // what proves the aliasing works; group_count stays 1.
        FuzzyRegex pattern = new("(?<x>a(?<x>b))");

        pattern.GroupNumbers.Should().Equal(0, 1);
        pattern.GroupNumberFromName("x").Should().Be(1);
    }

    [Test]
    public void An_inline_flag_shows_up_in_Options()
    {
        // Upstream's Pattern.flags is info.flags | version, so a flag the pattern set for itself
        // is reported as if the caller had passed it - alongside the default version and the
        // FullCase that version implies.
        new FuzzyRegex("(?s)a")
            .Options.Should()
            .Be(FuzzyRegexOptions.Singleline | FuzzyRegexOptions.Version1 | FuzzyRegexOptions.FullCase);
    }

    [Test]
    public void The_default_version_is_Version1_where_upstreams_front_end_sets_Version0()
    {
        // DIVERGES FROM UPSTREAM (S50b, spec amendment 24; docs/DIVERGENCES.md). Upstream's
        // _main.py line 443 overrides _regex_core.py line 161's VERSION1 with VERSION0 so that
        // regex stays a drop-in for re; this port keeps _regex_core.py's value instead.
        //
        // Version 1 also turns FullCase on, because DEFAULT_FLAGS maps VERSION1 to FULLCASE
        // (upstream/regex/_regex_core.py line 167). Verified against the local oracle 2026-08-30:
        // regex.compile('a', regex.V1).flags is 0x4120 - FULLCASE, VERSION1 and the UNICODE that
        // Options masks off - against 0x2020 with no flags at all.
        new FuzzyRegex("a")
            .Options.Should()
            .Be(FuzzyRegexOptions.Version1 | FuzzyRegexOptions.FullCase);

        // The caller who wants upstream's reading says so, and gets upstream's flags exactly.
        new FuzzyRegex("a", FuzzyRegexOptions.Version0)
            .Options.Should()
            .Be(FuzzyRegexOptions.Version0);

        // And so does a pattern that asks for it inline.
        new FuzzyRegex("(?V0)a")
            .Options.Should()
            .Be(FuzzyRegexOptions.Version0);
    }

    [Test]
    public void A_pattern_with_no_named_lists_reports_none()
    {
        new FuzzyRegex("a").NamedLists.Should().BeEmpty();
    }
}
