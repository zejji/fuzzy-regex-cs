using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

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

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// A case-insensitive node folds with the encoding of the scope it was parsed in, whatever it
    /// is: a character, a string, a range, a set, a backreference, a named list, and anything built
    /// of them (S91).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is CPython's and Perl's: <c>(?i)(?a:k)</c> refuses U+212A KELVIN SIGN exactly as
    /// <c>(?ai)k</c> does, and <c>(?ai)(?u:k)</c> accepts it exactly as <c>(?i)k</c> does. Each row
    /// quotes what was measured on 2026-09-25 with <c>re.search</c> (CPython 3.14.7), Perl 5.42.3
    /// (<c>/$p/</c> under <c>use utf8</c> and <c>use feature 'unicode_strings'</c>, with
    /// <c>(?aa:...)</c> for the ASCII case rules) and <c>regex.search</c> (regex 2026.9.10). Where
    /// neither CPython nor Perl has the construct, the row quotes upstream's answer to the same
    /// encoding set globally, which it gets right.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">The span found, or "none".</param>
    [Test]
    // re: None; Perl (?i)(?aa:k): None; upstream: (0, 1).
    [Arguments("(?i)(?a:k)", "\u212A", "none")]
    // re: (0, 1); Perl (?aai)(?u:k): (0, 1); upstream: None.
    [Arguments("(?ai)(?u:k)", "\u212A", "(0,1)")]
    // A string. re: None; Perl: None; upstream: (0, 2).
    [Arguments("(?i)(?a:kx)", "\u212Ax", "none")]
    // Two characters packed into one literal. re: None; Perl: None; upstream: (0, 2).
    [Arguments("(?i)k(?a:k)", "\u212A\u212A", "none")]
    // re: (0, 2); Perl: (0, 2); upstream: (0, 2).
    [Arguments("(?i)k(?a:k)", "\u212Ak", "(0,2)")]
    // A range. re: None; Perl: None; upstream: (0, 1).
    [Arguments("(?i)(?a:[a-z])", "\u212A", "none")]
    // re: (0, 1); Perl: (0, 1); upstream: None.
    [Arguments("(?ai)(?u:[a-z])", "\u212A", "(0,1)")]
    // A set, and a negated one. re: None, (0, 1); Perl: None, (0, 1); upstream: (0, 1), None.
    [Arguments("(?i)(?a:[k])", "\u212A", "none")]
    [Arguments("(?i)(?a:[^k])", "\u212A", "(0,1)")]
    // A set operation; neither re nor Perl's classic classes have one.
    // upstream (?aiV1)[[k]--[x]]: None; (?iV1)(?a:[[k]--[x]]): (0, 1).
    [Arguments("(?iV1)(?a:[[k]--[x]])", "\u212A", "none")]
    // A backreference. re: None, (0, 2); Perl: None, (0, 2); upstream: (0, 2), None.
    [Arguments(@"(?i)(?a:(k)\1)", "k\u212A", "none")]
    [Arguments(@"(?ai)(?u:(k)\1)", "k\u212A", "(0,2)")]
    // A repeat, an alternation, an atomic group and a lookahead. re: None for all four; Perl:
    // None for all four; upstream: (0, 2), (0, 1), (0, 1), (0, 1).
    [Arguments("(?i)(?a:k+)", "\u212A\u212A", "none")]
    [Arguments("(?i)(?a:(?:k|x))", "\u212A", "none")]
    [Arguments("(?i)(?a:(?>k+))", "\u212A", "none")]
    [Arguments(@"(?i)(?a:(?=k))\w", "\u212A", "none")]
    // A named list holding "k"; re has none. upstream (?ai)\L<w>: None; (?i)(?a:\L<w>): (0, 1).
    [Arguments(@"(?i)(?a:\L<w>)", "\u212A", "none")]
    // Fuzzy matching, which re and Perl lack. upstream (?ai)(?:kz){s<=1}: None, and
    // (?b)(?ai)(?:kx){e<=1}: None; the scoped spellings: (0, 2) and (0, 2).
    [Arguments("(?i)(?a:(?:kz){s<=1})", "\u212A\u212A", "none")]
    [Arguments("(?i)(?a:(?b)(?:kx){e<=1})", "\u212A\u212A", "none")]
    // The constraint's own test set folds with the scope's encoding too: re and upstream's
    // (?ai)(?:xz){s<=1:[k]} give None; upstream's scoped spelling (0, 2).
    [Arguments("(?i)(?a:(?:xz){s<=1:[k]})", "\u212Az", "none")]
    // Reversed matching; re has none. upstream (?r)(?ai)k: None; (?r)(?i)(?a:k): (0, 1).
    [Arguments("(?r)(?i)(?a:k)", "\u212A", "none")]
    // Letters ASCII thinks caseless keep their case flags under a scoped Unicode, alone and packed
    // into a literal. re: (0, 1) and (0, 2); Perl (?aai)(?u:\x{e9}\x{e9}): (0, 2); upstream: None
    // and None.
    [Arguments("(?ai)(?u:\u00E9)", "\u00C9", "(0,1)")]
    [Arguments("(?ai)(?u:\u00E9\u00E9)", "\u00C9\u00C9", "(0,2)")]
    // Full case folding under a scoped Unicode inside an ASCII pattern, which re lacks.
    // Upstream answers (0, 2) for (?V1)(?uif)\xdf, (?V1)(?uif)[\xde-\xdfx] and
    // (?V1)(?uif)[\xde-\xdf] on 'ss', and (0, 2), (0, 2) and None for their scoped spellings. It
    // answers (0, 1) for (?V1)(?uif)(?:st|sx) on U+FB06, and None for its scoped spelling. In the
    // set the letter comes from a range, so only the set's own expansion can match 'ss'.
    [Arguments("(?V1)(?aif)(?u:\u00DF)", "ss", "(0,2)")]
    [Arguments("(?V1)(?aif)(?u:[\u00DE-\u00DFx])", "ss", "(0,2)")]
    [Arguments("(?V1)(?aif)(?u:[\u00DE-\u00DF])", "ss", "(0,2)")]
    [Arguments("(?V1)(?aif)(?u:(?:st|sx))", "\uFB06", "(0,1)")]
    // And none under a scoped ASCII. upstream (?V1)(?aif)\xdf on 'ss': None, as its scoped
    // spelling (?V1)(?if)(?a:\xdf) is.
    [Arguments("(?V1)(?if)(?a:\u00DF)", "ss", "none")]
    public void A_case_insensitive_node_folds_with_its_scopes_encoding(
        string pattern,
        string subject,
        string expected
    ) => Search(pattern, subject).Should().Be(expected, pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// A partial match folds a string that runs off the end of the subject with the string's own
    /// encoding (S91).
    /// </summary>
    /// <remarks>
    /// <c>regex.match(pattern, '\u212ax', partial=True)</c> gives a partial (0, 2) for
    /// <c>(?iu)kxy</c>, <c>(?iu)x?kxy</c> and <c>(?iu)(?:q|kxy)</c>, and nothing for their scoped
    /// spellings under <c>(?ai)</c> (regex 2026.9.10, 2026-09-25). The second and third reach the
    /// string as the test a repeat and a branch look ahead with, rather than as the next node.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="partial">Whether the subject is a partial match.</param>
    [Test]
    [Arguments("(?ai)(?u:kxy)", true)]
    [Arguments("(?ai)x?(?u:kxy)", true)]
    [Arguments("(?ai)(?:q|(?u:kxy))", true)]
    [Arguments("(?i)(?a:kxy)", false)]
    [Arguments("(?i)x?(?a:kxy)", false)]
    public void A_partial_string_folds_with_its_scopes_encoding(string pattern, bool partial)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("\u212Ax", partial: true);

        m.Success.Should().Be(partial, pattern);
        m.Length.Should().Be(partial ? 2 : 0, pattern);
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// Word starts and ends, the <c>WORD</c> flag's boundaries and line separators, and grapheme
    /// boundaries follow the scope's encoding (S91).
    /// </summary>
    /// <remarks>
    /// CPython and Perl have none of these constructs as regex spells them, so each row quotes
    /// upstream's answer to the same encoding set globally (regex 2026.9.10, 2026-09-25), which the
    /// scoped spelling now gives, and upstream's answer to the scoped spelling.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">The span found, or "none".</param>
    [Test]
    // upstream (?a)\mx: (1, 2); (?a:\mx): None.
    [Arguments(@"(?a:\mx)", "\u00E9x", "(1,2)")]
    // upstream (?a)x\M: (0, 1); (?a:x\M): None.
    [Arguments(@"(?a:x\M)", "x\u00E9", "(0,1)")]
    // upstream (?aw)\bx: (1, 2); (?w)(?a:\bx): None.
    [Arguments(@"(?w)(?a:\bx)", "\u00E9x", "(1,2)")]
    // upstream (?aw).: (0, 1); (?w)(?a:.): None.
    [Arguments("(?w)(?a:.)", "\u2028", "(0,1)")]
    // upstream (?aw)x$: None; (?w)(?a:x$): (0, 1).
    [Arguments("(?w)(?a:x$)", "x\u2028", "none")]
    // upstream (?aw)(?m)x$ on 'x\u2028y': None; (?w)(?a:(?m)x$): (0, 1).
    [Arguments("(?w)(?a:(?m)x$)", "x\u2028y", "none")]
    // upstream (?aw)(?m)^x: None; (?w)(?a:(?m)^x): (1, 2).
    [Arguments("(?w)(?a:(?m)^x)", "\u2028x", "none")]
    // upstream (?a)\X: (0, 1); (?a:\X): (0, 2).
    [Arguments(@"(?a:\X)", "e\u0301", "(0,1)")]
    public void Word_line_and_grapheme_rules_follow_the_scopes_encoding(
        string pattern,
        string subject,
        string expected
    ) => Search(pattern, subject).Should().Be(expected, pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// Two alternatives or two characters that differ only in their encoding are never merged into
    /// one (S91).
    /// </summary>
    /// <remarks>
    /// The optimiser hoists a prefix the alternatives share, turns single characters into a set and
    /// packs characters into one literal, each on node equality. Upstream's property and
    /// zero-width nodes leave the encoding out of equality, so it hoisted one encoding in place of
    /// both; the case-flag nodes would have done the same had the encoding been anywhere but in
    /// their case flags. Measured 2026-09-25 as in
    /// <see cref="A_case_insensitive_node_folds_with_its_scopes_encoding"/>.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">The span found, or "none".</param>
    [Test]
    // re: (0, 2); Perl: (0, 2); upstream: (0, 2).
    [Arguments("(?i)(?:(?a:k)x|ky)", "\u212Ay", "(0,2)")]
    // re: None; Perl: None; upstream: (0, 2).
    [Arguments("(?i)(?:(?a:k)x|ky)", "\u212Ax", "none")]
    // re: (1, 2); Perl: (1, 2); upstream: None.
    [Arguments(@"(?:\b\u00E9|(?a:\b)\u00E9)", "x\u00E9", "(1,2)")]
    // re: (0, 2); Perl: (0, 2); upstream: None.
    [Arguments(@"(?:(?a:\w)x|\wy)", "\u00E9y", "(0,2)")]
    public void Nodes_that_differ_only_in_encoding_are_not_merged(string pattern, string subject, string expected) =>
        Search(pattern, subject).Should().Be(expected, pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// What the compiler hands the engine besides the code folds with a node's scoped encoding, or
    /// is not handed over at all (S91).
    /// </summary>
    /// <remarks>
    /// Upstream folds all three with the pattern's encoding. The firstset is a zero-width test at
    /// the head of the code, so a wrong one refuses matches, and
    /// <see cref="Nodes_that_differ_only_in_encoding_are_not_merged"/> shows it; the other two are
    /// not read at match time today - the required-string search takes only a case-sensitive
    /// string and the folded named lists are not consulted - so this pins the compiled pattern.
    /// </remarks>
    [Test]
    public void The_compiled_pattern_folds_with_a_scoped_encoding_or_declines()
    {
        // A required string whose case folding is the scope's is not offered: the engine's
        // required-string search would fold with the pattern's. Control: its global twin has one.
        PatternCompiler.Compile("(?i)(?a:kx)").ReqChars.Should().BeEmpty();
        PatternCompiler.Compile("(?ai)kx").ReqChars.Should().Equal('k', 'x');

        // No firstset folds members of two encodings with one. Control: one encoding gets one.
        ((Opcode)PatternCompiler.Compile("(?ai)(?:(?u:k)|x)y").Code[0])
            .Should()
            .NotBe(Opcode.SetUnionIgn);
        ((Opcode)PatternCompiler.Compile("(?ai)(?:k|x)y").Code[0]).Should().Be(Opcode.SetUnionIgn);

        // A named list folds with the encoding its reference folds with: ASCII leaves U+212A alone.
        Dictionary<string, IReadOnlyList<string>> lists = new() { ["w"] = ["\u212A"] };
        PatternCompiler.Compile(@"(?i)(?a:\L<w>)", namedLists: lists).NamedListIndexes[0].Should().Equal("\u212A");
        PatternCompiler.Compile(@"(?i)\L<w>", namedLists: lists).NamedListIndexes[0].Should().Equal("k");
    }

    private static string Search(string pattern, string subject)
    {
        Dictionary<string, IReadOnlyCollection<string>>? lists = pattern.Contains("L<w>", StringComparison.Ordinal)
            ? new() { ["w"] = ["k"] }
            : null;
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(5), lists).Match(subject);

        return m.Success ? $"({m.Index},{m.Index + m.Length})" : "none";
    }
}
