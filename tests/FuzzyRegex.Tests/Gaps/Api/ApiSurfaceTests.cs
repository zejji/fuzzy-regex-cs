using System.Reflection;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// Pins the public API surface: what it promises, what it validates, and which parts of it are
/// still waiting on the engine.
/// </summary>
/// <remarks>
/// <para>
/// S01 wrote this file as <c>ApiSurfaceStubTests</c>, where every assertion was "this member
/// throws <see cref="NotImplementedException"/>", with the note that phase 2 would flip them into
/// real ones rather than delete them. S07 is that phase: the constructor compiles, so the
/// pattern-level members assert on what it produced. The matching members are still stubs -
/// the engine is phase 3 - and are still pinned as such.
/// </para>
/// <para>
/// These are gap tests, not ported ones: they pin our own scaffolding, so they do not count
/// towards the parity percentage.
/// </para>
/// </remarks>
public sealed class ApiSurfaceTests
{
    [Test]
    public void Constructing_a_pattern_compiles_it()
    {
        FuzzyRegex pattern = new("a");

        pattern.Pattern.Should().Be("a");
        pattern.ToString().Should().Be("a");
        pattern.GroupNumbers.Should().Equal(0);
    }

    [Test]
    public void A_null_pattern_is_rejected_before_anything_is_compiled()
    {
        // Validation at a trust boundary is tested like the real code it is: the
        // ArgumentNullException must win over anything the compiler would say.
        Action construct = () => _ = new FuzzyRegex(null!);

        construct.Should().Throw<ArgumentNullException>().WithParameterName("pattern");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-5)]
    public void A_non_positive_match_timeout_is_rejected(int milliseconds)
    {
        // Not -1: TimeSpan.FromMilliseconds(-1) *is* Timeout.InfiniteTimeSpan, so it is the
        // "no limit" sentinel rather than a negative timeout. The next test pins that.

        Action construct = () =>
            _ = new FuzzyRegex("a", FuzzyRegexOptions.None, TimeSpan.FromMilliseconds(milliseconds));

        construct.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("matchTimeout");
    }

    [Test]
    public void An_infinite_match_timeout_is_accepted_and_reported_back()
    {
        FuzzyRegex pattern = new("a", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout);

        pattern.MatchTimeout.Should().Be(FuzzyRegex.InfiniteMatchTimeout);
    }

    [Test]
    public void A_positive_match_timeout_is_reported_back()
    {
        FuzzyRegex pattern = new("a", FuzzyRegexOptions.None, TimeSpan.FromSeconds(2));

        pattern.MatchTimeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Test]
    public void Minus_one_millisecond_is_the_infinite_sentinel_not_a_negative_timeout()
    {
        // Matches Regex.InfiniteMatchTimeout, which is likewise Timeout.InfiniteTimeSpan.
        FuzzyRegex.InfiniteMatchTimeout.Should().Be(TimeSpan.FromMilliseconds(-1));
    }

    [Test]
    public void The_matching_members_are_still_stubs()
    {
        // The engine is phase 3. When this starts failing, the slice that made it fail should
        // replace it with real assertions, exactly as S07 did to its predecessor.
        Action isMatch = () => _ = FuzzyRegex.IsMatch("abc", "a");
        Action match = () => _ = new FuzzyRegex("a").Match("abc");

        isMatch.Should().Throw<NotImplementedException>();
        match.Should().Throw<NotImplementedException>();
    }

    [Test]
    public void Options_hides_the_upstream_flags_this_port_does_not_expose()
    {
        // upstream/regex/_main.py lines 570-574 OR UNICODE (0x20) into every str pattern's flags,
        // and the version bit is always added, so the raw resolved flags for "a" are 0x2020 - a
        // number with no name in FuzzyRegexOptions. What a caller sees is the version alone.
        new FuzzyRegex("a")
            .Options.Should()
            .Be(FuzzyRegexOptions.Version0);
    }

    [Test]
    public void Every_instance_field_of_FuzzyRegex_is_initonly()
    {
        // The type promises to be safe to share between threads (see its own doc comment, and
        // DECISIONS 2026-08-29). Immutability is how that promise is kept, and this is the gate:
        // S07 is the slice that gave FuzzyRegex instance fields at all. Auto-properties count -
        // a `{ get; }` property compiles to an initonly backing field.
        FieldInfo[] mutable =
        [
            .. typeof(FuzzyRegex)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsInitOnly),
        ];

        mutable.Should().BeEmpty("FuzzyRegex is documented as safe to share between threads");
    }

    [Test]
    public void Option_values_are_upstream_flag_values_so_the_parser_port_can_use_them_directly()
    {
        // upstream/regex/_regex_core.py lines 73-90 (class RegexFlag).
        ((int)FuzzyRegexOptions.IgnoreCase)
            .Should()
            .Be(0x2);
        ((int)FuzzyRegexOptions.Multiline).Should().Be(0x8);
        ((int)FuzzyRegexOptions.Singleline).Should().Be(0x10);
        ((int)FuzzyRegexOptions.IgnorePatternWhitespace).Should().Be(0x40);
        ((int)FuzzyRegexOptions.Version1).Should().Be(0x100);
        ((int)FuzzyRegexOptions.RightToLeft).Should().Be(0x400);
        ((int)FuzzyRegexOptions.BestMatch).Should().Be(0x1000);
        ((int)FuzzyRegexOptions.Version0).Should().Be(0x2000);
        ((int)FuzzyRegexOptions.FullCase).Should().Be(0x4000);
        ((int)FuzzyRegexOptions.EnhanceMatch).Should().Be(0x8000);
        ((int)FuzzyRegexOptions.Posix).Should().Be(0x10000);
    }

    [Test]
    public void Every_option_is_an_upstream_flag_bit()
    {
        // S07 removed ExplicitCapture, the one option with no upstream counterpart (DECISIONS
        // 2026-08-30, decision C). This is what stops another one being added by accident: every
        // option must be a bit upstream's RegexFlag defines, because the compile-parity corpus
        // can only verify constructs upstream has.
        const int allUpstreamFlags = 0x1FFFF;

        FuzzyRegexOptions[] options = Enum.GetValues<FuzzyRegexOptions>();

        options.Should().OnlyHaveUniqueItems();
        options.Should().AllSatisfy(option => ((int)option & ~allUpstreamFlags).Should().Be(0));
    }

    [Test]
    public void The_surface_can_express_every_upstream_operation_the_ported_suite_calls()
    {
        // Not a behaviour test - a compilation test. S01 exists to be the compilation target for
        // S02-S05, and each of these upstream operations has no other member to translate onto.
        // If a signature changes and one of these stops binding, this fails to build, which is
        // exactly the moment the ported suite would have stopped building too.
        Type surface = typeof(FuzzyRegex);

        surface
            .GetMethod(nameof(FuzzyRegex.FullMatch), [typeof(string), typeof(int), typeof(int), typeof(bool)])
            .Should()
            .NotBeNull("upstream fullmatch is used 71 times in test_regex.py");
        surface
            .GetMethod(nameof(FuzzyRegex.MatchAtStart), [typeof(string), typeof(int), typeof(int), typeof(bool)])
            .Should()
            .NotBeNull("upstream match is anchored at pos and is not our Match");
        surface
            .GetMethod(nameof(FuzzyRegex.ReplaceFormat), [typeof(string), typeof(string), typeof(int)])
            .Should()
            .NotBeNull("upstream subf uses str.format templates, not $1 templates");

        typeof(Match)
            .GetProperty(nameof(Match.LastGroupNumber))
            .Should()
            .NotBeNull("upstream lastindex is not derivable from Groups");
        typeof(Match)
            .GetProperty(nameof(Match.LastGroupName))
            .Should()
            .NotBeNull("upstream lastgroup is not derivable from Groups");
        typeof(Match)
            .GetMethod(nameof(Match.ResultFormat), [typeof(string)])
            .Should()
            .NotBeNull("upstream expandf takes str.format templates");
    }

    [Test]
    public void Escape_carries_both_of_upstreams_flags_because_all_four_combinations_differ()
    {
        // Oracle, 2026-08-29: escape('foo!?') is foo!\? but escape('foo!?', special_only=False)
        // is foo\!\?; escape('a b') is a\ b but escape('a b', literal_spaces=True) is 'a b'.
        // A single-argument Escape could not produce all four.
        MethodInfo? escape = typeof(FuzzyRegex).GetMethod(
            nameof(FuzzyRegex.Escape),
            [typeof(string), typeof(bool), typeof(bool)]
        );

        escape.Should().NotBeNull();
        escape!.GetParameters().Select(p => p.Name).Should().Equal("input", "specialOnly", "literalSpaces");
    }

    [Test]
    public void A_parse_exception_carries_the_pattern_and_the_offset()
    {
        var error = new FuzzyRegexParseException("bad set", "[a-", 2);

        error.Pattern.Should().Be("[a-");
        error.Offset.Should().Be(2);
    }

    [Test]
    public void A_parse_exception_without_a_position_reports_no_offset()
    {
        var error = new FuzzyRegexParseException("bad set");

        error.Pattern.Should().BeNull();
        error.Offset.Should().Be(-1);
    }
}
