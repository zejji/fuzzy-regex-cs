using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// What <c>(?L)</c> does to a <c>str</c> pattern, which S09 left for this slice to settle against
/// the oracle.
/// </summary>
/// <remarks>
/// <para>
/// The answer is: <b>nothing, until casing is consulted</b>. <c>LOCALE</c> is an encoding flag, so
/// <c>_main._compile</c> stops OR-ing <c>UNICODE</c> in (<c>upstream/regex/_main.py:570-574</c>)
/// and the resolved flags come out as <c>LOCALE|VERSION0</c>; the property escapes get encoding
/// tag 0 rather than the Unicode one; and every construct that does not ask a character's case
/// compiles exactly as it would without the flag. Only <c>get_all_cases</c> and the folding
/// functions branch on it, and those read the process's C locale, which has no .NET counterpart -
/// so this port throws its <c>needs:locale-flag</c> seam there and nowhere else.
/// </para>
/// <para>
/// No corpus row covers <c>(?L)</c> with a <c>str</c> pattern: upstream's suite only uses it on
/// <c>bytes</c>. Every expected value below was measured against the local oracle
/// (<c>regex</c> 2026.7.19) on 2026-08-30 by intercepting <c>regex._regex.compile</c>:
/// </para>
/// <code>
/// '(?L)a'           resolved=L|V0  code=[12, 1, 97, 1]
/// '(?L)\w'          resolved=L|V0  code=[37, 1, 6291457, 1]
/// '(?L)\W'          resolved=L|V0  code=[37, 0, 6291457, 1]
/// '(?L)[[:alpha:]]' resolved=L|V0  code=[37, 1, 1, 1]
/// '(?L)\b'          resolved=L|V0  code=[9, 1, 1]
/// '(?L).'           resolved=L|V0  code=[2, 0, 1]
/// '(?L)\d'          resolved=L|V0  code=[37, 1, 1966089, 1]
/// '(?L)[a-z]'       resolved=L|V0  code=[42, 1, 97, 122, 1]
/// '(?L)\p{L}'       resolved=L|V0  code=[37, 1, 1966111, 1]
/// '(?Li)a'          resolved=I|L|V0 code=[13, 1, 97, 1]
/// '(?Li)ab'         resolved=I|L|V0 code=[77, 16, 2, 97, 98, 1]
/// '(?Li)--'         resolved=I|L|V0 code=[74, 16, 2, 45, 45, 1]
/// </code>
/// <para>
/// The seam is narrower than it looks. <c>is_cased_i</c> is reached only from
/// <c>Sequence._flush_characters</c>, and a one-item sequence is never a <c>Sequence</c>
/// (<c>make_sequence</c>, <c>:1935</c>), so <c>(?Li)a</c> compiles here as it does upstream and
/// only a run of two or more characters stops at the seam. <c>Character.folded</c> is not
/// locale-sensitive either: it folds with the constant <c>FULL_CASE_FOLDING</c>, whose
/// <c>UNICODE</c> bit wins over the pattern's flags.
/// </para>
/// </remarks>
public sealed class LocaleFlagTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    private const int _localeVersion0 = RegexFlags.Locale | RegexFlags.Version0;

    [Test]
    [Arguments(@"(?L)a", new uint[] { 12, 1, 97, 1 })]
    [Arguments(@"(?L)\w", new uint[] { 37, 1, 6291457, 1 })]
    [Arguments(@"(?L)\W", new uint[] { 37, 0, 6291457, 1 })]
    [Arguments(@"(?L)[[:alpha:]]", new uint[] { 37, 1, 1, 1 })]
    [Arguments(@"(?L)\b", new uint[] { 9, 1, 1 })]
    [Arguments(@"(?L).", new uint[] { 2, 0, 1 })]
    [Arguments(@"(?L)\d", new uint[] { 37, 1, 1966089, 1 })]
    [Arguments(@"(?L)[a-z]", new uint[] { 42, 1, 97, 122, 1 })]
    [Arguments(@"(?L)\p{L}", new uint[] { 37, 1, 1966111, 1 })]
    public void A_locale_pattern_that_never_asks_about_case_compiles_as_upstream_does(
        string pattern,
        uint[] expectedCode
    )
    {
        CompiledPattern compiled = PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal(expectedCode);
            compiled.Flags.Should().Be(_localeVersion0);
        }
    }

    /// <summary>A single case-insensitive character never reaches <c>is_cased_i</c> either.</summary>
    [Test]
    [Arguments(@"(?Li)a", new uint[] { 13, 1, 97, 1 })]
    [Arguments(@"(?Li)K", new uint[] { 13, 1, 75, 1 })]
    [Arguments(@"(?Li)ß", new uint[] { 13, 1, 223, 1 })]
    public void A_single_case_insensitive_locale_character_compiles_as_upstream_does(
        string pattern,
        uint[] expectedCode
    )
    {
        CompiledPattern compiled = PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal(expectedCode);
            compiled.Flags.Should().Be(_localeVersion0 | RegexFlags.IgnoreCase);
        }
    }

    /// <summary>
    /// A run of two or more case-insensitive characters reaches <c>is_cased_i</c>, whose
    /// <c>get_all_cases</c> uses the C locale's <c>tolower</c>/<c>toupper</c> over bytes 0-255
    /// (<c>upstream/src/_regex.c:1057-1360</c>). Upstream compiles <c>'(?Li)ab'</c> to
    /// <c>[77, 16, 2, 97, 98, 1]</c> and <c>'(?Li)--'</c> - two uncased characters, so the case
    /// flags are dropped - to <c>[74, 16, 2, 45, 45, 1]</c>. This port stops at the seam instead,
    /// because there is nothing in .NET to read that locale from.
    /// </summary>
    [Test]
    [Arguments(@"(?Li)ab")]
    [Arguments(@"(?Li)--")]
    [Arguments(@"(?Lfi)ab")]
    public void A_case_insensitive_locale_run_stops_at_the_locale_seam(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        compile.Should().Throw<NotImplementedException>().WithMessage("needs:locale-flag*");
    }
}
