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
    /// A RANGE reaches the dotless small upstream without ever spelling an <c>I</c> - that is what
    /// <c>0049; T; 0131</c> buys, and it is the shape the grid above cannot show, because every cell
    /// of it is one literal against one subject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured 2026-09-15 on regex 2026.9.10, <c>tools/probes/upstream-turkic-without-spans.py</c>:
    /// upstream matches U+0131 with <c>[A-Z]</c> and with <c>[A-Y]</c> and matches nothing with
    /// <c>[A-H]</c> or <c>[J-Z]</c>, so it is the <c>I</c> INSIDE the range and not the range's
    /// breadth. This port matches none of the four, which is what PCRE2 and .NET do.
    /// </para>
    /// <para>
    /// It is pinned because the wave's own classifier turns on it: <c>turkic-default-folding</c>
    /// accepts a row whose pattern spells no Turkic letter at all as long as it carries a construct
    /// that can fold into one, and a range is the commonest such construct. Row 29165 of the seed-7
    /// 2000-row wave of 2026-09-15 is exactly this shape.
    /// </para>
    /// </remarks>
    /// <param name="cls">The class.</param>
    [Test]
    [Arguments("[A-Z]")]
    [Arguments("[A-Y]")]
    [Arguments("[A-H]")]
    [Arguments("[J-Z]")]
    public void A_range_spanning_the_plain_I_does_not_reach_the_dotless_small(string cls) =>
        FuzzyRegex.FullMatch(_dotlessSmall, "(?i)" + cls).Success.Should().BeFalse();

    /// <summary>
    /// The three answers that carry NO match position - a replacement, a split and a template that
    /// throws - diverge here too, and each is pinned because the oracle needed a second fact from
    /// upstream before it could classify one.
    /// </summary>
    /// <remarks>
    /// Upstream's answers, measured 2026-09-15 on regex 2026.9.10 by
    /// <c>tools/probes/upstream-turkic-without-spans.py</c>: <c>subn('(?i)I', 'X', 'ı')</c> is
    /// <c>('X', 1)</c>, <c>split('(?i)(I)', 'aıb')</c> is <c>['a', 'ı', 'b']</c>, and
    /// <c>subfn('(?i)I', '{1}', 'ı')</c> raises <c>IndexError</c> - it finds the match by the
    /// <c>T</c> row and only then discovers the template names a group that does not exist. This
    /// port matches nothing on any of the three, so it replaces nothing, splits nothing and has no
    /// match to expand a template against.
    /// </remarks>
    [Test]
    public void An_answer_that_carries_no_span_diverges_on_the_dotless_small_as_well()
    {
        FuzzyRegex.Replace(_dotlessSmall, "(?i)I", "X").Should().Be(_dotlessSmall);
        FuzzyRegex.Split("a" + _dotlessSmall + "b", "(?i)(I)").Should().Equal("a" + _dotlessSmall + "b");
        // No match, so the template is never expanded and the bad group reference never bites.
        FuzzyRegex.ReplaceFormat(_dotlessSmall, "(?i)I", "{1}").Should().Be(_dotlessSmall);
    }

    /// <summary>
    /// A SET UNION reaches the case partners of its members - including partners no member reaches
    /// on its own - but it does not reach them through a Turkic <c>T</c> row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mechanism of seed 20260915 row 88716 of the 6000-row gate, isolated to one line. That row
    /// needed a fourth Turkic oracle entry because the letter is read by a negative LOOKBEHIND, so
    /// no match span on either side covers it; what the lookbehind holds is
    /// <c>[a\p{ASCII}]</c>, and <b>the union reaches U+0131 where neither <c>a</c> nor
    /// <c>\p{ASCII}</c> does</b>, because a set is expanded by its members' case partners and
    /// <c>I</c> is ASCII.
    /// </para>
    /// <para>
    /// <b>What switches the expansion on is a SECOND MEMBER, which was measured after a first
    /// draft got it wrong.</b> The draft said a set is expanded by its members' case partners;
    /// <c>[\p{ASCII}]</c> refutes that, because its member holds <c>I</c> and it reaches nothing.
    /// <c>[\p{ASCII}\p{ASCII}]</c> - the same member twice, so exactly the same characters - DOES
    /// reach U+0131. A one-member set behaves like the bare property; a two-member one
    /// case-expands the property's contents. <c>[ab]</c> reaching nothing says it is the
    /// property's members expanding rather than sets in general.
    /// </para>
    /// <para>
    /// <b>The expansion itself is not the defect, and that is what this test separates.</b> A
    /// multi-member set holding <c>\p{ASCII}</c> reaches every character whose partner is ASCII,
    /// and this port agrees on the ordinary <c>C</c> rows - U+212A KELVIN SIGN and U+017F LATIN
    /// SMALL LETTER LONG S - while refusing the two <c>T</c> rows. A test that only asserted the
    /// refusals would pass on an engine that had lost set expansion altogether.
    /// </para>
    /// <para>
    /// <b>Provenance of the expected values.</b> Upstream's answers, measured 2026-09-15 on regex
    /// 2026.9.10 by <c>python tools/probes/upstream-turkic-without-spans.py</c>, in the section
    /// headed "the MECHANISM of the lookaround row": a multi-member set holding <c>\p{ASCII}</c>
    /// matches U+0131, U+0130, U+212A and U+017F and answers None to U+00C5 and U+00F1, while
    /// <c>a</c>, <c>[ab]</c>, <c>\p{ASCII}</c> and <c>[\p{ASCII}]</c> answer None to all six. The
    /// port-side half was measured the same day with <c>pwsh -File tools/run-oracle.ps1 -Rows</c>
    /// over the 42-cell grid of those six characters against seven spellings: <b>36 cells AGREE,
    /// and the only six that diverge are U+0131 and U+0130 against the three multi-member
    /// spellings.</b>
    /// </para>
    /// </remarks>
    [Test]
    public void A_set_union_reaches_the_case_partners_of_its_members_but_not_through_a_Turkic_row()
    {
        const string union = @"[a\p{ASCII}]";
        const FuzzyRegexOptions fold = FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase;

        // The two ordinary `C` rows, where upstream and this port agree: the union reaches a
        // character neither of its members reaches, and that expansion is correct.
        FuzzyRegex.MatchAtStart("K", union, fold).Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart("ſ", union, fold).Success.Should().BeTrue();

        // The two `T` rows, which upstream reaches through the same expansion and this port does
        // not. This is the whole of the divergence on row 88716.
        FuzzyRegex.MatchAtStart(_dotlessSmall, union, fold).Success.Should().BeFalse();
        FuzzyRegex.MatchAtStart("İ", union, fold).Success.Should().BeFalse();

        // Partners that are not ASCII are out of reach for both engines, so the union is not simply
        // matching every cased letter.
        FuzzyRegex.MatchAtStart("Å", union, fold).Success.Should().BeFalse();
        FuzzyRegex.MatchAtStart("ñ", union, fold).Success.Should().BeFalse();

        // And no ONE-MEMBER spelling reaches any of them, on either engine. `[\p{ASCII}]` is the
        // cell that kills the "a set expands its members" reading: same member, same characters,
        // and nothing reached.
        foreach (string alone in (string[])[@"a", @"[a]", @"[ab]", @"\p{ASCII}", @"[\p{ASCII}]"])
        {
            foreach (string subject in (string[])["K", "ſ", _dotlessSmall, "İ"])
            {
                FuzzyRegex.MatchAtStart(subject, alone, fold).Success.Should().BeFalse();
            }
        }

        // A SECOND member is the whole of the switch - the same property twice is enough, so it is
        // not the other member contributing anything.
        FuzzyRegex.MatchAtStart("K", @"[\p{ASCII}\p{ASCII}]", fold).Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart("K", @"[\p{ASCII}z]", fold).Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart(_dotlessSmall, @"[\p{ASCII}\p{ASCII}]", fold).Success.Should().BeFalse();
        FuzzyRegex.MatchAtStart(_dotlessSmall, @"[\p{ASCII}z]", fold).Success.Should().BeFalse();
    }

    /// <summary>
    /// A LOOKBEHIND that spells a dotless small i does not reach a plain <c>I</c> here, where
    /// upstream's does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is NOT the minimal form of gate row 88716, and an earlier version of this comment
    /// said it was.</b> The blind review killed that claim by measuring: row 88716's letter is
    /// read by the set union in its NEGATIVE lookbehind, which the test above isolates, and
    /// neutering row 88716's inner <c>(?&lt;=ı[\w\s])</c> leaves the engines disagreeing. What this
    /// test pins is a separate and narrower fact - that a lookbehind is not itself a way round the
    /// Turkic refusal - which is worth keeping because the oracle entry it sits beside is about a
    /// letter read inside a lookaround.
    /// </para>
    /// <para>
    /// <b>Provenance of the expected values.</b> Measured 2026-09-15 on regex 2026.9.10 by
    /// <c>python tools/probes/upstream-turkic-without-spans.py</c>, in the section headed "a
    /// lookbehind is not itself a way round the refusal", which is exactly the four calls below:
    /// <c>search('(?i)(?&lt;=ı)ı', 'Iı')</c> is <c>(1, 2)</c> upstream, <c>'ıı'</c> is
    /// <c>(1, 2)</c>, and <c>'iı'</c> and <c>'İı'</c> are both <c>None</c>. So it is the plain
    /// capital <c>I</c> that pairs - <c>0049; T; 0131</c> and that row alone - and not case
    /// folding in general.
    /// </para>
    /// <para>
    /// <b>Why the swap letters are all non-ASCII, said out loud because the obvious control is
    /// wrong.</b> The full pattern's <c>(?&lt;!(?:a|\p{ASCII})+)</c> reads ASCII-ness, so swapping
    /// the dotless i for <c>h</c> or <c>i</c> changes the question rather than removing the
    /// <c>T</c> row, and both of those reproduce upstream's count. A first draft of this judgement
    /// used <c>h</c> and read the agreement as "not Turkic after all".
    /// </para>
    /// </remarks>
    [Test]
    public void A_lookbehind_that_reads_a_dotless_small_i_does_not_reach_the_plain_I()
    {
        // The lookbehind spells the DOTLESS small i; the subject offers the plain capital I, which
        // upstream pairs with it through `0049; T; 0131` and this port does not.
        FuzzyRegex
            .Match("I" + _dotlessSmall, "(?i)(?<=" + _dotlessSmall + ")" + _dotlessSmall)
            .Success.Should()
            .BeFalse();

        // The same lookbehind over the letter it actually spells still matches, so the test is about
        // the pairing and not about lookbehinds. Upstream agrees here: (1, 2) on both engines.
        FuzzyRegex
            .Match(_dotlessSmall + _dotlessSmall, "(?i)(?<=" + _dotlessSmall + ")" + _dotlessSmall)
            .Success.Should()
            .BeTrue();

        // And it is the PLAIN CAPITAL I that pairs, not case folding in general: upstream answers
        // None to both of these too, so the two engines agree on them.
        FuzzyRegex
            .Match("i" + _dotlessSmall, "(?i)(?<=" + _dotlessSmall + ")" + _dotlessSmall)
            .Success.Should()
            .BeFalse();
        FuzzyRegex
            .Match("İ" + _dotlessSmall, "(?i)(?<=" + _dotlessSmall + ")" + _dotlessSmall)
            .Success.Should()
            .BeFalse();
    }

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

    /// <summary>
    /// A leading literal whose full fold is longer than one character loses the zero-width partial at
    /// the end of an empty slice, and the dotted capital is one of those letters here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 34508 of the seed-20260915 2000-row wave of commit <c>407c0cb</c>, and the shape where the
    /// <c>T</c> rows are visible with NO Turkic letter in the subject at all - the U+0130 is the
    /// pattern's own leading literal. Under <c>(?fiV1)</c> such a literal compiles to a folded STRING,
    /// and neither engine reports a partial for one when the slice it would start in is empty:
    /// U+00DF (<c>ss</c>), U+FB00 (<c>ff</c>) and U+01F0 (<c>j</c> + U+030C) all answer None on both
    /// sides. Upstream answers the partial for U+0130 alone, because <c>0130; T; 0069</c> makes its
    /// fold a single <c>i</c>, which is what <c>h</c>, <c>i</c> and U+0131 - the one-character folds -
    /// answer on both sides too.
    /// </para>
    /// <para>
    /// <b>Upstream's own <c>match</c> answers it, not only its <c>search</c></b>, so this is its slow
    /// path and not the <c>search_start</c> prefilter the entry <c>search-start-partial</c> covers:
    /// <c>search_start</c> is called only when searching (<c>upstream/src/_regex.c:11816</c>), and
    /// the row's recorded <c>searchOnlyPartial</c> is <see langword="false"/> for exactly that reason.
    /// Measured 2026-09-15 on regex 2026.9.10 by
    /// <c>tools/probes/upstream-turkic-from-the-pattern-side.py</c> and its port half
    /// <c>tools/probes/port-turkic-from-the-pattern-side.ps1</c>, which print the whole grid.
    /// </para>
    /// </remarks>
    /// <param name="literal">The pattern's leading literal.</param>
    /// <param name="partialExpected">Whether a zero-width partial is reported.</param>
    [Test]
    // Folds longer than one character: no partial. upstream agrees on all three.
    [Arguments("İ", false)]
    [Arguments("ß", false)]
    [Arguments("ﬀ", false)]
    [Arguments("ǰ", false)]
    // Folds of exactly one character: the partial. upstream agrees on all three.
    [Arguments("h", true)]
    [Arguments("i", true)]
    [Arguments("ı", true)]
    public void A_leading_literal_that_folds_longer_than_itself_reports_no_partial_on_an_empty_slice(
        string literal,
        bool partialExpected
    )
    {
        // The row's own flags, 0x410A: FULLCASE, VERSION1, MULTILINE, IGNORECASE. The subject is the
        // row's; the slice is empty at its end, so only the pattern decides the answer.
        Match m = new FuzzyRegex(
            "^" + literal + @"\K\b",
            FuzzyRegexOptions.FullCase
                | FuzzyRegexOptions.Version1
                | FuzzyRegexOptions.Multiline
                | FuzzyRegexOptions.IgnoreCase
        ).MatchAtStart("sﬁﬀıİ", 5, 0, partial: true);

        m.Success.Should().Be(partialExpected);
        if (partialExpected)
        {
            (m.Index, m.Length).Should().Be((5, 0));
            m.PartialMatch.Should().BeTrue();
        }
    }

    /// <summary>
    /// A <c>\L&lt;name&gt;</c> word beginning with the dotted capital costs what a word beginning with
    /// any other expanding fold costs, and not what a one-character fold costs.
    /// </summary>
    /// <remarks>
    /// Row 25482 of the seed-20260915 2000-row wave of commit <c>407c0cb</c>: the Turkic letter is in
    /// neither the subject nor the pattern text but in a named list, which is the third place the
    /// entry <c>turkic-default-folding</c>'s span test cannot look. The fuzzy section reaches the
    /// two-codepoint fold one codepoint sooner, so the first match it finds ends at 2 having spent one
    /// substitution; a word whose first letter folds to a single character ends at 3 having spent two.
    /// Upstream answers the SECOND for U+0130 and the first for U+00DF, U+FB00 and U+01F0, which is
    /// <c>0130; T; 0069</c> and nothing else - measured 2026-09-15 on regex 2026.9.10 by
    /// <c>tools/probes/upstream-turkic-from-the-pattern-side.py</c>.
    /// </remarks>
    /// <param name="first">The list word's first letter.</param>
    /// <param name="end">Where the zero-width answer lands.</param>
    /// <param name="substitutions">What it cost to get there.</param>
    [Test]
    // Folds longer than one character. upstream answers this for all but U+0130.
    [Arguments("İ", 2, 1)]
    [Arguments("ß", 2, 1)]
    [Arguments("ﬀ", 2, 1)]
    [Arguments("ǰ", 2, 1)]
    // Folds of exactly one character, which is what upstream makes U+0130.
    [Arguments("h", 3, 2)]
    [Arguments("i", 3, 2)]
    public void A_named_list_word_starting_on_an_expanding_fold_costs_one_substitution_not_two(
        string first,
        int end,
        int substitutions
    )
    {
        Dictionary<string, IReadOnlyCollection<string>> lists = new(StringComparer.Ordinal)
        {
            ["w1"] = [first + "ı", "ﬀ"],
        };

        // The row's own flags, 0x4102: FULLCASE, VERSION1, IGNORECASE.
        Match m = new FuzzyRegex(
            @"(?(?=\D)[\p{L}||\p{N}])\L<w1>{e<=2}\K",
            FuzzyRegexOptions.FullCase | FuzzyRegexOptions.Version1 | FuzzyRegexOptions.IgnoreCase,
            lists
        ).MatchAtStart("ﬀ\r ﬀ", partial: true);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((end, 0));
        m.FuzzyCounts.Substitutions.Should().Be(substitutions);
    }
}
