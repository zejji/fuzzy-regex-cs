using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The scoped ASCII and Unicode flags at match time: a <c>PROPERTY</c> node compiled inside
/// <c>(?a:...)</c> or <c>(?u:...)</c> carries its own encoding in its status word and ignores the
/// pattern's, which is upstream's <c>ENCODING_KIND</c> (<c>upstream/src/_regex.c</c> line 167).
/// </summary>
/// <remarks>
/// <para>
/// Upstream's own suite pins <c>(?a:\d)</c> alone (<c>test_hg_bugs</c> #476); every other
/// combination it has reaches the answer through <c>findall</c>, which is S25. Without these the
/// engine could read the encoding bits for the one case upstream happens to test and ignore them
/// everywhere else, and no test would say so.
/// </para>
/// <para>
/// Every expected value is upstream's, recorded against regex 2026.7.19 on 2026-08-31 with
/// <c>regex.match(pattern, subject)</c> and quoted beside the assertion.
/// </para>
/// </remarks>
public sealed class ScopedEncodingTests
{
    /// <summary>U+FF19 FULLWIDTH DIGIT NINE: <c>Nd</c>, and above <c>RE_ASCII_MAX</c>.</summary>
    private const string _fullwidthNine = "\uFF19";

    /// <summary>U+00E9 LATIN SMALL LETTER E WITH ACUTE: a letter, and above <c>RE_ASCII_MAX</c>.</summary>
    private const string _eAcute = "\u00E9";

    /// <summary>U+212A KELVIN SIGN: <c>Lu</c>, and well above it.</summary>
    private const string _kelvinSign = "\u212A";

    [Test]
    // upstream: regex.match(r'(?a:\d)', '\uff19') is None
    [Arguments(@"(?a:\d)", _fullwidthNine, false)]
    // upstream: regex.match(r'(?u:\d)', '\uff19').span() == (0, 1)
    [Arguments(@"(?u:\d)", _fullwidthNine, true)]
    // A scoped flag wins over the pattern's, in both directions.
    // upstream: regex.match(r'(?a)(?u:\d)', '\uff19').span() == (0, 1)
    [Arguments(@"(?a)(?u:\d)", _fullwidthNine, true)]
    // upstream: regex.match(r'(?u)(?a:\d)', '\uff19') is None
    [Arguments(@"(?u)(?a:\d)", _fullwidthNine, false)]
    // upstream: regex.match(r'(?a)\d', '\uff19') is None
    [Arguments(@"(?a)\d", _fullwidthNine, false)]
    // The same node inside a set, so matches_member's own ENCODING_KIND switch is the one under
    // test rather than matches_PROPERTY's.
    // upstream: regex.match(r'(?a:[\d])', '\uff19') is None
    [Arguments(@"(?a:[\d])", _fullwidthNine, false)]
    // upstream: regex.match(r'(?a:[^\d])', '\uff19').span() == (0, 1)
    [Arguments(@"(?a:[^\d])", _fullwidthNine, true)]
    // upstream: regex.match(r'(?a:\p{L})', '\u00e9') is None
    [Arguments(@"(?a:\p{L})", _eAcute, false)]
    // upstream: regex.match(r'(?u:\p{L})', '\u00e9').span() == (0, 1)
    [Arguments(@"(?u:\p{L})", _eAcute, true)]
    // upstream: regex.match(r'(?a)[[:alpha:]]', '\u00e9') is None
    [Arguments("(?a)[[:alpha:]]", _eAcute, false)]
    // upstream: regex.match(r'(?a)\p{L}', '\u212a') is None
    [Arguments(@"(?a)\p{L}", _kelvinSign, false)]
    public void The_encoding_a_property_node_was_compiled_under_decides_its_answer(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Success.Should().Be(expected);

    // DIVERGES FROM UPSTREAM, deliberately, and these rows pin OUR answers rather than upstream's.
    /// <summary>
    /// A scope that names no encoding keeps the one around it, and a POSIX class takes the scope's
    /// encoding as a <c>\p{...}</c> does.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>parse_subpattern</c> (<c>_regex_core.py:1172</c>) resets the encoding whenever
    /// ANY encoding flag is in force, so an inner <c>(?s:</c> or <c>(?i:</c> wiped an outer
    /// <c>(?a:</c>; and <c>parse_posix_class</c> passes no encoding at all. Upstream answers every
    /// row below with a match. CPython's re, which documents scoped <c>(?a:...)</c>, refuses the
    /// first two (it has no POSIX classes); the rest follow from <c>(?a:\p{L})</c>, which upstream
    /// itself refuses. Measured 2026-09-25 on regex 2026.9.10.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">A non-ASCII letter.</param>
    [Test]
    [Arguments(@"(?a:(?s:\w))", "\u00E9")]
    [Arguments(@"(?a:(?i:\w))", "\u00E9")]
    [Arguments(@"(?a:(?i:\p{Lu}))", "\u00C9")]
    [Arguments(@"(?a:(?m:\p{L}))", "\u0138")]
    [Arguments("(?a:[[:alpha:]])", "\u00E9")]
    [Arguments("(?a:[[:upper:]])", "\u00C9")]
    [Arguments("(?i)(?a:[[:upper:]])", "\u00E9")]
    [Arguments(@"(?i)(?a:[\p{Lu}x])", "\u00E9")]
    public void An_inner_scope_or_a_posix_class_keeps_the_ascii_scope_around_it(string pattern, string subject) =>
        FuzzyRegex.FullMatch(subject, pattern).Success.Should().BeFalse(pattern);

    /// <summary>The controls: the same scopes still match ASCII letters, and <c>(?u:...)</c> lifts ASCII.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    [Test]
    [Arguments(@"(?a:(?s:\w))", "e")]
    [Arguments(@"(?a:(?i:\p{Lu}))", "e")]
    [Arguments("(?a:[[:alpha:]])", "e")]
    [Arguments("(?a)(?u:[[:alpha:]])", "\u00E9")]
    [Arguments(@"(?a:(?u:\w))", "\u00E9")]
    public void The_scoped_encoding_controls_still_match(string pattern, string subject) =>
        FuzzyRegex.FullMatch(subject, pattern).Success.Should().BeTrue(pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// An encoding named by positional flags inside a group replaces the one in force, as the
    /// scoped spelling of the same thing does.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>parse_positional_flags</c> ORs the new flags in, so <c>(?a:(?u)\w)</c> held
    /// ASCII and UNICODE both, ASCII won, and it refused 'é' where <c>(?a:(?u:\w))</c> matches
    /// (regex 2026.9.10, 2026-09-25). CPython's re refuses the positional spelling outright
    /// ("global flags not at the start of the expression"), so it cannot corroborate either way;
    /// the scoped spelling is the one both document. Upstream also answered by whether the group
    /// captures: <c>(?a)(?:(?u)\w)</c> matched 'é' and <c>(?a)((?u)\w)</c> refused it.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches.</param>
    [Test]
    [Arguments(@"(?a:(?u)\w)", "\u00E9", true)]
    [Arguments(@"(?u:(?a)\w)", "\u00E9", false)]
    [Arguments(@"(?a:x(?u)\w)", "x\u00E9", true)]
    [Arguments(@"(?a)((?u)\w)", "\u00E9", true)]
    [Arguments(@"(?a)(?:(?u)\w)", "\u00E9", true)]
    [Arguments(@"(?a:(?u)\w)\w", "\u00E9\u00E9", true)]
    [Arguments(@"((?a)\w)", "\u00E9", false)]
    public void Positional_encoding_flags_replace_the_encoding_in_force(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.FullMatch(subject, pattern).Success.Should().Be(expected, pattern);

    /// <summary>
    /// A clash between positional encoding flags after a conditional is rejected, as it is after
    /// any other group.
    /// </summary>
    /// <remarks>
    /// A conditional restores the flags when it closes, so what follows it is back at the top
    /// level, where a second encoding clashes with the first. Upstream raises for all five
    /// spellings (regex 2026.9.10, V1, 2026-09-25). The positional-flags fix first counted a
    /// lookaround conditional as still open after it closed, so this port accepted the three
    /// lookaround spellings (the blind review of the fix, 2026-09-25).
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    [Test]
    [Arguments(@"(?u)(?:x|y)(?a)z")]
    [Arguments(@"(?u)(x)(?(1)x|y)(?a)z")]
    [Arguments(@"(?u)(?(?=x)x|y)(?a)z")]
    [Arguments(@"(?u)(?(?!q)x|y)(?a)z")]
    [Arguments(@"(?u)(?(?<=q)x|y)(?a)z")]
    public void A_clash_of_encodings_after_a_conditional_is_rejected(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .WithMessage("ASCII, LOCALE and UNICODE flags are mutually incompatible");
    }
}
