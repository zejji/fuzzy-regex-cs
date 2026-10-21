using System.Buffers;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// C6 option B3: a case-insensitive alternation whose literals hold a character with a case .NET's
/// ignore-case rule does not know, as <c>k</c> here also matches U+212A KELVIN SIGN, keeps the
/// ignore-case string screen and hands over to the code units near each such outlier in the text
/// (<c>PatternObject.LeadingOutliers</c>, <c>Matcher.NextLeadingStringPastOutliers</c>).
/// </summary>
public sealed class LeadingIgnoreCaseOutliersTests
{
    /// <summary>
    /// The outliers come from the case tables, not a list. Over ASCII letters under the Unicode
    /// encoding they are the Kelvin sign and long s, and nothing under the ASCII encoding.
    /// </summary>
    [Test]
    public void The_outliers_of_ascii_letters_are_the_kelvin_sign_and_long_s()
    {
        var unicode = new SortedSet<char>();
        var ascii = new SortedSet<char>();
        for (char ch = 'A'; ch <= 'z'; ch++)
        {
            if (!char.IsAsciiLetter(ch))
            {
                continue;
            }

            unicode.UnionWith(RequiredStringScreen.OrdinalIgnoreCaseOutliers(CaseEncoding.Unicode, ch)!);
            ascii.UnionWith(RequiredStringScreen.OrdinalIgnoreCaseOutliers(CaseEncoding.Ascii, ch)!);
        }

        using (new AssertionScope())
        {
            unicode.Should().Equal('ſ', 'K');
            ascii.Should().BeEmpty();
        }
    }

    /// <summary>Which patterns get outliers, and which.</summary>
    /// <param name="pattern">A pattern opening with an alternation of literals.</param>
    /// <param name="outliers">The outliers expected, or <see langword="null"/> for none.</param>
    [Test]
    [Arguments("(?i)kit|dog", "K")]
    [Arguments("(?i)sit|dog", "ſ")]
    [Arguments(@"(?i)\b(?:kits|dog)\b", "\u017F\u212A")]
    [Arguments(@"(?i)\b(?:kiss|dog)\b", "\u212A")]
    [Arguments("(?i)cat|dog", null)]
    [Arguments("(?ai)kit|sit", null)]
    [Arguments("(?-i:kit)|(?i:dog)", null)]
    public void A_set_with_a_kelvin_sign_or_long_s_case_gets_outliers(string pattern, string? outliers)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;

        using (new AssertionScope())
        {
            compiled.LeadingStrings.Should().NotBeNull();
            if (outliers is null)
            {
                compiled.LeadingOutliers.Should().BeNull();
                return;
            }

            SearchValues<char>? found = compiled.LeadingOutliers;
            found.Should().NotBeNull();
            for (int ch = 0; ch <= char.MaxValue && found is not null; ch++)
            {
                found.Contains((char)ch).Should().Be(outliers.Contains((char)ch), $"U+{ch:X4}");
            }
        }
    }

    /// <summary>
    /// Answers pinned to upstream around outliers: at the start and end of the text, next to a
    /// match the string search finds, inside a run of them, and with sharp s and its capital. The
    /// Turkish dotted and dotless i are left to the differential below: this port gives them the
    /// default case data, not upstream's (<c>TurkicDefaults</c>).
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Every match's span, <c>start-end</c>, space-separated.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer with regex.V1 unless (?V0), 2026-10-16")]
    [Arguments("(?i)kit|dog", "Kit dog", "0-3 4-7")]
    [Arguments("(?i)kit|dog", "dog KIT", "0-3 4-7")]
    [Arguments("(?i)kit|dog", "dogkitKitdog", "0-3 3-6 6-9 9-12")]
    [Arguments("(?i)kit|dog", "KKKitK", "2-5")]
    [Arguments("(?i)sit|dog", "ſIT DOG ſit", "0-3 4-7 8-11")]
    [Arguments(@"(?i)\b(?:kiss|dog)\b", "KiſS DOG KISS kiſſ", "0-4 5-8 9-13 14-18")]
    [Arguments("(?V0i)kit|aß", "AẞS KIT aß", "0-2 4-7 8-10")]
    public void An_alternation_with_outliers_finds_every_match(string pattern, string subject, string expected)
    {
        Spans(new FuzzyRegex(pattern), subject).Should().Be(expected);
    }

    /// <summary>
    /// A differential against the same alternation with an extra class arm, which no leading screen
    /// takes, over random text dense with outliers, their ASCII partners and Turkish i: the answers
    /// agree everywhere, including searches over a slice of the text.
    /// </summary>
    /// <param name="pattern">A pattern whose alternation has outliers.</param>
    [Test]
    [Arguments("(?i)(?:kit|sit|dog)")]
    [Arguments(@"(?i)\b(?:kiss|ski|dog)\b")]
    [Arguments(@"(?i)(?:sk\d|ks|tik)")]
    [Arguments("(?V0i)(?:kiss|ask|it)")]
    public void Answers_match_an_unscreened_control_over_text_dense_with_outliers(string pattern)
    {
        var screened = new FuzzyRegex(pattern);
        var control = new FuzzyRegex(pattern.Replace("(?:", "(?:[0-9]x|", StringComparison.Ordinal));
        const string alphabet = "kKsSiIt dogKſİıß1";
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(20261016);
        List<string> wrong = [];

        for (int row = 0; row < 3000; row++)
        {
            char[] chars = new char[random.Next(0, 40)];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[random.Next(alphabet.Length)];
            }

            string subject = new(chars);
            int begin = random.Next(0, subject.Length + 1);
            int length = random.Next(0, subject.Length - begin + 1);
            if (
                !string.Equals(Spans(screened, subject), Spans(control, subject), StringComparison.Ordinal)
                || !string.Equals(
                    Spans(screened, subject, begin, length),
                    Spans(control, subject, begin, length),
                    StringComparison.Ordinal
                )
            )
            {
                wrong.Add($"{subject} [{begin}, +{length}]");
            }
        }
#pragma warning restore CA5394, S2245

        using (new AssertionScope())
        {
            screened.PatternObject.LeadingOutliers.Should().NotBeNull();
            control.PatternObject.LeadingStrings.Should().BeNull();
            wrong.Should().BeEmpty();
        }
    }

    /// <summary>
    /// The rows the 2026-10-16 differential caught in the first build: a string hit inside the
    /// window before an outlier was taken before the units had searched the window, so a match
    /// starting earlier in it, holding the outlier, was skipped. Each answer is the unscreened
    /// control's; the first is also pinned.
    /// </summary>
    /// <param name="pattern">A pattern whose alternation has outliers.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="overlapped">Whether to find overlapping matches.</param>
    [Test]
    [Arguments("(?V0i)(?:Kabk|Ssdsa|kSbKkb|sb)", "  kkksBkKbſIbiooßIIoıKSo1SSiKaBk", false)]
    [Arguments("(?i)(?:KeiK|eKstkkt|teekSisd)s?", "KEiKIſ ẞıIa k-tTEekſiſdTeeKSiSdTeeKSIsd", true)]
    [Arguments("(?i)(?:Kbakd|ek|iektttK|ios|ktoesb|odak|ok)$", "IeKttTktoßsẞk 1ieKtttK", false)]
    public void A_string_hit_inside_an_outlier_window_waits_for_the_units(
        string pattern,
        string subject,
        bool overlapped
    )
    {
        var screened = new FuzzyRegex(pattern);
        var control = new FuzzyRegex(pattern.Replace("(?:", "(?:[0-9]x|", StringComparison.Ordinal));

        using (new AssertionScope())
        {
            screened.PatternObject.LeadingOutliers.Should().NotBeNull();
            control.PatternObject.LeadingStrings.Should().BeNull();
            Spans(screened, subject, overlapped: overlapped)
                .Should()
                .Be(Spans(control, subject, overlapped: overlapped));
            if (pattern.StartsWith("(?V0i)", StringComparison.Ordinal))
            {
                Spans(screened, subject).Should().Be("4-10 28-32");
            }
        }
    }

    /// <summary>
    /// The Turkish dotted capital I and dotless small i stay apart from <c>i</c> and <c>I</c> on
    /// both ignore-case string screens, with outliers and without: this port gives them the default
    /// case data, not upstream's (<c>docs/DIVERGENCES.md</c>, the Turkic row). Upstream finds
    /// <c>İt</c> here; the screen must not hide a match the engine would make, and the engine
    /// makes none.
    /// </summary>
    /// <param name="pattern">An alternation the ignore-case strings screen.</param>
    /// <param name="hasOutliers">Whether it gets outliers.</param>
    [Test]
    [Arguments("(?i)(?:it|xy)", false)]
    [Arguments("(?i)(?:it|ks)", true)]
    public void The_turkic_i_stays_unmatched_on_the_ignore_case_screen(string pattern, bool hasOutliers)
    {
        var screened = new FuzzyRegex(pattern);

        using (new AssertionScope())
        {
            screened.PatternObject.LeadingStrings.Should().NotBeNull();
            (screened.PatternObject.LeadingOutliers is not null).Should().Be(hasOutliers);
            Spans(screened, "İt").Should().BeEmpty();
            Spans(screened, "ıt").Should().BeEmpty();
            Spans(screened, "It iT").Should().Be("0-2 3-5");
        }
    }

    /// <summary>
    /// Every subject of up to six characters over <c>a</c>, <c>b</c>, <c>k</c>, the Kelvin sign,
    /// <c>s</c> and long s, searched whole, overlapped and over a slice that drops its first and last
    /// character: the screened pattern answers as the same alternation with an extra class arm,
    /// which no leading screen takes. The 2026-10-16 blind review ran this set against upstream as
    /// well (223,853 rows, no difference); here the control is the unscreened path.
    /// </summary>
    /// <param name="pattern">A pattern whose alternation has outliers.</param>
    [Test]
    [Arguments("(?i)(?:kab|sa|bk)")]
    [Arguments(@"(?i)\b(?:ks|sab)\b")]
    [Arguments("(?i)(?:kas|ab)(?=b)")]
    [Arguments("(?i)(?<=a)(?:sk|kba)")]
    [Arguments(@"(?i)(?:askb|kk|sa)\w")]
    [Arguments("(?V0i)(?:ssa|kab)")]
    [Arguments("(?i)(?:kbs|bas)s?")]
    [Arguments("(?i)(?:kabsk|sk)")]
    public void Every_short_subject_over_outliers_gets_the_unscreened_answer(string pattern)
    {
        var screened = new FuzzyRegex(pattern);
        var control = new FuzzyRegex(pattern.Replace("(?:", "(?:[0-9]x|", StringComparison.Ordinal));
        const string alphabet = "abkKsſ";
        List<string> wrong = [];
        int rows = 0;

        for (int length = 0; length <= 6; length++)
        {
            char[] chars = new char[length];
            int count = 1;
            for (int i = 0; i < length; i++)
            {
                count *= alphabet.Length;
            }

            for (int index = 0; index < count; index++)
            {
                int rest = index;
                for (int i = 0; i < length; i++)
                {
                    chars[i] = alphabet[rest % alphabet.Length];
                    rest /= alphabet.Length;
                }

                string subject = new(chars);
                int sliceLength = Math.Max(0, length - 2);
                rows++;
                if (
                    !string.Equals(Spans(screened, subject), Spans(control, subject), StringComparison.Ordinal)
                    || !string.Equals(
                        Spans(screened, subject, overlapped: true),
                        Spans(control, subject, overlapped: true),
                        StringComparison.Ordinal
                    )
                    || !string.Equals(
                        Spans(screened, subject, Math.Min(1, length), sliceLength),
                        Spans(control, subject, Math.Min(1, length), sliceLength),
                        StringComparison.Ordinal
                    )
                )
                {
                    wrong.Add(subject);
                }
            }
        }

        using (new AssertionScope())
        {
            screened.PatternObject.LeadingOutliers.Should().NotBeNull();
            control.PatternObject.LeadingStrings.Should().BeNull();
            rows.Should().Be(55_987);
            wrong.Should().BeEmpty();
        }
    }

    /// <summary>
    /// A caller that walks a long text in short slices, as <c>Match(text, i, 50)</c> in a loop,
    /// pays for each slice and not for the rest of the text each time: the search for the next
    /// outlier stops a literal's length past the slice. Before the fix it ran to the end of the
    /// text on every call (the 2026-10-16 blind review measured 399 ms against 6 ms for a list with
    /// no outliers, 4,000 calls over 4,000,000 units).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_search_over_a_short_slice_looks_for_outliers_only_near_it()
    {
        var regex = new FuzzyRegex("(?i)kestrel|thistle");
        string text = new('a', 1_000_000);
        const int calls = 4000;
        const int slice = 50;

        regex.PatternObject.LeadingOutliers.Should().NotBeNull();
        EngineWork.ShouldScanAtMostUnits(
            () =>
            {
                for (int call = 0; call < calls; call++)
                {
                    regex.Match(text, call * 250, slice).Success.Should().BeFalse();
                }
            },
            calls * (slice + 7L),
            "each call searches 50 units, and the outlier search needs only a literal's length (7) past them; 53 measured"
        );
    }

    /// <summary>
    /// Text dense with outliers that no unit stands near, as long s throughout for
    /// <c>(?i)tsx|kab</c>, takes one turn of the search per character, so the search polls the
    /// timeout as <c>SimpleStringSearch</c> does. The seam spends the budget on the second clock
    /// check: the first is the attempt's own, which the search runs after.
    /// </summary>
    [Test]
    public void The_search_past_outliers_honours_the_timeout()
    {
        var regex = new FuzzyRegex("(?i)tsx|kab") { ExpiresOnCheck = 2 };
        string text = new('ſ', 100_000);

        regex.PatternObject.LeadingOutliers.Should().NotBeNull();
        Action call = () => regex.Match(text, timeout: EngineWork.HangGuard);
        call.Should().Throw<RegexMatchTimeoutException>();
    }

    private static string Spans(
        FuzzyRegex regex,
        string subject,
        int beginning = 0,
        int length = -1,
        bool overlapped = false
    ) =>
        string.Join(
            ' ',
            regex.Matches(subject, beginning, length, overlapped).Select(static m => $"{m.Index}-{m.Index + m.Length}")
        );
}
