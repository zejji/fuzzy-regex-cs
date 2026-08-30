namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// The subjects upstream's <c>test_fuzzy</c> reuses across several assertions.
/// </summary>
/// <remarks>
/// These are shared rather than copied per file because the expected spans depend on their exact
/// length - <c>#48</c> and <c>#50</c> assert 120 and 119 against <see cref="Hosts"/> - so a
/// one-character divergence between two copies would silently make two tests test different
/// things.
/// </remarks>
internal static class FuzzyTestData
{
    /// <summary>Upstream's first <c>text</c>, at line 2627.</summary>
    public const string Molasses = "molasses anaconda foo bar baz smith anderson ";

    /// <summary>Upstream's second <c>text</c>, at line 2638.</summary>
    public const string Anaconda = "anaconda foo bar baz smith anderson";

    /// <summary>The subject of the weighted-cost assertions, at line 2711.</summary>
    public const string Scattered = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";

    /// <summary>Upstream's third <c>text</c>, at lines 2723-2725. 120 characters.</summary>
    public const string Hosts =
        "www.cnn.com 64.236.16.20\nwww.slashdot.org 66.35.250.150\n"
        + "For useful information, use www.slashdot.org\nthis is demo data!\n";

    /// <summary>Upstream's <c>words="cat dog".split()</c> named list.</summary>
    public static readonly Dictionary<string, IReadOnlyCollection<string>> Words = new(StringComparer.Ordinal)
    {
        ["words"] = ["cat", "dog"],
    };
}
