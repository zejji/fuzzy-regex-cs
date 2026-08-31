using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_named_lists</c>
/// (lines 2604-2607).
/// </summary>
/// <remarks>
/// <para>
/// These two assertions sit inside <c>test_named_lists</c> but use no named list: they check that
/// a backreference under <c>(?f)</c> full case-folding matches a form of a different length, in
/// both directions. They are here rather than in <c>Ported/NamedLists/</c> so that the status
/// board attributes them to case folding, which is what they actually exercise; the provenance
/// still names the upstream method they came from.
/// </para>
/// <para>
/// Both subjects are 16 UTF-16 code units and hold nothing above U+FFFF, so upstream's codepoint
/// span <c>(1, 15)</c> is also the UTF-16 span. Read back from the local Python oracle on
/// 2026-08-30.
/// </para>
/// </remarks>
public sealed class FullCaseBackreferenceTests
{
    // Written with an explicit code-unit cast rather than an escape or literal UTF-8, because a
    // precomposed and a decomposed form are indistinguishable by eye in a source file.
    private static readonly string _strasseLower = "stra" + (char)0x00DF + "e"; // straße

    [Test]
    [Property("Upstream", "RegexTests.test_named_lists#15-16")]
    public void A_backreference_under_full_case_folding_matches_a_different_length_form()
    {
        foreach (string subject in new[] { " " + _strasseLower + " STRASSE ", " STRASSE " + _strasseLower + " " })
        {
            Match m = FuzzyRegex.Match(subject, "(?fi)\\b(\\w+) +\\1\\b");

            m.Success.Should().BeTrue();
            (m.Index, m.Index + m.Length).Should().Be((1, 15));
        }
    }
}
