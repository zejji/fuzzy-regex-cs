using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_case_folding</c>
/// (lines 584-670), the full case-folding (<c>(?fi)</c>) and Unicode default (<c>(?iV1)</c>
/// ligature) assertions. The fuzzy-matching findall assertions from the same method live in
/// <see cref="FuzzyBoundaryFindallTests"/>, and the <c>\L&lt;options&gt;</c> named-list
/// assertions are not ported (see the trailing comment).
/// </summary>
/// <remarks>
/// Named characters that appear in a SUBJECT string (not a pattern) were resolved to their actual
/// codepoints via the local oracle 2026-08-29 (<c>unicodedata.lookup</c>): LATIN SMALL LETTER SHARP
/// S is U+00DF (ß), LATIN SMALL LIGATURE ST is U+FB06 (ﬆ), LATIN SMALL LIGATURE LONG S T is U+FB05
/// (ﬅ), LATIN SMALL LIGATURE FFI is U+FB03 (ﬃ), LATIN SMALL LIGATURE FF is U+FB00 (ﬀ), and LATIN
/// SMALL LIGATURE FI is U+FB01 (ﬁ). Where a name appears inside a PATTERN it is kept as the literal
/// <c>\N{...}</c> escape, since our parser must support that form directly. Every span assertion
/// here was also cross-checked against the local oracle 2026-08-29 and matched upstream verbatim.
/// </remarks>
public sealed class CaseFoldingTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#1")]
    public void Lower_ss_full_folds_to_upper_SS()
    {
        Match m = Upstream.Match("SS", "(?fi)ss");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#2")]
    public void Upper_SS_full_folds_to_lower_ss()
    {
        Match m = Upstream.Match("ss", "(?fi)SS");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#3")]
    public void Upper_SS_full_folds_to_sharp_s()
    {
        Match m = Upstream.Match("ß", "(?fi)SS");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#4")]
    public void Sharp_s_pattern_full_folds_to_upper_SS()
    {
        Match m = Upstream.Match("SS", @"(?fi)\N{LATIN SMALL LETTER SHARP S}");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#5")]
    public void Ligature_st_pattern_full_folds_to_ST()
    {
        Match m = Upstream.Match("ST", @"(?fi)\N{LATIN SMALL LIGATURE ST}");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#6")]
    public void Upper_ST_full_folds_to_ligature_st()
    {
        Match m = Upstream.Match("ﬆ", "(?fi)ST");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#7")]
    public void Upper_ST_full_folds_to_ligature_long_s_t()
    {
        Match m = Upstream.Match("ﬅ", "(?fi)ST");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#8")]
    public void Upper_SST_full_folds_to_sharp_s_plus_t()
    {
        Match m = Upstream.Match("ßt", "(?fi)SST");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#9")]
    public void Upper_SST_full_folds_to_s_plus_ligature_long_s_t()
    {
        Match m = Upstream.Match("sﬅ", "(?fi)SST");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    // Upstream's assertion 12 repeats assertion 10 exactly (same pattern and subject); folded here.
    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#10,12")]
    public void Upper_SST_full_folds_to_s_plus_ligature_st()
    {
        Match m = Upstream.Match("sﬆ", "(?fi)SST");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#11")]
    public void Ligature_st_pattern_full_folds_within_upper_SST()
    {
        Match m = Upstream.Match("SST", @"(?fi)\N{LATIN SMALL LIGATURE ST}");

        (m.Index, m.Index + m.Length).Should().Be((1, 3));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#13")]
    public void Upper_FFI_full_folds_to_ligature_ffi()
    {
        Match m = Upstream.Match("ﬃ", "(?fi)FFI");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#14")]
    public void Upper_FFI_full_folds_to_ligature_ff_plus_i()
    {
        Match m = Upstream.Match("ﬀi", "(?fi)FFI");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#15")]
    public void Upper_FFI_full_folds_to_f_plus_ligature_fi()
    {
        Match m = Upstream.Match("fﬁ", "(?fi)FFI");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#16")]
    public void Ligature_ffi_pattern_full_folds_to_upper_FFI()
    {
        Match m = Upstream.Match("FFI", @"(?fi)\N{LATIN SMALL LIGATURE FFI}");

        (m.Index, m.Index + m.Length).Should().Be((0, 3));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#17")]
    public void Ligature_ff_pattern_plus_i_full_folds_to_upper_FFI()
    {
        Match m = Upstream.Match("FFI", @"(?fi)\N{LATIN SMALL LIGATURE FF}i");

        (m.Index, m.Index + m.Length).Should().Be((0, 3));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#18")]
    public void F_plus_ligature_fi_pattern_full_folds_to_upper_FFI()
    {
        Match m = Upstream.Match("FFI", @"(?fi)f\N{LATIN SMALL LIGATURE FI}");

        (m.Index, m.Index + m.Length).Should().Be((0, 3));
    }

    // Upstream's nested for-ch1/for-ch2 loop over sigma ("Σσς") is unrolled into its 9
    // combinations.
    [Test]
    [Arguments("Σ", "Σ")]
    [Arguments("Σ", "σ")]
    [Arguments("Σ", "ς")]
    [Arguments("σ", "Σ")]
    [Arguments("σ", "σ")]
    [Arguments("σ", "ς")]
    [Arguments("ς", "Σ")]
    [Arguments("ς", "σ")]
    [Arguments("ς", "ς")]
    [Property("Upstream", "RegexTests.test_case_folding#19")]
    public void Every_sigma_form_full_folds_to_every_other_sigma_form(string ch1, string ch2) =>
        Upstream.MatchAtStart(ch2, "(?fi)" + ch1).Success.Should().BeTrue();

    // Upstream repeats several of these six (?iV1) checks verbatim later in the method (its own
    // assertions 26-29 and 31); folded into their first occurrence.
    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#20,26")]
    public void V1_ignore_case_ff_matches_ligature_ff_then_fi() =>
        Upstream.Match("ﬀﬁ", "(?iV1)ff").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#21")]
    public void V1_ignore_case_ff_matches_ligature_fi_then_ff() =>
        Upstream.Match("ﬁﬀ", "(?iV1)ff").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#22,27")]
    public void V1_ignore_case_fi_matches_ligature_ff_then_fi() =>
        Upstream.Match("ﬀﬁ", "(?iV1)fi").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#23")]
    public void V1_ignore_case_fi_matches_ligature_fi_then_ff() =>
        Upstream.Match("ﬁﬀ", "(?iV1)fi").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#24,28")]
    public void V1_ignore_case_fffi_matches_ligature_ff_then_fi() =>
        Upstream.Match("ﬀﬁ", "(?iV1)fffi").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#25,29")]
    public void V1_ignore_case_f_plus_ligature_ffi_matches_ligature_ff_then_fi() =>
        Upstream.Match("ﬀﬁ", "(?iV1)fﬃ").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#30,31")]
    public void V1_ignore_case_f_plus_ligature_fi_matches_ligature_ff_then_i() =>
        Upstream.Match("ﬀi", "(?iV1)fﬁ").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#34")]
    public void Ligature_ffi_full_folds_inside_a_longer_word()
    {
        Match m = Upstream.Match("  affine  ", @"(?fi)a\N{LATIN SMALL LIGATURE FFI}ne");

        (m.Index, m.Index + m.Length).Should().Be((2, 8));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#35")]
    public void Ligature_ffi_full_folds_inside_an_alternation()
    {
        Match m = Upstream.Match("  affine  ", @"(?fi)a(?:\N{LATIN SMALL LIGATURE FFI}|x)ne");

        (m.Index, m.Index + m.Length).Should().Be((2, 8));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_case_folding#36")]
    public void Ligature_ffi_full_folds_inside_a_multi_char_alternation()
    {
        Match m = Upstream.Match("  affine  ", @"(?fi)a(?:\N{LATIN SMALL LIGATURE FFI}|xy)ne");

        (m.Index, m.Index + m.Length).Should().Be((2, 8));
    }

    // NOT PORTED: assertions 37-38 use `options=[...]`, the \L<name> named-list feature, which has
    // no counterpart on this API yet (deferred, per docs/PORTMAP.md).
}
