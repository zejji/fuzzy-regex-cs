using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The four dotted and dotless I codepoints under the DEFAULT (non-Turkic) case data this port
/// substitutes for upstream's. Every expectation here is the definitive source's, not upstream's.
/// </summary>
/// <remarks>
/// <para>
/// <b>These tests deliberately disagree with <c>regex 2026.9.10</c>.</b>
/// <see cref="Fuzzy.Text.RegularExpressions.Unicode.TurkicDefaults"/> carries the quoted
/// <c>CaseFolding.txt</c> rows, the UTS #18 RL1.5 wording and the reasoning; this file is the
/// behaviour those rows imply, at the operations a user reaches. The oracle entry
/// <c>turkic-default-folding</c> classifies the same divergence for the wave.
/// </para>
/// <para>
/// <b>The grid, and who agrees with it.</b> Run on 2026-09-14 by <c>.scratch/s45-definition.py</c>
/// (PCRE2 10.47 via <c>libpcre2-8-0.dll</c>, <c>PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS</c>),
/// <c>.scratch/s45-perl.pl</c> (Perl 5.42.2, <c>/i</c> under <c>(?u:...)</c>, which folds fully) and
/// <c>.scratch/s45-dotnet.ps1</c> (.NET 10.0.10, <c>IgnoreCase | CultureInvariant</c>). The four
/// cells upstream answers alone on are <c>I</c>~<c>ı</c>, <c>ı</c>~<c>I</c>, <c>i</c>~<c>İ</c> and
/// <c>İ</c>~<c>i</c>: upstream matches all four, the three second engines match none of them, and
/// after S45 nor does this port.
/// </para>
/// <para>
/// All four codepoints are in the BMP, so no UTF-16 index translation applies to any span here.
/// </para>
/// </remarks>
public sealed class CaseFoldingTests
{
    private const string _dottedCapital = "İ";
    private const string _dotlessSmall = "ı";
    private const string _dottedSmall = "i̇";

    /// <summary>
    /// Simple <c>(?i)</c> matching over the whole 25-cell grid. PCRE2 and .NET, which both fold
    /// simply, answer every cell exactly as below.
    /// </summary>
    /// <param name="pattern">The literal to case-fold.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches under the default case data.</param>
    [Test]
    // The dotted capital is alone in its case set: it matches itself and nothing else.
    [Arguments("İ", "İ", true)]
    [Arguments("İ", "i̇", false)]
    // upstream: regex.fullmatch('(?i)İ', 'i') matches. PCRE2, Perl and .NET: no match.
    [Arguments("İ", "i", false)]
    [Arguments("İ", "I", false)]
    [Arguments("İ", "ı", false)]
    // 'i' + U+0307 is two characters, so a one-character pattern cannot fullmatch it.
    [Arguments("i̇", "İ", false)]
    [Arguments("i̇", "i̇", true)]
    [Arguments("i̇", "i", false)]
    [Arguments("i̇", "I", false)]
    [Arguments("i̇", "ı", false)]
    // I and i share the simple folding U+0069, and share it with nothing else.
    // upstream: regex.fullmatch('(?i)i', 'İ') matches. PCRE2, Perl and .NET: no match.
    [Arguments("i", "İ", false)]
    [Arguments("i", "i̇", false)]
    [Arguments("i", "i", true)]
    [Arguments("i", "I", true)]
    // upstream: regex.fullmatch('(?i)I', 'ı') matches. PCRE2, Perl and .NET: no match.
    [Arguments("i", "ı", false)]
    [Arguments("I", "İ", false)]
    [Arguments("I", "i̇", false)]
    [Arguments("I", "i", true)]
    [Arguments("I", "I", true)]
    [Arguments("I", "ı", false)]
    // The dotless small is alone in its case set too.
    [Arguments("ı", "İ", false)]
    [Arguments("ı", "i̇", false)]
    [Arguments("ı", "i", false)]
    // upstream: regex.fullmatch('(?i)ı', 'I') matches. PCRE2, Perl and .NET: no match.
    [Arguments("ı", "I", false)]
    [Arguments("ı", "ı", true)]
    public void Simple_ignore_case_pairs_the_dotted_I_forms_by_the_default_case_data(
        string pattern,
        string subject,
        bool expected
    ) =>
        // (?-f) because S50b made Version 1, and so full folding, this port's default; simple
        // folding is what this grid is about, and the full grid is the next test down.
        FuzzyRegex.FullMatch(subject, "(?i)(?-f)" + pattern).Success.Should().Be(expected);

    /// <summary>
    /// Full <c>(?fi)</c> matching over the same grid. Perl's <c>/i</c> folds fully and answers
    /// every cell exactly as below; the only cells that differ from the simple grid are the two
    /// where U+0130 expands.
    /// </summary>
    /// <param name="pattern">The literal to case-fold.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches under the default case data.</param>
    [Test]
    // THE SLICE'S OWN CELL. 0130; F; 0069 0307, so the dotted capital reaches the dotted small.
    // upstream: regex.fullmatch('(?fi)İ', 'i̇') is None. Perl: matches.
    [Arguments("İ", "i̇", true)]
    // ... and symmetrically, because full folding is applied to both sides.
    // upstream: regex.fullmatch('(?fi)i̇', 'İ') is None. Perl: matches.
    [Arguments("i̇", "İ", true)]
    [Arguments("İ", "İ", true)]
    // Folding U+0130 fully does not reach a bare 'i': the combining dot is still required.
    // upstream: regex.fullmatch('(?fi)İ', 'i') matches. Perl: no match.
    [Arguments("İ", "i", false)]
    [Arguments("İ", "I", false)]
    [Arguments("İ", "ı", false)]
    [Arguments("i̇", "i̇", true)]
    [Arguments("i̇", "i", false)]
    [Arguments("i̇", "I", false)]
    [Arguments("i̇", "ı", false)]
    // upstream: regex.fullmatch('(?fi)i', 'İ') matches. Perl: no match.
    [Arguments("i", "İ", false)]
    [Arguments("i", "i̇", false)]
    [Arguments("i", "i", true)]
    [Arguments("i", "I", true)]
    [Arguments("i", "ı", false)]
    [Arguments("I", "İ", false)]
    [Arguments("I", "i̇", false)]
    [Arguments("I", "i", true)]
    [Arguments("I", "I", true)]
    // upstream: regex.fullmatch('(?fi)I', 'ı') matches. Perl: no match.
    [Arguments("I", "ı", false)]
    [Arguments("ı", "İ", false)]
    [Arguments("ı", "i̇", false)]
    [Arguments("ı", "i", false)]
    [Arguments("ı", "I", false)]
    [Arguments("ı", "ı", true)]
    public void Full_ignore_case_pairs_the_dotted_I_forms_by_the_default_case_data(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.FullMatch(subject, "(?fi)" + pattern).Success.Should().Be(expected);

    /// <summary>
    /// The expansion also works inside a character class and inside a set, where the parser - not
    /// the matcher - is what has to reach U+0130. Upstream's expansion inventory holds U+0130
    /// (<c>get_expand_on_folding</c> lists it), so the set path was already looking for a character
    /// whose folding never expanded.
    /// </summary>
    [Test]
    public void A_class_holding_the_dotted_capital_reaches_the_dotted_small_under_full_folding() =>
        FuzzyRegex.FullMatch(_dottedSmall, "(?fi)[İx]").Success.Should().BeTrue();

    /// <summary>
    /// And the other way round a class does NOT reach it, because a set matches one character at a
    /// time and U+0130's case set holds only itself. Upstream's holds <c>i</c> as well, so
    /// upstream matches here and this port does not.
    /// </summary>
    /// <remarks>
    /// upstream: <c>regex.compile('[i̇x]', regex.I | regex.F).fullmatch('İ')</c> matches,
    /// measured 2026-09-14 on regex 2026.9.10. The set's <c>i</c> is what reaches U+0130 there; it
    /// is not the combining dot, which no single character can be paired with.
    /// </remarks>
    [Test]
    public void A_class_holding_the_dotted_small_does_not_reach_the_dotted_capital() =>
        FuzzyRegex.FullMatch(_dottedCapital, "(?fi)[i̇x]").Success.Should().BeFalse();

    /// <summary>
    /// Reversed matching, <c>(?r)</c>, folds the same way. It reads the subject backwards and
    /// reverses the folded run as it goes, so an expansion is the case most likely to be wrong in
    /// one direction only.
    /// </summary>
    [Test]
    public void The_reversed_matcher_folds_the_dotted_capital_the_same_way() =>
        FuzzyRegex.FullMatch(_dottedSmall, "(?rfi)İ").Success.Should().BeTrue();

    /// <summary>
    /// Reversed simple matching keeps the dotted capital out of I's case set, the same as forwards.
    /// </summary>
    [Test]
    public void The_reversed_matcher_keeps_the_dotted_capital_out_of_the_plain_I_case_set() =>
        FuzzyRegex.FullMatch(_dottedCapital, "(?ri)i").Success.Should().BeFalse();

    /// <summary>
    /// A partial match stops mid-expansion: the subject holds only the first codepoint of U+0130's
    /// full folding, so the match cannot succeed yet but has not failed either.
    /// </summary>
    [Test]
    public void A_subject_holding_only_the_first_codepoint_of_the_expansion_is_a_partial_match()
    {
        Match m = new FuzzyRegex("(?fi)İ").MatchAtStart("i", partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
    }

    /// <summary>
    /// The dotless small I is not a partial match of the dotted capital under any folding: it
    /// shares no codepoint with the expansion at all.
    /// </summary>
    [Test]
    public void The_dotless_small_is_not_even_a_partial_match_of_the_dotted_capital() =>
        new FuzzyRegex("(?fi)İ").MatchAtStart(_dotlessSmall, partial: true).Success.Should().BeFalse();

    /// <summary>
    /// Fuzzy matching costs the substitution it should. Under the default case data <c>I</c> and
    /// <c>ı</c> are different characters, so <c>(?i)I</c> reaches <c>ı</c> only by spending an
    /// error - where upstream reaches it for nothing.
    /// </summary>
    [Test]
    public void The_dotless_small_costs_a_substitution_against_a_plain_I()
    {
        FuzzyRegex.FullMatch(_dotlessSmall, "(?i)I").Success.Should().BeFalse();
        FuzzyRegex.FullMatch(_dotlessSmall, "(?i)I{s<=1}").Success.Should().BeTrue();
        FuzzyRegex.FullMatch(_dotlessSmall, "(?i)I{e<=0}").Success.Should().BeFalse();
    }

    /// <summary>
    /// A scan finds every plain I and i and stops there, where upstream's finds the dotted and
    /// dotless forms too.
    /// </summary>
    [Test]
    public void A_scan_over_all_four_forms_finds_only_the_plain_pair()
    {
        MatchCollection found = FuzzyRegex.Matches("Iİiı", "(?i)i");

        found.Count.Should().Be(2);
        found[0].Index.Should().Be(0);
        found[1].Index.Should().Be(2);
    }

    /// <summary>
    /// Substitution rewrites the same two and leaves the other two alone.
    /// </summary>
    [Test]
    public void A_substitution_over_all_four_forms_rewrites_only_the_plain_pair() =>
        FuzzyRegex.Replace("Iİiı", "(?i)i", "-").Should().Be("-İ-ı");

    /// <summary>
    /// <c>(?fi)FFI</c> - corpus row #313 - reaches the ligature, and <b>this is the row where the
    /// bytecode moved and the BEHAVIOUR did not</b>. That is what the test is for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// upstream: <c>regex.compile('FFI', regex.I | regex.F).fullmatch('ﬃ')</c> matches too,
    /// measured 2026-09-14 on regex 2026.9.10. Upstream's folded run is <c>ffI</c>, which its
    /// <c>.lower()</c> then rescues for the chunking decision; this port's is already <c>ffi</c>,
    /// because S45 restored <c>0049; C; 0069</c>. Both reach <c>STRING_FLD</c>, and the emitted
    /// literal carries <c>73</c> upstream where this port carries <c>105</c>.
    /// </para>
    /// <para>
    /// <b>So this test passes with S45 reverted, deliberately.</b> A test that failed on a revert
    /// would be asserting a behavioural change that did not happen. The bytecode half is pinned
    /// where it belongs, by <c>CompileParityTests</c>'s divergence list.
    /// </para>
    /// </remarks>
    [Test]
    public void The_full_fold_of_a_trailing_capital_I_still_reaches_the_ffi_ligature() =>
        FuzzyRegex.FullMatch("ﬃ", "(?fi)FFI").Success.Should().BeTrue();

    /// <summary>
    /// <c>[\w--a]</c> under <c>(?iV1)</c> - corpus row #485 - carries the expand-on-folding
    /// inventory, and U+0130 is in it. It therefore matches the two-codepoint form.
    /// </summary>
    /// <remarks>
    /// Upstream emits U+0130 into that set as the ONE codepoint 304, which is the defect stated
    /// plainly: an entry in the expansion inventory whose folding does not expand. This port emits
    /// the two it folds to, 105 and 775. Measured 2026-09-14 on regex 2026.9.10:
    /// <c>regex.compile(r'[\w--a]', regex.I | regex.F | regex.V1).fullmatch('i̇')</c> is None
    /// upstream and matches here. U+0130 itself matches on both sides and always did - it is a word
    /// character that is not <c>a</c>, so the base set holds it without the expansion branch - which
    /// is why the assertion below is on the two-codepoint form and not on it.
    /// </remarks>
    [Test]
    public void A_set_carrying_the_expansion_inventory_reaches_the_dotted_small()
    {
        FuzzyRegex.FullMatch(_dottedSmall, "(?fiV1)[\\w--a]").Success.Should().BeTrue();
        FuzzyRegex.FullMatch(_dottedCapital, "(?fiV1)[\\w--a]").Success.Should().BeTrue();
    }

    /// <summary>
    /// The ASCII encoding is untouched by any of this: <c>(?a)</c> folds only A-Z, so all four
    /// forms answer as they did before S45. This is the control that the change is confined to the
    /// Unicode encoding.
    /// </summary>
    /// <param name="pattern">The literal to case-fold.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Whether it matches.</param>
    [Test]
    [Arguments("I", "i", true)]
    [Arguments("I", "ı", false)]
    [Arguments("i", "İ", false)]
    [Arguments("İ", "İ", true)]
    [Arguments("ı", "ı", true)]
    public void The_ascii_encoding_folds_only_the_plain_pair(string pattern, string subject, bool expected) =>
        FuzzyRegex.FullMatch(subject, "(?ai)" + pattern).Success.Should().Be(expected);
}
