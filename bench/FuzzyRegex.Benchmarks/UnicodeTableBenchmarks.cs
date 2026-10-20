using System.Globalization;
using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// Four rows that read the generated Unicode tables (<c>src/FuzzyRegex/Unicode/*.g.cs</c>) over a
/// mixed-script subject, so a table regeneration (an upstream sync to a new Unicode version) can be
/// timed before and after on rows that actually touch them.
/// </summary>
/// <remarks>
/// <para>
/// The rest of the suite has two case-fold rows and no grapheme or property rows, so a regenerated
/// table had nothing to move. <c>GraphemeScan</c> walks the extended-grapheme-cluster rules and so
/// the Grapheme_Cluster_Break, Extended_Pictographic and Indic_Conjunct_Break tables;
/// <c>UnicodePropertyScan</c> and <c>WordScanUnicode</c> read the General_Category and word
/// tables; <c>CaseFoldScanUnicode</c> reads the full case-fold table.
/// </para>
/// <para>
/// The subject is deterministic and about 64 KB of UTF-16, in segments cycled in a fixed order:
/// Latin with combining marks, Devanagari conjuncts (including consonant, virama, consonant),
/// emoji ZWJ sequences, flags and skin-tone modifiers, Hangul (precomposed and as conjoining
/// jamo) and Greek. It is written as code points, not literal characters, so no editor or tool can
/// normalise it. Every benchmark returns a count, so a changed answer between two trees is visible
/// and the work cannot be discarded; <see cref="AnswerDetails"/> prints each row's spans for a
/// stronger check.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class UnicodeTableBenchmarks
{
    /// <summary>Roughly how many UTF-16 units the subject is built to.</summary>
    private const int _size = 64 * 1024;

    /// <summary>One extended grapheme cluster per match, whatever the script.</summary>
    private static readonly FuzzyRegex _grapheme = new(@"\X");

    /// <summary>Runs of letters in any script, which reads the General_Category table.</summary>
    private static readonly FuzzyRegex _letters = new(@"\p{L}+");

    /// <summary>Runs of word characters in any script.</summary>
    private static readonly FuzzyRegex _words = new(@"\w+");

    /// <summary>
    /// A literal alternation under full case folding: <c>strasse</c> meets the corpus's sharp s,
    /// <c>final</c> its fi ligature, the accented word its capital, and the Greek word its capitals
    /// and final sigma. Every member folds to something other than itself.
    /// </summary>
    private static readonly FuzzyRegex _caseFolded = new(
        "strasse|" + Cp("00E9") + "cole|" + Cp("03C3 03AF 03C3 03C5 03C6 03BF 03C2") + "|final",
        FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase
    );

    /// <summary>The mixed-script subject, built once.</summary>
    private static readonly string _subject = BuildSubject();

    /// <summary>Counts every extended grapheme cluster.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int GraphemeScan() => _grapheme.Count(_subject);

    /// <summary>Counts every run of letters.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int UnicodePropertyScan() => _letters.Count(_subject);

    /// <summary>Counts every run of word characters.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int WordScanUnicode() => _words.Count(_subject);

    /// <summary>Counts every case-folded match of the alternation.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int CaseFoldScanUnicode() => _caseFolded.Count(_subject);

    /// <summary>
    /// The full answer behind each row's returned number: every span, as text (<see cref="AnswerText"/>).
    /// <c>dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- unicode-answers</c> prints them,
    /// to compare two trees before a timing run means anything.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Func<string>> AnswerDetails = new Dictionary<
        string,
        Func<string>
    >
    {
        [nameof(GraphemeScan)] = static () => AnswerText.Spans(_grapheme.Matches(_subject)),
        [nameof(UnicodePropertyScan)] = static () => AnswerText.Spans(_letters.Matches(_subject)),
        [nameof(WordScanUnicode)] = static () => AnswerText.Spans(_words.Matches(_subject)),
        [nameof(CaseFoldScanUnicode)] = static () => AnswerText.Spans(_caseFolded.Matches(_subject)),
    };

    /// <summary>Writes code points, given as space-separated hex, as a string.</summary>
    /// <param name="hex">The code points, for example <c>0915 094D 0937</c>.</param>
    /// <returns>The string.</returns>
    private static string Cp(string hex) =>
        string.Concat(
            hex.Split(' ')
                .Select(static h =>
                    char.ConvertFromUtf32(int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                )
        );

    /// <summary>Builds <see cref="_subject"/>: the segments, cycled in order until the size is reached.</summary>
    /// <returns>The subject.</returns>
    private static string BuildSubject()
    {
        // Each segment is one script's worth of cases, ending in a space.
        string[] segments =
        [
            // Latin: precomposed e-acute, e + combining acute, o + combining diaeresis, n + combining
            // tilde, sharp s, the fi ligature, an accented capital, a dotted capital I.
            Cp("0063 0061 0066 00E9 20 0063 0061 0066 0065 0301 20 005A 006F 0065 0308 20 0061 006E 0303 006F 20")
                + Cp("0073 0074 0072 0061 00DF 0065 20 FB01 006E 0061 006C 20 00C9 0043 004F 004C 0045 20 0130 20"),
            // Devanagari: ksha, stra + vowel sign, namaste, and a ka-virama-ta-virama-ya-aa chain.
            Cp("0915 094D 0937 20 0938 094D 0924 094D 0930 0940 20 0928 092E 0938 094D 0924 0947 20")
                + Cp("0915 094D 0924 094D 092F 093E 20"),
            // Emoji: a ZWJ family, a flag pair, a thumbs-up with skin tone, a keycap.
            Cp("1F468 200D 1F469 200D 1F467 20 1F1EC 1F1E7 20 1F44D 1F3FD 20 0031 FE0F 20E3 20"),
            // Hangul: precomposed syllables, then the same syllables as conjoining jamo.
            Cp("D55C AD6D C5B4 20 1112 1161 11AB 20 1100 1175 11A8 20"),
            // Greek: a lower-case word with final sigma, and its capitals.
            Cp("03C3 03AF 03C3 03C5 03C6 03BF 03C2 20 03A3 038A 03A3 03A5 03A6 039F 03A3 20"),
        ];

        System.Text.StringBuilder builder = new(_size + 128);
        int next = 0;
        while (builder.Length < _size)
        {
            builder.Append(segments[next]);
            next = (next + 1) % segments.Length;
        }

        return builder.ToString();
    }
}
