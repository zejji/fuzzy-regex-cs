using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

// DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
/// <summary>
/// A scoped <c>(?a:...)</c> or <c>(?u:...)</c> answers exactly as the same encoding set for the
/// whole pattern, for every construct whose answer depends on the encoding.
/// </summary>
/// <remarks>
/// <para>
/// The rule is CPython's: a scoped encoding "switches to ASCII-only matching ... only in effect
/// for the narrow inline group" (re docs, 3.14.7, fetched 2026-09-25), and CPython 3.14.7 refuses
/// U+212A for <c>(?i)(?a:k)</c> exactly as for <c>(?ai)k</c>. Perl 5.42's <c>(?aa:...)</c>
/// answers the same way. Upstream contradicts itself: over the full grid of
/// <c>tools/probes/s91-scoped-encoding-grid.py</c> (40,672 scoped/global pairs) regex 2026.9.10
/// gives the two spellings different answers on 2,207, and before S91 this port did on 1,596.
/// </para>
/// <para>
/// Each case compares a scoped spelling with its global twin, so no expected value is written
/// down: the port's own global answer is the oracle, and the test holds for any answer the two
/// spellings share.
/// </para>
/// </remarks>
public sealed class ScopedEncodingGridTests
{
    private static readonly string[] _atoms =
    [
        "k",
        "s",
        "kx",
        "[a-z]",
        "[k]",
        "[^k]",
        "[^[^k]]",
        "[k-l]",
        "[[k]--[x]]",
        @"(k)\1",
        "(?:k|x)",
        "k+",
        "k{2}",
        "k?x",
        "(?>k+)",
        "(?:kx){e<=0}",
        "(?:kz){s<=1}",
        "(?:k){i<=1}x",
        "(?b)(?:kx){e<=1}",
        @"\L<w>",
        @"\mx",
        @"x\M",
        @"\bx",
        @"x\b",
        @"\Bx",
        ".",
        "x$",
        "(?m)^x",
        @"\X",
        @"\w",
        @"\W",
        @"\d",
        "ss",
        @"\xdf",
        @"(?=k)\w",
        "(?<=k)x",
        @"[\p{Lu}x]",
    ];

    private static readonly (string Scoped, string Global)[] _forms =
    [
        ("(?i)(?a:{X})", "(?ai){X}"),
        ("(?a:(?i:{X}))", "(?ai){X}"),
        ("(?a)(?i:{X})", "(?ai){X}"),
        ("(?ai)(?u:{X})", "(?iu){X}"),
        ("(?a)(?iu:{X})", "(?iu){X}"),
        ("(?a)(?u:{X})", "(?u){X}"),
        ("(?a:{X})", "(?a){X}"),
        ("(?w)(?a:{X})", "(?aw){X}"),
        ("(?aw)(?u:{X})", "(?uw){X}"),
        ("(?iw)(?a:{X})", "(?aiw){X}"),
        ("(?if)(?a:{X})", "(?aif){X}"),
        ("(?aif)(?u:{X})", "(?uif){X}"),
        ("(?r)(?i)(?a:{X})", "(?r)(?ai){X}"),
        ("(?r)(?ai)(?u:{X})", "(?r)(?iu){X}"),
        ("(?i)(?a:(?s:{X}))", "(?ai){X}"),
    ];

    private static readonly string[] _subjects =
    [
        "k",
        "K",
        "\u212A",
        "\u017F",
        "\u212Ax",
        "\u212A\u212A",
        "x\u00E9",
        "\u00E9x",
        "e\u0301",
        "x\u2028",
        "\u2028x",
        "\u00DF",
        "ss",
        "\uFF19",
        "k\u212A",
    ];

    private static readonly Dictionary<string, IReadOnlyCollection<string>> _lists = new() { ["w"] = ["k"] };

    /// <summary>Every scoped spelling answers as its global twin, in both versions.</summary>
    /// <param name="version">The version prefix.</param>
    [Test]
    [Arguments("(?V0)")]
    [Arguments("(?V1)")]
    public void A_scoped_encoding_answers_as_the_same_encoding_set_globally(string version)
    {
        List<string> disagreements = [];
        foreach (string atom in _atoms)
        {
            foreach ((string scoped, string global) in _forms)
            {
                FuzzyRegex s = Compile(version + scoped.Replace("{X}", atom, StringComparison.Ordinal));
                FuzzyRegex g = Compile(version + global.Replace("{X}", atom, StringComparison.Ordinal));
                foreach (string subject in _subjects)
                {
                    string a = Answer(s, subject);
                    string b = Answer(g, subject);
                    if (!string.Equals(a, b, StringComparison.Ordinal))
                    {
                        disagreements.Add($"{s} vs {g} over {Escape(subject)}: {a} / {b}");
                    }
                }
            }
        }

        disagreements.Should().BeEmpty();
    }

    private static FuzzyRegex Compile(string pattern) =>
        new(
            pattern,
            FuzzyRegexOptions.None,
            TimeSpan.FromSeconds(5),
            pattern.Contains("L<w>", StringComparison.Ordinal) ? _lists : null
        );

    private static string Answer(FuzzyRegex regex, string subject)
    {
        Match m = regex.Match(subject);

        return m.Success ? $"({m.Index},{m.Index + m.Length})" : "none";
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(static c => c < 0x80 ? c.ToString() : $"\\u{(int)c:X4}"));
}
