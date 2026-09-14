using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// What this port's <c>Version1</c> default (S50b, spec amendment 24) does to a pattern that names
/// no version, and - the part that needed a fix - to one that names <c>Version0</c> inline.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this class pins is upstream's, and this port fixes it</b> (ledger entry 22). Under
/// <c>DEFAULT_VERSION = VERSION1</c>, upstream disagrees with itself about what <c>VERSION0</c>
/// means: the flag gives simple folding and the inline spelling gives full folding. Measured on
/// regex 2026.9.10 / CPython 3.14.6, 2026-09-14, by
/// <c>python tools/probes/upstream-inline-v0-under-a-v1-default.py</c>:
/// </para>
/// <code>
/// DEFAULT_VERSION = V1
///   compile('a', regex.V0)            flags = U|V0
///   compile('(?V0)a')                 flags = F|U|V0
///   compile('ss', regex.V0|regex.I) vs SHARP S: False
///   compile('(?V0)(?i)ss')          vs SHARP S: True
/// </code>
/// <para>
/// <b>The mechanism.</b> A leading inline flag that is global - a version is one
/// (<c>upstream/regex/_regex_core.py</c> <c>GLOBAL_FLAGS</c>) - raises <c>_UnscopedFlagSet</c> and
/// the whole pattern is parsed a second time seeded with <c>info.global_flags</c>
/// (<c>:1201-1206</c>, <c>_main.py:537-554</c>). But the FIRST attempt has already run
/// <c>Info.__init__</c>'s <c>flags |= DEFAULT_FLAGS[(flags &amp; _ALL_VERSIONS) or
/// DEFAULT_VERSION]</c> (<c>:4359</c>) against the default version, because <c>(?V0)</c> has not
/// been read yet - and it assigns that result to <c>global_flags</c> too (<c>:4361</c>). So version
/// 1's implied <c>FULLCASE</c> is carried into the attempt that knows the pattern asked for
/// version 0, where <c>DEFAULT_FLAGS[VERSION0]</c> is <c>0</c> and has nothing to take it back off
/// with.
/// </para>
/// <para>
/// <b>It cannot fire upstream</b>, where <c>DEFAULT_VERSION</c> is <c>VERSION0</c> and
/// <c>DEFAULT_FLAGS[VERSION0]</c> is <c>0</c>, so the first attempt contributes nothing to carry.
/// Upstream does support changing the global - changelog "Hg issue 69: Changing DEFAULT_VERSION
/// does not actually work ... should now work as expected" - so it is a live bug in a supported
/// configuration, and it is the configuration this port ships. The fix is in <c>Info</c>: the
/// version-implied defaults go into <c>Flags</c> and not into <c>GlobalFlags</c>, so a retry is
/// seeded with what the caller and the pattern actually asked for. Under <c>VERSION0</c> that is
/// bit-for-bit what upstream does, which is why all 1,659 compile-parity rows are unmoved.
/// </para>
/// </remarks>
public sealed class DefaultVersionTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    [Test]
    public void A_pattern_that_names_no_version_gets_Version1_and_the_FullCase_it_implies() =>
        new FuzzyRegex("a").Options.Should().Be(FuzzyRegexOptions.Version1 | FuzzyRegexOptions.FullCase);

    /// <summary>
    /// The three ways to ask for upstream's version 0 all mean the same thing, which is the
    /// property upstream loses. See this class's remarks.
    /// </summary>
    /// <param name="pattern">The pattern, spelling version 0 inline or not at all.</param>
    /// <param name="options">The options, naming version 0 or not at all.</param>
    [Test]
    [Arguments("a", FuzzyRegexOptions.Version0)]
    [Arguments("(?V0)a", FuzzyRegexOptions.None)]
    [Arguments("(?V0)a", FuzzyRegexOptions.Version0)]
    public void Version_0_means_simple_folding_however_it_is_asked_for(string pattern, FuzzyRegexOptions options) =>
        new FuzzyRegex(pattern, options).Options.Should().Be(FuzzyRegexOptions.Version0);

    /// <summary>
    /// The same thing where a user would notice it: <c>ß</c> against <c>ss</c> is the
    /// README's own example of what full case-folding adds.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="expected">Whether it matches <c>ß</c>.</param>
    [Test]
    [Arguments("(?i)ss", true)]
    [Arguments("(?V1)(?i)ss", true)]
    [Arguments("(?V0)(?i)ss", false)]
    [Arguments("(?i)(?-f)ss", false)]
    public void Full_folding_is_on_by_default_and_off_under_version_0(string pattern, bool expected) =>
        new FuzzyRegex(pattern).FullMatch("ß").Success.Should().Be(expected);

    /// <summary>
    /// What the new default actually buys, at the four places upstream's README and S45 point at.
    /// Every row is <c>(?i)</c> with no other flag, which is the whole point: a caller gets full
    /// folding without having to know <c>FullCase</c> exists.
    /// </summary>
    /// <param name="pattern">The pattern, case-insensitive and nothing else.</param>
    /// <param name="subject">The subject to full-match.</param>
    /// <param name="expected">Whether it matches under the default.</param>
    [Test]
    // The README's own examples of what full folding adds. Simple folding answers false to both.
    [Arguments("(?i)ss", "ß", true)]
    [Arguments("(?i)ß", "SS", true)]
    [Arguments("(?i)fi", "ﬁ", true)]
    [Arguments("(?i)ﬁ", "FI", true)]
    [Arguments("(?i)ffi", "ﬃ", true)]
    // The Kelvin sign folds simply too, so the default does not change it.
    [Arguments("(?i)k", "K", true)]
    [Arguments("(?i)K", "K", true)]
    // The S45 Turkic four, where this port deliberately answers with PCRE2, Perl and .NET rather
    // than with upstream (Unicode.TurkicDefaults, docs/DIVERGENCES.md). Full folding is what the
    // new default turns on, and it must not turn these back on with it.
    [Arguments("(?i)I", "ı", false)]
    [Arguments("(?i)ı", "I", false)]
    [Arguments("(?i)i", "İ", false)]
    [Arguments("(?i)İ", "i", false)]
    // The dotted capital's own full expansion, which S45's grid keeps: U+0130 folds to "i" + U+0307.
    [Arguments("(?i)İ", "i̇", true)]
    public void Full_case_folding_is_what_the_default_turns_on(string pattern, string subject, bool expected) =>
        new FuzzyRegex(pattern).FullMatch(subject).Success.Should().Be(expected);

    /// <summary>
    /// And the same rows under <c>Version0</c>, which is how a caller asks for the simple folding
    /// <c>re</c> and <c>System.Text.RegularExpressions</c> do. Only the expanding pairs move.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject to full-match.</param>
    /// <param name="expected">Whether it matches under version 0.</param>
    [Test]
    [Arguments("(?i)ss", "ß", false)]
    [Arguments("(?i)ß", "SS", false)]
    [Arguments("(?i)fi", "ﬁ", false)]
    [Arguments("(?i)ffi", "ﬃ", false)]
    [Arguments("(?i)k", "K", true)]
    [Arguments("(?i)I", "ı", false)]
    [Arguments("(?i)i", "İ", false)]
    [Arguments("(?i)İ", "i̇", false)]
    public void Version_0_keeps_the_simple_folding_re_and_dotnet_do(string pattern, string subject, bool expected) =>
        new FuzzyRegex(pattern, FuzzyRegexOptions.Version0).FullMatch(subject).Success.Should().Be(expected);

    /// <summary>
    /// A version named late in the pattern takes the same route - <c>_UnscopedFlagSet</c> is raised
    /// wherever the flag is - so it must come out the same way.
    /// </summary>
    [Test]
    public void A_version_0_flag_after_the_first_item_resolves_the_same_way() =>
        new FuzzyRegex("a(?V0)").Options.Should().Be(FuzzyRegexOptions.Version0);

    /// <summary>
    /// The retry must not lose a global flag the pattern set alongside the version, which is the
    /// thing <c>GlobalFlags</c> is for.
    /// </summary>
    [Test]
    public void A_version_0_flag_beside_another_global_flag_keeps_both() =>
        new FuzzyRegex("(?V0)(?r)a").Options.Should().Be(FuzzyRegexOptions.Version0 | FuzzyRegexOptions.RightToLeft);

    /// <summary>
    /// The loud edge of the new default: an unescaped <c>[</c> inside a set is a literal under
    /// version 0, <c>re</c> and <c>System.Text.RegularExpressions</c>, and opens a nested set under
    /// version 1. Upstream says only "unterminated character set", which tells a caller arriving
    /// from <c>Regex</c> nothing about why their working pattern stopped compiling, so this port
    /// names both ways out.
    /// </summary>
    /// <param name="pattern">A pattern that is legal under version 0 and not under version 1.</param>
    /// <param name="escaped">The same pattern with the inner <c>[</c> escaped.</param>
    /// <param name="subject">A subject both readings match.</param>
    [Test]
    [Arguments("[[]", @"[\[]", "[")]
    [Arguments("[a[b]", @"[a\[b]", "[")]
    [Arguments("a[b[c]d", @"a[b\[c]d", "a[d")]
    public void An_unescaped_bracket_inside_a_set_says_how_to_get_the_version_0_reading(
        string pattern,
        string escaped,
        string subject
    )
    {
        Action compile = () => _ = new FuzzyRegex(pattern);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Message.Should()
            .Contain("unterminated character set")
            .And.Contain("Version0")
            .And.Contain(@"\[");

        // Both ways out really are ways out, so the message is advice that works.
        new FuzzyRegex(pattern, FuzzyRegexOptions.Version0)
            .IsMatch(subject)
            .Should()
            .BeTrue();
        new FuzzyRegex(escaped).IsMatch(subject).Should().BeTrue();
    }

    /// <summary>
    /// A set that is simply unclosed keeps upstream's bare message: nothing about the version
    /// caused it, and pointing at <c>Version0</c> would send the reader the wrong way.
    /// </summary>
    /// <remarks>
    /// <b>Rows three onward are what two blind reviews found, and between them they are why the
    /// condition is the whole pattern compiling under version 0 rather than a flag saying "a nested
    /// set was opened".</b> Each one DOES open a nested set somewhere and is still broken under
    /// version 0, so a flag - whether scoped to the pattern or to one top-level set - offered
    /// <c>Version0</c> as a remedy that does not work. <c>[[a-z]--[aeiou]]x[</c> is the first
    /// review's, where a legal version-1 set operation closes and a later <c>[</c> is the whole
    /// problem; <c>[[a]--[b</c> is the second's, where the unclosed nested set is inside the set
    /// that fails. Measured against regex 2026.9.10 on 2026-09-15: every row raises "unterminated
    /// character set" under <c>(?V0)</c> and <c>(?V1)</c> alike.
    /// </remarks>
    /// <param name="pattern">A pattern whose set is unterminated under either version.</param>
    [Test]
    [Arguments("a[")]
    [Arguments("a[b")]
    [Arguments("[[a-z]--[aeiou]]x[")]
    [Arguments("[[a]]b[")]
    [Arguments("[[a]]b[c")]
    [Arguments("a[b[c]d[")]
    [Arguments("[[a]--[b")]
    [Arguments("[[a]b[c")]
    [Arguments("[[a][")]
    public void A_set_that_is_merely_unclosed_keeps_upstreams_message(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern);
        Action underVersion0 = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.Version0);

        compile.Should().Throw<FuzzyRegexParseException>().WithMessage("unterminated character set");

        // And the reason the bare message is right: Version0 is not a way out of this one.
        underVersion0.Should().Throw<FuzzyRegexParseException>();
    }

    /// <summary>
    /// And the compiler-level statement of the same fix, where the divergence would first show:
    /// the flags a retry is seeded with.
    /// </summary>
    [Test]
    public void The_compiled_flags_carry_no_full_case_the_pattern_did_not_ask_for()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?V0)a", 0, _noNamedLists, PatternCompiler.DefaultVersion);

        (compiled.Flags & RegexFlags.FullCase).Should().Be(0);
        (compiled.Flags & RegexFlags.AllVersions).Should().Be(RegexFlags.Version0);
    }
}
