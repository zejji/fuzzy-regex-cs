using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The <c>_IGN</c> and <c>_FLD</c> opcodes at match time: simple case folding, full case folding,
/// and the encoding each of them answers in.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value is upstream's, recorded against regex 2026.7.19 on 2026-08-31 with
/// <c>regex.search(pattern, subject)</c> over exactly the pattern text below, and quoted beside the
/// assertion. The flags are inline rather than options because <see cref="FuzzyRegexOptions"/> has
/// no <c>Ascii</c> member - upstream's <c>ASCII</c> is reachable only as <c>(?a)</c> - and mixing
/// the two forms across one table would make the rows harder to compare than the flags are worth.
/// </para>
/// <para>
/// Where upstream's span is in codepoints and ours in UTF-16 code units - every astral row - both
/// are quoted, so a translation slip cannot hide behind a comment that agrees with the code.
/// </para>
/// <para>
/// These are gap tests because upstream's own suite reaches almost none of them: its case-folding
/// assertions mostly go through <c>findall</c> and <c>subn</c>, which are S25 and S24, and it has
/// no test at all for a case-insensitive property under the ASCII flag - the row S22's oracle
/// generator diverged on.
/// </para>
/// </remarks>
public sealed class CaseInsensitiveMatchingTests
{
    /// <summary>
    /// Under IGNORECASE the three cased general categories collapse into "is it a cased letter",
    /// and Uppercase / Lowercase into Cased. Upstream writes that collapse out once per encoding
    /// arm of <c>matches_PROPERTY_IGN</c> (<c>upstream/src/_regex.c</c> line 2937), so it applies
    /// under the ASCII encoding too.
    /// </summary>
    /// <remarks>
    /// <b>Every expectation here is <c>regex.match</c>, and asserted through
    /// <c>FuzzyRegex.MatchAtStart</c>, deliberately.</b> Upstream's <c>search</c> answers
    /// differently on the ASCII rows - see
    /// <see cref="A_cased_ascii_property_is_found_by_our_search_where_upstreams_screen_refuses_it"/>.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether upstream matches.</param>
    [Test]
    // upstream: regex.match(r'(?i)\p{Lu}', 'a').span() == (0, 1)
    [Arguments(@"(?i)\p{Lu}", "a", true)]
    // upstream: regex.match(r'(?ai)\p{Lu}', 'a').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lu}", "a", true)]
    // upstream: regex.match(r'(?i)\p{Ll}', 'A').span() == (0, 1)
    [Arguments(@"(?i)\p{Ll}", "A", true)]
    // upstream: regex.match(r'(?ai)\p{Ll}', 'A').span() == (0, 1)
    [Arguments(@"(?ai)\p{Ll}", "A", true)]
    // upstream: regex.match(r'(?i)\p{Lt}', 'A').span() == (0, 1)
    [Arguments(@"(?i)\p{Lt}", "A", true)]
    // upstream: regex.match(r'(?ai)\p{Lt}', 'A').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lt}", "A", true)]
    // upstream: regex.match(r'(?i)\p{Upper}', 'a').span() == (0, 1)
    [Arguments(@"(?i)\p{Upper}", "a", true)]
    // upstream: regex.match(r'(?ai)\p{Upper}', 'a').span() == (0, 1)
    [Arguments(@"(?ai)\p{Upper}", "a", true)]
    // upstream: regex.match(r'(?i)\p{Lower}', 'A').span() == (0, 1)
    [Arguments(@"(?i)\p{Lower}", "A", true)]
    // upstream: regex.match(r'(?ai)\p{Lower}', 'A').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lower}", "A", true)]
    // \p{L} is the control: case-insensitive already, so the collapse never applies to it and it
    // answers from the encoding's own table.
    // upstream: regex.match(r'(?ai)\p{L}', 'a').span() == (0, 1)
    [Arguments(@"(?ai)\p{L}", "a", true)]
    // ... and that table still clamps under ASCII, so a fullwidth digit is answered as unassigned.
    // upstream: regex.match(r'(?i)\p{Nd}', '\uff19').span() == (0, 1)
    [Arguments(@"(?i)\p{Nd}", "\uFF19", true)]
    // upstream: regex.match(r'(?ai)\p{Nd}', '\uff19') is None
    [Arguments(@"(?ai)\p{Nd}", "\uFF19", false)]
    // A caseless letter is not Cased, so the Uppercase/Lowercase collapse does not sweep it in.
    // U+118C0 WARANG CITI SMALL LETTER NGAA is cased; U+110C0 SHARADA SIGN JIHVAMULIYA is not.
    // upstream: regex.match(r'(?i)\p{Ll}', '\U000118c0').span() == (0, 1)
    [Arguments(@"(?i)\p{Ll}", "\U000118C0", true)]
    // upstream: regex.match(r'(?i)\p{Ll}', '\U000110c0') is None
    [Arguments(@"(?i)\p{Ll}", "\U000110C0", false)]
    // The same property under a *_REPEAT_ONE, so the count runs through count_one's bulk-stepper
    // path rather than the dispatch switch. Upstream's bulk stepper calls a different function
    // (match_many_PROPERTY_IGN -> the encoding table's has_property_ign, which does not collapse
    // under ASCII); these rows are what says the two agree in practice, and why this port has one
    // predicate rather than two.
    // upstream: regex.match(r'(?i)\p{Lu}+', 'a').span() == (0, 1)
    [Arguments(@"(?i)\p{Lu}+", "a", true)]
    // upstream: regex.match(r'(?ai)\p{Lu}+', 'a').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lu}+", "a", true)]
    // upstream: regex.fullmatch(r'(?ai)\p{Lt}+?', 'I').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lt}+?", "I", true)]
    // A scoped encoding beats the pattern's, in both directions - though for a cased property the
    // collapse makes both answers the same, which is itself worth pinning.
    // upstream: regex.match(r'(?i)(?a:\p{Lu})', 'a').span() == (0, 1)
    [Arguments(@"(?i)(?a:\p{Lu})", "a", true)]
    // upstream: regex.match(r'(?ai)(?u:\p{Lu})', 'a').span() == (0, 1)
    [Arguments(@"(?ai)(?u:\p{Lu})", "a", true)]
    public void A_cased_property_under_ignore_case_collapses_into_is_it_a_cased_letter(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Success.Should().Be(expected);

    /// <summary>
    /// <b>A known difference from upstream's matcher.</b> Under ASCII|IGNORECASE a cased property
    /// means the 52 ASCII letters, so no character above U+007F matches it.
    /// </summary>
    /// <remarks>
    /// Upstream's matcher runs the case collapse on the raw character, so its <c>match</c>
    /// accepts É, é, ĸ, the Kelvin sign and U+118C0 below, although the case-sensitive
    /// <c>(?a)\p{Lu}</c> refuses every one of them and no case variant of any is ASCII. Upstream's
    /// own set form, <c>(?ai)[\p{Lu}x]</c>, and its search screen refuse them. The rule is what
    /// Perl 5.42 answers for <c>[[:upper:]]</c> under <c>/ai</c>, PCRE2 10.47 without UCP under
    /// CASELESS, and Python's re documents for <c>[A-Z]</c> under ASCII|IGNORECASE ("only letters
    /// 'a' to 'z' and 'A' to 'Z' are matched"). Measured 2026-09-25; ledger entry 34.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether this port matches.</param>
    [Test]
    // upstream: regex.match(r'(?ai)\p{Ll}', '\U000118c0').span() == (0, 1)
    [Arguments(@"(?ai)\p{Ll}", "\U000118C0", false)]
    // upstream: regex.match(r'(?ai)\p{Lu}', '\xc9').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lu}", "\u00C9", false)]
    // upstream: regex.match(r'(?ai)\p{Lu}', '\xe9').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lu}", "\u00E9", false)]
    // upstream: regex.match(r'(?ai)\p{Ll}', '\u0138').span() == (0, 1)
    [Arguments(@"(?ai)\p{Ll}", "\u0138", false)]
    // upstream: regex.match(r'(?ai)\p{Lu}', '\u212a').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lu}", "\u212A", false)]
    // upstream: regex.match(r'(?ai)[[:upper:]]', '\xe9').span() == (0, 1)
    [Arguments(@"(?ai)[[:upper:]]", "\u00E9", false)]
    // upstream: regex.match(r'(?ai)\p{Lower}', '\xc9').span() == (0, 1)
    [Arguments(@"(?ai)\p{Lower}", "\u00C9", false)]
    // The ASCII letters still collapse, both ways.
    [Arguments(@"(?ai)\p{Lu}", "a", true)]
    [Arguments(@"(?ai)[[:lower:]]", "A", true)]
    // A scoped Unicode encoding lifts the clamp, and the set form agrees with the bare one.
    // upstream: regex.match(r'(?ai)(?u:\p{Lu})', '\xe9').span() == (0, 1)
    [Arguments(@"(?ai)(?u:\p{Lu})", "\u00E9", true)]
    // upstream: regex.match(r'(?ai)[\p{Lu}x]', '\xc9') is None
    [Arguments(@"(?ai)[\p{Lu}x]", "\u00C9", false)]
    [Arguments(@"(?ai)[\p{Lu}x]", "a", true)]
    public void A_cased_property_under_ascii_and_ignore_case_matches_only_ascii_letters(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Success.Should().Be(expected);

    /// <summary>
    /// <b>A known difference from upstream, not parity.</b> Upstream does not agree with itself
    /// about a cased property under the ASCII encoding, and this port answers it one way
    /// throughout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three code paths reach for three different predicates and only one of them does the
    /// collapse: the dispatch switch calls <c>matches_PROPERTY_IGN</c>, which collapses;
    /// <c>count_one</c>'s bulk stepper calls the encoding table's <c>has_property_ign</c>, which
    /// does not under ASCII; and <c>search_start</c> screens candidate positions with a third.
    /// Measured against regex 2026.7.19 on 2026-08-31, <c>regex.match</c> against
    /// <c>'KsKK'</c>:
    /// </para>
    /// <code>
    /// pattern        (?ai)      (?ui)
    /// \p{Ll}         (0, 1)     (0, 1)
    /// \p{Ll}{2}      (0, 2)     (0, 2)
    /// \p{Ll}{4}      (0, 4)     (0, 4)
    /// \p{Ll}+        (0, 2)     (0, 4)
    /// \p{Ll}*        (0, 0)     (0, 4)
    /// (\p{Ll})+      (0, 4)     (0, 4)
    /// </code>
    /// <para>
    /// A greedy <c>*</c> that consumes nothing where <c>+</c> consumes two is not a rule any
    /// predicate states, and <c>regex.search(r'(?ai)\p{Ll}', 'A')</c> being <c>None</c> where
    /// <c>regex.match</c> of the same pair spans <c>(0, 1)</c> is the same fault from the search
    /// side - <c>regex.search(r'(?ai)x?\p{Ll}', 'A')</c>, which defeats the screen, spans
    /// <c>(0, 1)</c> again. This port collapses in every path, so it answers <c>(0, 4)</c> for all
    /// three repeat forms and finds the searched match. The rows below say so out loud, so that a
    /// later slice changing them is a decision rather than an accident; the oracle's
    /// <c>case-folding</c> generator keeps the ASCII flag off property rows for the same reason.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedLength">What this port answers, anchored at 0.</param>
    [Test]
    // upstream: regex.match(r'(?ai)\p{Ll}+', 'KsKK').span() == (0, 2)
    [Arguments(@"(?ai)\p{Ll}+", "KsKK", 4)]
    // upstream: regex.match(r'(?ai)\p{Ll}*', 'KsKK').span() == (0, 0)
    [Arguments(@"(?ai)\p{Ll}*", "KsKK", 4)]
    // upstream: regex.match(r'(?ai)\p{Ll}{1,3}', 'KsKK').span() == (0, 2)
    [Arguments(@"(?ai)\p{Ll}{1,3}", "KsKK", 3)]
    // The fixed counts and the grouped repeat agree with us already, and are here as the control:
    // whatever a later slice does to the rows above must leave these alone.
    // upstream: regex.match(r'(?ai)\p{Ll}{4}', 'KsKK').span() == (0, 4)
    [Arguments(@"(?ai)\p{Ll}{4}", "KsKK", 4)]
    // upstream: regex.match(r'(?ai)(\p{Ll})+', 'KsKK').span() == (0, 4)
    [Arguments(@"(?ai)(\p{Ll})+", "KsKK", 4)]
    public void A_repeated_cased_property_under_ascii_is_where_upstream_disagrees_with_itself(
        string pattern,
        string subject,
        int expectedLength
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Length.Should().Be(expectedLength);

    /// <summary>
    /// The search half of the same upstream inconsistency: our search finds what upstream's own
    /// <c>match</c> finds and upstream's <c>search</c> does not. <b>A known difference, not
    /// parity</b> - see
    /// <see cref="A_repeated_cased_property_under_ascii_is_where_upstream_disagrees_with_itself"/>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    [Test]
    // upstream: regex.match(...) spans (0, 1); regex.search(...) is None
    [Arguments(@"(?ai)\p{Ll}", "A")]
    // upstream: regex.match(...) spans (0, 1); regex.search(...) is None
    [Arguments(@"(?ai)\p{Lu}", "a")]
    // upstream: regex.match(...) spans (0, 1); regex.search(...) is None
    [Arguments(@"(?ai)\p{Upper}", "a")]
    public void A_cased_ascii_property_is_found_by_our_search_where_upstreams_screen_refuses_it(
        string pattern,
        string subject
    ) => FuzzyRegex.Match(subject, pattern).Success.Should().BeTrue();

    /// <summary>
    /// <c>CHARACTER_IGN</c>, <c>STRING_IGN</c>, <c>RANGE_IGN</c>, the <c>SET_*_IGN</c> family and
    /// <c>REF_GROUP_IGN</c>: simple folding, where both sides always advance one character.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedIndex">Upstream's index, or -1 for no match.</param>
    /// <param name="expectedLength">Upstream's length.</param>
    [Test]
    // The Kelvin sign folds to 'k' in both directions.
    // upstream: regex.search(r'(?i)k', '\u212a').span() == (0, 1)
    [Arguments("(?i)k", "\u212A", 0, 1)]
    // upstream: regex.search('(?i)\u212a', 'k').span() == (0, 1)
    [Arguments("(?i)\u212A", "k", 0, 1)]
    // upstream: regex.search(r'(?i)abc', 'ABC').span() == (0, 3)
    [Arguments("(?i)abc", "ABC", 0, 3)]
    // U+017F LATIN SMALL LETTER LONG S folds to 's'.
    // upstream: regex.search('(?i)\u017f', 's').span() == (0, 1)
    [Arguments("(?i)\u017F", "s", 0, 1)]
    // upstream: regex.search('(?i)\u017f', 'S').span() == (0, 1)
    [Arguments("(?i)\u017F", "S", 0, 1)]
    // The plain pair folds together, here and upstream.
    // upstream: regex.search(r'(?i)i', 'I').span() == (0, 1)
    [Arguments("(?i)i", "I", 0, 1)]
    // upstream: regex.search('(?i)i', '\u0131') is None
    [Arguments("(?i)i", "\u0131", -1, 0)]
    // upstream: regex.search('(?i)\u0130', '\u0131') is None
    [Arguments("(?i)\u0130", "\u0131", -1, 0)]
    // The two rows where upstream pairs a dotted form with a dotless one - '(?i)i' against U+0130,
    // and '(?i)I' against U+0131 - are NOT here any more. S45 replaced upstream's Turkic case data
    // with the default one CaseFolding.txt specifies, so this port no longer matches where upstream
    // does on either. They live in Gaps.Engine.CaseFoldingTests, which asserts the whole 25-cell
    // grid against the definitive source and against PCRE2, Perl and .NET.
    // Cherokee folds *upward*: the small letters at U+AB70 fold into the U+13A0 block.
    // upstream: regex.search('(?i)\u13a0', '\uab70').span() == (0, 1)
    [Arguments("(?i)\u13A0", "\uAB70", 0, 1)]
    // upstream: regex.search('(?i)\uab70', '\u13a0').span() == (0, 1)
    [Arguments("(?i)\uAB70", "\u13A0", 0, 1)]
    // Final sigma folds with both the medial small sigma and the capital.
    // upstream: regex.search('(?i)\u03c3', '\u03c2').span() == (0, 1)
    [Arguments("(?i)\u03C3", "\u03C2", 0, 1)]
    // upstream: regex.search('(?i)\u03a3', '\u03c2').span() == (0, 1)
    [Arguments("(?i)\u03A3", "\u03C2", 0, 1)]
    // RANGE_IGN tests the *subject's* cases against the range, so a codepoint far outside it still
    // matches when one of its cases is inside.
    // upstream: regex.search(r'(?i)[a-z]', 'K').span() == (0, 1)
    [Arguments("(?i)[a-z]", "K", 0, 1)]
    // upstream: regex.search('(?i)[a-z]', '\u212a').span() == (0, 1)
    [Arguments("(?i)[a-z]", "\u212A", 0, 1)]
    // ... and a negated range therefore excludes it.
    // upstream: regex.search(r'(?i)[^a-z]', 'K') is None
    [Arguments("(?i)[^a-z]", "K", -1, 0)]
    // SET_UNION_IGN, both ways round.
    // upstream: regex.search('(?i)[k]', '\u212a').span() == (0, 1)
    [Arguments("(?i)[k]", "\u212A", 0, 1)]
    // upstream: regex.search('(?i)[\u212a]', 'k').span() == (0, 1)
    [Arguments("(?i)[\u212A]", "k", 0, 1)]
    // The ASCII encoding's all_cases knows only A-Z and a-z, which is enough for this row and is
    // what would break if the Unicode table were consulted regardless of the flag.
    // upstream: regex.search(r'(?ai)[a-z]', 'K').span() == (0, 1)
    [Arguments("(?ai)[a-z]", "K", 0, 1)]
    // REF_GROUP_IGN: the captured text is compared against the subject, ignoring case.
    // upstream: regex.search(r'(?i)(a)\1', 'aA').span() == (0, 2)
    [Arguments(@"(?i)(a)\1", "aA", 0, 2)]
    // upstream: regex.search('(?i)(k)\\1', 'K\u212a').span() == (0, 2)
    [Arguments(@"(?i)(k)\1", "K\u212A", 0, 2)]
    // Without FULLCASE a ligature is one character that folds to itself, so none of the expanding
    // rows in the next test match through this path.
    // upstream: regex.search('(?i)ffi', '\ufb03') is None
    [Arguments("(?i)ffi", "\uFB03", -1, 0)]
    // upstream: regex.search('(?i)stra\u00dfe', 'STRASSE') is None
    [Arguments("(?i)stra\u00DFe", "STRASSE", -1, 0)]
    // upstream: regex.search('(?i)(ss)\\1', '\u00df\u00df') is None
    [Arguments(@"(?i)(ss)\1", "\u00DF\u00DF", -1, 0)]
    public void Simple_folding_matches_where_upstream_does(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength
    ) =>
        // (?V0) because S50b made Version 1 - and so FULLCASE - this port's default, while every
        // row above quotes upstream's answer under its own VERSION0 default. The full-folding
        // twin is the next test down, and it says (?f) for itself.
        ShouldMatchUpstream("(?V0)" + pattern, subject, expectedIndex, expectedLength);

    /// <summary>
    /// <c>STRING_FLD</c> and <c>REF_GROUP_FLD</c>: full case folding, where one character on one
    /// side answers for up to three on the other and the two sides advance at different rates.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedIndex">Upstream's index.</param>
    /// <param name="expectedLength">Upstream's length.</param>
    [Test]
    // The subject expands: one U+FB03 folds to 'f', 'f', 'i', so three pattern characters are
    // consumed against one subject character and the match is one unit long.
    // upstream: regex.search('(?fi)ffi', '\ufb03').span() == (0, 1)
    [Arguments("(?fi)ffi", "\uFB03", 0, 1)]
    // The pattern expands instead: the parser folded U+FB03 into 'f','f','i' at compile time, so
    // three subject characters are consumed.
    // upstream: regex.search('(?fi)\ufb03', 'ffi').span() == (0, 3)
    [Arguments("(?fi)\uFB03", "ffi", 0, 3)]
    // upstream: regex.search('(?fi)st', '\ufb06').span() == (0, 1)
    [Arguments("(?fi)st", "\uFB06", 0, 1)]
    // U+FB05 LATIN SMALL LIGATURE LONG S T folds to 's','t' - through the long s, not directly.
    // upstream: regex.search('(?fi)\ufb05', 'st').span() == (0, 2)
    [Arguments("(?fi)\uFB05", "st", 0, 2)]
    // A literal that is partly ordinary and partly expanding, so the folded position has to survive
    // characters that fold to themselves as well as ones that do not.
    // upstream: regex.search('(?fi)po\ufb06', 'post').span() == (0, 4)
    [Arguments("(?fi)po\uFB06", "post", 0, 4)]
    // The sharp s, whose folding is two characters rather than a ligature's two or three.
    // upstream: regex.search('(?fi)stra\u00dfe', 'STRASSE').span() == (0, 7)
    [Arguments("(?fi)stra\u00DFe", "STRASSE", 0, 7)]
    // REF_GROUP_FLD, both ways round: the captured text and the subject are folded independently,
    // so each side advances only when its own folding runs out.
    // upstream: regex.search('(?fi)(\ufb03)\\1', 'ffiffi').span() == (0, 6)
    [Arguments(@"(?fi)(\uFB03)\1", "ffiffi", 0, 6)]
    // upstream: regex.search('(?fi)(ffi)\\1', '\ufb03\ufb03').span() == (0, 2)
    [Arguments(@"(?fi)(ffi)\1", "\uFB03\uFB03", 0, 2)]
    // upstream: regex.search('(?fi)(\u00df)\\1', 'ssSS').span() == (0, 4)
    [Arguments(@"(?fi)(\u00DF)\1", "ssSS", 0, 4)]
    // upstream: regex.search('(?fi)(ss)\\1', '\u00df\u00df').span() == (0, 2)
    [Arguments(@"(?fi)(ss)\1", "\u00DF\u00DF", 0, 2)]
    public void Full_case_folding_matches_where_upstream_does(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength
    ) => ShouldMatchUpstream(pattern, subject, expectedIndex, expectedLength);

    /// <summary>
    /// Case-insensitive matching over astral characters, where upstream's one codepoint is this
    /// port's two UTF-16 code units. Each of these agrees with upstream on *whether* it matches
    /// however the position is stepped, and would differ only in the span - which is what makes
    /// them the rows that catch a stepping slip.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedIndex">Upstream's index, translated to UTF-16.</param>
    /// <param name="expectedLength">Upstream's length, translated to UTF-16.</param>
    [Test]
    // U+10400 DESERET CAPITAL LONG I and U+10428 DESERET SMALL LONG I fold together.
    // upstream: regex.search('(?i)\U00010400', '\U00010428').span() == (0, 1) codepoints
    [Arguments("(?i)\U00010400", "\U00010428", 0, 2)]
    // upstream: regex.search('(?i)\U00010428', '\U00010400').span() == (0, 1) codepoints
    [Arguments("(?i)\U00010428", "\U00010400", 0, 2)]
    // SET_UNION_IGN over an astral member.
    // upstream: regex.search('(?i)[\U00010400]', '\U00010428').span() == (0, 1) codepoints
    [Arguments("(?i)[\U00010400]", "\U00010428", 0, 2)]
    // RANGE_IGN whose bounds are astral and whose subject is outside the range until it is folded.
    // upstream: regex.search('(?i)[\U00010400-\U00010427]', '\U00010428').span() == (0, 1)
    // codepoints
    [Arguments("(?i)[\U00010400-\U00010427]", "\U00010428", 0, 2)]
    // A *_REPEAT_ONE over an astral character, so count_one steps by codepoint and not by unit.
    // upstream: regex.search('(?i)\U00010400+', '\U00010428\U00010428').span() == (0, 2) codepoints
    [Arguments("(?i)\U00010400+", "\U00010428\U00010428", 0, 4)]
    // REF_GROUP_IGN where both the capture and the subject are astral, so the group position and
    // the text position each have to advance a whole character.
    // upstream: regex.search('(?i)(\U00010400)\\1', '\U00010428\U00010400').span() == (0, 2)
    // codepoints
    [Arguments("(?i)(\U00010400)\\1", "\U00010428\U00010400", 0, 4)]
    public void Astral_case_insensitive_spans_are_reported_in_utf16_units(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength
    ) => ShouldMatchUpstream(pattern, subject, expectedIndex, expectedLength);

    /// <summary>
    /// <b>Two more known differences from upstream, not parity</b>, both found by S22's blind
    /// review and both in machinery this port defers to Phase 7 rather than in the folding itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first: a whole pattern that is one <c>STRING_FLD</c> goes through
    /// <c>locate_required_string</c> and <c>string_search_fld</c>, which compares with
    /// <c>same_char_ign_turkic</c> (<c>upstream/src/_regex.c</c> line 6687) and then lets the
    /// <c>RE_STATUS_REQUIRED</c> arm skip the real comparison - so the dotless i folds with 'i'
    /// there and nowhere else in upstream. Splitting the string defeats it and upstream agrees
    /// with this port again. Measured against regex 2026.7.19 on 2026-08-31:
    /// </para>
    /// <code>
    /// regex.match('(?fi)fi', 'fı')    -> (0, 2)
    /// regex.match('(?fi)(f)i', 'fı')  -> None
    /// regex.match('(?fi)i', 'ı')      -> None
    /// </code>
    /// <para>
    /// The second: upstream's <c>GREEDY_REPEAT_ONE</c> retreat fast path (<c>:16041</c>) clamps the
    /// position it retreats to by a folded length it recomputes from the pattern's *already
    /// folded* values, over-counts, and gives up before reaching the position that matches. Only
    /// the <c>default</c> arm of that sub-switch is ported, so this port retreats one character at
    /// a time and finds it:
    /// </para>
    /// <code>
    /// regex.match('(?fi).*ẖẛ', 'ẖṡ')  -> None
    /// regex.match('(?fi)x?ẖẛ', 'ẖṡ')  -> (0, 2)
    /// regex.match('(?fi)ẖẛ', 'ẖṡ')    -> (0, 2)
    /// </code>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedIndex">What this port answers, anchored at 0, or -1 for no match.</param>
    /// <param name="expectedLength">What this port answers.</param>
    [Test]
    // upstream: (0, 2), through string_search_fld's Turkic comparison
    [Arguments("(?fi)fi", "fı", -1, 0)]
    // upstream: None - the control that says the difference is the required string, not the folding
    [Arguments("(?fi)(f)i", "fı", -1, 0)]
    // upstream: None - the same control at one character
    [Arguments("(?fi)i", "ı", -1, 0)]
    // upstream: None, from the repeat-one retreat fast path this port does not have
    [Arguments("(?fi).*ẖẛ", "ẖṡ", 0, 2)]
    // upstream: (0, 2) - the same pattern with the fast path defeated
    [Arguments("(?fi)x?ẖẛ", "ẖṡ", 0, 2)]
    // upstream: (0, 2)
    [Arguments("(?fi)ẖẛ", "ẖṡ", 0, 2)]
    public void Two_phase_7_deferrals_show_through_on_full_case_folding(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength
    ) => ShouldMatchUpstream(pattern, subject, expectedIndex, expectedLength, anchored: true);

    /// <summary>Matches, and checks the span against the expected one.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expectedIndex">The expected index in UTF-16 units, or -1 for no match.</param>
    /// <param name="expectedLength">The expected length in UTF-16 units.</param>
    /// <param name="anchored">Anchor at position 0 rather than searching.</param>
    private static void ShouldMatchUpstream(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength,
        bool anchored = false
    )
    {
        Match m = anchored ? FuzzyRegex.MatchAtStart(subject, pattern) : FuzzyRegex.Match(subject, pattern);

        if (expectedIndex < 0)
        {
            m.Success.Should().BeFalse();
            return;
        }

        (m.Index, m.Length).Should().Be((expectedIndex, expectedLength));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// Every spelling of one case-insensitive property question gives one answer: bare, in a set,
    /// in an alternation, after an optional item.
    /// </summary>
    /// <remarks>
    /// Under IGNORECASE a cased property means any cased letter, the rule Perl 5.42 and .NET 10's
    /// <c>Regex</c> apply to a bare property and a set alike, and upstream's for a bare property
    /// (UTS #18 RL1.5 lets an implementation choose). Upstream's set members used case closure
    /// instead, so its answer turned on the spelling. Upstream, measured 2026-09-25 on regex
    /// 2026.9.10, over U+0138 and 'a':
    /// <code>
    /// (?i)\p{Lu}      U+0138 match      (?i)[\p{Lu}x]   U+0138 None
    /// (?i)\p{Lt}      'a'    match      (?i)x?\p{Lt}    'a'    None     (?i)[\p{Lt}x]  'a' None
    /// </code>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    [Test]
    [Arguments(@"(?i)[\p{Lu}x]", "\u0138")]
    [Arguments(@"(?i)\p{Lu}|z", "\u0138")]
    [Arguments(@"(?i)[\p{Lt}x]", "a")]
    [Arguments(@"(?i)\p{Lt}|z", "a")]
    [Arguments(@"(?i)x?\p{Lt}", "a")]
    [Arguments(@"(?i)y*\p{Lt}", "a")]
    [Arguments(@"(?ai)x?\p{Lt}", "a")]
    [Arguments(@"(?ai)[\p{Lt}x]", "a")]
    [Arguments(@"(?i)[[:upper:]x]", "\u0138")]
    public void A_cased_property_in_a_set_answers_as_the_bare_property_does(string pattern, string subject) =>
        FuzzyRegex.FullMatch(subject, pattern).Success.Should().BeTrue(pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// <c>\p{Upper=No}</c> under IGNORECASE is the complement of <c>\p{Upper}</c>, as
    /// <c>\P{Upper}</c> is.
    /// </summary>
    /// <remarks>
    /// Upstream collapses Uppercase and Lowercase whatever value was asked for
    /// (<c>_regex.c:2981</c>), so <c>regex.fullmatch(r'(?i)\p{Upper=No}', 'a')</c> matches.
    /// Perl 5.42 answers <c>\p{Upper=N}</c> and <c>\p{Lowercase=No}</c> under <c>/i</c> with no
    /// match over 'a' and 'A', and a match over '1' (measured 2026-09-25).
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches.</param>
    [Test]
    [Arguments(@"(?i)\p{Upper=No}", "a", false)]
    [Arguments(@"(?i)\p{Upper=No}", "A", false)]
    [Arguments(@"(?i)\p{Upper=No}", "1", true)]
    [Arguments(@"(?i)\p{Lowercase=False}", "A", false)]
    [Arguments(@"(?i)\p{Lowercase=False}", "1", true)]
    [Arguments(@"(?ai)\p{Upper=No}", "\u00E9", true)]
    [Arguments(@"(?ai)\P{Upper}", "\u00E9", true)]
    [Arguments(@"(?i)[\p{Upper=No}x]", "a", false)]
    public void A_no_value_of_a_cased_property_is_its_complement(string pattern, string subject, bool expected) =>
        FuzzyRegex.FullMatch(subject, pattern).Success.Should().Be(expected, pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// Under IGNORECASE each member of a set matches case-insensitively first, and only then do
    /// the set's operations combine the answers, so nesting a member or naming it in an operation
    /// never changes what it matches.
    /// </summary>
    /// <remarks>
    /// Perl 5.42's <c>(?[ ... ])</c> and .NET 10's <c>[...-[...]]</c> both answer this way under
    /// <c>/i</c> (measured 2026-09-25): <c>[\w--\p{Lu}]</c> refuses every letter, as
    /// <c>\p{Lu}</c> matches every cased letter, and it matches U+0345, a mark and no letter.
    /// Upstream instead tests each case variant of the character against the case-sensitive set,
    /// so the answer turned on how the set was written. Upstream, regex 2026.9.10, V1:
    /// <code>
    /// (?i)[\p{Lu}]      U+0345 None       (?i)[[\p{Lu}]x]   U+0345 match
    /// (?i)\p{Greek}     U+00B5 None       (?i)[\p{Greek}x]  U+00B5 match
    /// (?i)[\w--\p{Lu}]  U+0138 match      (?i)\p{Lu}        U+0138 match
    /// </code>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches.</param>
    [Test]
    [Arguments(@"(?i)[\p{Lu}x]", "\u0345", false)]
    [Arguments(@"(?i)[[\p{Lu}]x]", "\u0345", false)]
    [Arguments(@"(?i)[\p{Greek}x]", "\u00B5", false)]
    [Arguments(@"(?i)[[\p{Greek}]x]", "\u0345", false)]
    [Arguments(@"(?i)[\w--\p{Lu}]", "\u0138", false)]
    [Arguments(@"(?i)[\w--\p{Lu}]", "a", false)]
    [Arguments(@"(?i)[\w--\p{Lu}]", "\u0345", true)]
    [Arguments(@"(?i)[\p{L}--\p{Lu}]", "a", false)]
    [Arguments(@"(?i)[[A-Z]--[a-z]]", "A", false)]
    [Arguments(@"(?i)[[a-z]&&[K]]", "\u212A", true)]
    [Arguments(@"(?i)[[a-z]--[k]]", "\u212A", false)]
    [Arguments(@"(?i)[x[\w--\p{Lu}]]", "a", false)]
    [Arguments(@"(?i)[x[\w--\p{Lu}]]", "\u0345", true)]
    [Arguments(@"(?i)[x[[a-z]&&[K]]]", "\u212A", true)]
    [Arguments(@"(?i)[x[[a-z]&&[K]]]", "a", false)]
    public void Set_members_match_case_insensitively_before_the_set_combines_them(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.FullMatch(subject, pattern).Success.Should().Be(expected, pattern);

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    /// <summary>
    /// A set's full case-folding expansions (<c>ss</c> for <c>ß</c>) follow the same rule as its
    /// single characters: a member that refuses 's' case-insensitively cannot let <c>ß</c> or
    /// <c>ss</c> through.
    /// </summary>
    /// <remarks>
    /// The expansions are chosen when the set is parsed. Ledger 35 changed how a cased property
    /// answers under IGNORECASE at match time but left that choice asking the case-sensitive
    /// property, so <c>(?i)[\P{Lu}x]</c> refused 's' and matched <c>ß</c> and <c>ss</c>, while
    /// <c>(?i)\P{Lu}</c> refused all three (found by the blind review of ledger 35, 2026-09-25).
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    [Test]
    [Arguments(@"(?i)[\P{Lu}x]", "\u00DF")]
    [Arguments(@"(?i)[\P{Lu}x]", "ss")]
    [Arguments(@"(?i)[\p{Upper=No}x]", "ss")]
    [Arguments(@"(?i)[\p{L}--\p{Lt}]", "ss")]
    [Arguments(@"(?i)[\w--\p{Lu}]", "\u00DF")]
    public void A_set_expands_to_a_full_folding_only_when_its_members_accept_the_folded_letters(
        string pattern,
        string subject
    ) => FuzzyRegex.FullMatch(subject, pattern).Success.Should().BeFalse(pattern);
}
