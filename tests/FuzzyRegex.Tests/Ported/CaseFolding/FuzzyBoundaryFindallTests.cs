using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_case_folding</c>
/// (lines 654-659): the two fuzzy-quantifier findall assertions, one under each of V0 and V1
/// behaviour. The rest of that method lives in <see cref="CaseFoldingTests"/>.
/// </summary>
public sealed class FuzzyBoundaryFindallTests
{
    private const string _subject = "word word2 word word3 word word234 word23 word";
    private const string _pattern = @"\m(?:word){e<=3}\M(?<!\m(?:word){e<=1}\M)";

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy quantifiers yet")]
    [Property("Upstream", "RegexTests.test_case_folding#32")]
    public void V0_fuzzy_word_boundary_findall_skips_near_exact_matches() =>
        FuzzyRegex.Matches(_subject, "(?iV0)" + _pattern).Select(m => m.Value).Should().Equal("word234", "word23");

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy quantifiers yet")]
    [Property("Upstream", "RegexTests.test_case_folding#33")]
    public void V1_fuzzy_word_boundary_findall_skips_near_exact_matches() =>
        FuzzyRegex.Matches(_subject, "(?iV1)" + _pattern).Select(m => m.Value).Should().Equal("word234", "word23");
}
