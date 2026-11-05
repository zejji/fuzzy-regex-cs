using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D144: an ordinal <see cref="FuzzyLiteralFilter"/> answers over any subject, not only an ASCII
/// one, so a no-match search reads the subject once for the pieces and not again to check it is
/// ASCII. See the class remarks on <see cref="FuzzyLiteralFilter"/>, "Encoding".
/// </summary>
/// <remarks>
/// <b>Provenance.</b> Every expected answer quoted beside a row is upstream's, recorded against
/// <c>regex 2026.9.29</c> on 2026-11-05 with indices converted to UTF-16 code units, and every row is
/// also checked against this port's engine with the filter taken off the pattern.
/// </remarks>
public sealed class FuzzyLiteralFilterEncodingTests
{
    private const string _astral = "\U0001F600";

    private static FuzzyLiteralFilter Filter(string source) =>
        PatternObject
            .Compile(
                PatternCompiler.Compile(
                    source,
                    0,
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    PatternCompiler.DefaultVersion
                ),
                source
            )
            .FuzzyLiteralFilter!;

    private static FuzzyRegex Unfiltered(string pattern)
    {
        var regex = new FuzzyRegex(pattern);
        regex.PatternObject.FuzzyLiteralFilter.Should().NotBeNull("the row is about the filter");
        regex.PatternObject.FuzzyLiteralFilter = null;
        return regex;
    }

    private static string Show(Match match) =>
        match.Success
            ? $"({match.Index},{match.Index + match.Length}) {match.FuzzyCounts.Substitutions},{match.FuzzyCounts.Insertions},{match.FuzzyCounts.Deletions}"
            : "None";

    private static string ShowAll(FuzzyRegex regex, string subject, int beginning = 0, int length = -1) =>
        string.Join(" ", regex.Matches(subject, beginning, length).Select(Show));

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("ééé café naïve ééé")]
    [Arguments(_astral + _astral + " zz " + _astral)]
    [Arguments("zz \ud800 zz \udc00 zz")]
    [Arguments("é" + _astral + "abxdxf\ud800")]
    public void An_ordinal_filter_refuses_a_subject_holding_no_piece_whatever_its_encoding(string subject)
    {
        // Before D144 the filter checked every subject for ASCII and switched itself off at the
        // first character that is not, so these answered CannotTell and the engine tried every
        // position. A match leaves a piece standing whatever the text around it holds.
        FuzzyLiteralFilter filter = Filter("(?:abcdef){e<=1}");
        filter.NextStart(subject, 0, subject.Length, filter.NewScanMemory()).Should().Be(FuzzyLiteralFilter.NoMatch);

        FuzzyLiteralFilter reverse = Filter("(?r)(?:abcdef){e<=1}");
        reverse.MayMatchBefore(subject, 0, subject.Length, reverse.NewScanMemory()).Should().BeFalse();

        new FuzzyRegex("(?:abcdef){e<=1}").Match(subject).Success.Should().BeFalse();
        Unfiltered("(?:abcdef){e<=1}").Match(subject).Success.Should().BeFalse();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void An_ignore_case_filter_still_cannot_tell_over_text_that_is_not_ascii()
    {
        // KELVIN SIGN equals 'k' ignoring case, and no ordinal case-insensitive search sees that,
        // so the case-insensitive filter keeps the full ASCII check.
        // regex.search(r'(?i)(?:kitten){e<=1}', 'Kitten') -> span=(0, 6) counts=(0, 0, 0)
        const string subject = "Kitten";
        FuzzyLiteralFilter filter = Filter("(?i)(?:kitten){e<=1}");
        filter.NextStart(subject, 0, subject.Length, filter.NewScanMemory()).Should().Be(FuzzyLiteralFilter.CannotTell);
        Show(new FuzzyRegex("(?i)(?:kitten){e<=1}").Match(subject)).Should().Be("(0,6) 0,0,0");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // An astral insertion before the piece found: 'def' at 5 is 3 + 1 characters but 3 + 2 units
    // after the match's start, so the bound must allow one more unit per error.
    [Arguments("(?:abcdef){e<=1}", "ab" + _astral + "cdef", 0)]
    // Text that is not ASCII but holds no surrogate: the plain bound, 'abc' at 5 less k.
    [Arguments("(?:abcdef){e<=1}", "zzéé abcdxf é", 4)]
    // A lone high surrogate is one character of one unit to the engine, but the filter does not
    // tell it from a pair, so it widens the bound.
    [Arguments("(?:abcdef){e<=1}", "ab\ud800cdef", 0)]
    public void The_start_bound_counts_a_surrogate_near_the_piece(string pattern, string subject, int expected)
    {
        FuzzyLiteralFilter filter = Filter(pattern);
        filter.NextStart(subject, 0, subject.Length, filter.NewScanMemory()).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "regex 2026.9.29 probe, 2026-11-05")]
    // regex.search(r'(?:abcdef){e<=1}', 'ab\U0001f600cdef') -> span=(0, 8) counts=(0, 1, 0)
    [Arguments("(?:abcdef){e<=1}", "ab" + _astral + "cdef", "(0,8) 0,1,0")]
    // regex.search(r'(?:abcdef){e<=1}', 'xx\U0001f600ab\U0001f600cdef') -> span=(4, 12) counts=(0, 1, 0)
    [Arguments("(?:abcdef){e<=1}", "xx" + _astral + "ab" + _astral + "cdef", "(4,12) 0,1,0")]
    // regex.search(r'(?:abcdefghi){e<=2}', '\U0001f600\U0001f600abcdefghi') -> span=(2, 13) counts=(1, 1, 0)
    [Arguments("(?:abcdefghi){e<=2}", _astral + _astral + "abcdefghi", "(2,13) 1,1,0")]
    // regex.search(r'(?:abcdefghi){e<=2}', 'a\U0001f600b\U0001f600cdefghi') -> span=(0, 13) counts=(0, 2, 0)
    [Arguments("(?:abcdefghi){e<=2}", "a" + _astral + "b" + _astral + "cdefghi", "(0,13) 0,2,0")]
    // regex.search(r'(?:abcdef){e<=1}', '\U0001f600bcdef') -> span=(0, 7) counts=(1, 0, 0)
    [Arguments("(?:abcdef){e<=1}", _astral + "bcdef", "(0,7) 1,0,0")]
    // regex.search(r'(?:abcdef){e<=1}', 'abc\U0001f600ef') -> span=(0, 7) counts=(1, 0, 0)
    [Arguments("(?:abcdef){e<=1}", "abc" + _astral + "ef", "(0,7) 1,0,0")]
    // regex.search(r'(?:abcdef){e<=1}', 'ab\ud800cdef') -> span=(0, 7) counts=(0, 1, 0)
    [Arguments("(?:abcdef){e<=1}", "ab\ud800cdef", "(0,7) 0,1,0")]
    // regex.search(r'(?:abcdef){e<=1}', 'ab\udc00\ud800cdef') -> None
    [Arguments("(?:abcdef){e<=1}", "ab\udc00\ud800cdef", "None")]
    // regex.search(r'(?:abcdef){e<=1}', 'zz\xe9\xe9 abcdxf \xe9') -> span=(5, 11) counts=(1, 0, 0)
    [Arguments("(?:abcdef){e<=1}", "zzéé abcdxf é", "(5,11) 1,0,0")]
    // regex.search(r'(?:abcdef|uvwxyz){e<=1}', '\U0001f600uv\U0001f600wxyz') -> span=(2, 10) counts=(0, 1, 0)
    [Arguments("(?:abcdef|uvwxyz){e<=1}", _astral + "uv" + _astral + "wxyz", "(2,10) 0,1,0")]
    // regex.search(r'(?r)(?:abcdef){e<=1}', 'ab\U0001f600cdef') -> span=(0, 8) counts=(0, 1, 0)
    [Arguments("(?r)(?:abcdef){e<=1}", "ab" + _astral + "cdef", "(0,8) 0,1,0")]
    public void A_fuzzy_literal_over_text_that_is_not_ascii_answers_as_upstream_does(
        string pattern,
        string subject,
        string expected
    )
    {
        Show(new FuzzyRegex(pattern).Match(subject)).Should().Be(expected);
        Show(Unfiltered(pattern).Match(subject)).Should().Be(expected);
    }

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "none - gap test")]
    public void A_no_match_search_reads_the_subject_once_per_piece_and_not_again_for_ascii()
    {
        // The WPR profile of 2026-11-04 (docs/plan/2026-11-04-phase11-profile.md): 17% of a no-match
        // IsMatch over a megabyte was Ascii.IsValid, a third pass after the two pieces' searches.
        // Two pieces, so the searches read 2n; the ASCII check made it 3n.
        string subject = string.Concat(Enumerable.Repeat("the quick brown fox jumps over the lazy dog ", 2400));
        var regex = new FuzzyRegex("(?:needle){e<=1}", FuzzyRegexOptions.None, EngineWork.HangGuard);
        EngineWork.ShouldSearchAtMostCharacters(
            () => regex.IsMatch(subject).Should().BeFalse(),
            (long)(2.2 * subject.Length),
            "an ordinal filter needs no ASCII pass"
        );
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Over_random_mixed_text_the_filter_never_changes_an_answer()
    {
        // Every answer with the filter is the answer without it: forward and reverse, ordinal and
        // case-insensitive, one literal and several, over text mixing ASCII, Latin-1, astral pairs
        // and lone surrogates around pieces of the literals. Rule 11: the grid counts the rows where
        // the D144 path answered over text that is not ASCII, and where a surrogate sat next to a
        // found piece, so it could have gone red.
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        string[] patterns =
        [
            "(?:abcdef){e<=1}",
            "(?:abcdefghi){e<=2}",
            "(?:abcdef|uvwxyz){e<=1}",
            "(?r)(?:abcdef){e<=1}",
            "(?i)(?:abcdef){e<=1}",
            "(?:abcdefghijkl){e<=3}",
            "(?:abcdefghijkl){i<=2,d<=1}",
        ];
        string[] chunks =
        [
            "abc",
            "def",
            "ghi",
            "jkl",
            "uvw",
            "xyz",
            "a",
            "b",
            "e",
            "z",
            " ",
            "é",
            "K",
            _astral,
            "\ud800",
            "\udc00",
            "abcdef",
        ];
        var random = new Random(20261105);
        int nonAscii = 0;
        int surrogateNearPiece = 0;
        int rows = 0;
        foreach (string pattern in patterns)
        {
            var filtered = new FuzzyRegex(pattern);
            FuzzyRegex unfiltered = Unfiltered(pattern);
            FuzzyLiteralFilter filter = Filter(pattern);
            for (int round = 0; round < 400; round++)
            {
                string subject = string.Concat(
                    Enumerable.Range(0, random.Next(1, 16)).Select(_ => chunks[random.Next(chunks.Length)])
                );
                int beginning = random.Next(subject.Length + 1);
                int length = random.Next(4) == 0 ? random.Next(subject.Length - beginning + 1) : -1;
                ShowAll(filtered, subject, beginning, length)
                    .Should()
                    .Be(
                        ShowAll(unfiltered, subject, beginning, length),
                        $"{pattern} over '{subject}' from {beginning}"
                    );
                rows++;

                if (!filter.Reverse && !System.Text.Ascii.IsValid(subject))
                {
                    int next = filter.NextStart(subject, 0, subject.Length, filter.NewScanMemory());
                    nonAscii += next == FuzzyLiteralFilter.CannotTell ? 0 : 1;
                    surrogateNearPiece +=
                        next >= 0
                        && subject
                            .AsSpan(next, Math.Min(subject.Length - next, 8))
                            .ContainsAnyInRange('\ud800', '\udfff')
                            ? 1
                            : 0;
                }
            }
        }

#pragma warning restore CA5394, S2245
        rows.Should().Be(patterns.Length * 400);
        nonAscii.Should().BeGreaterThan(500, "the ordinal rows answer over text that is not ASCII");
        surrogateNearPiece.Should().BeGreaterThan(50, "rows put a surrogate inside the bound's window");
    }
}
