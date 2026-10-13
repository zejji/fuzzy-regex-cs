using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The detail half of the "same answer" check the v1.0 gate makes before it compares a time
/// (<c>S63</c> scope item 2). A benchmark returns one number, and one number can agree by accident:
/// a swap of two words leaves a substitution's length unchanged whatever it swapped. So each row
/// also writes the full answer it rests on - every span, the substituted text, which inputs
/// matched - as text, and <c>bench/baselines/measure_python.py</c> builds the same text from
/// Python <c>regex</c> and compares.
/// </summary>
/// <remarks>
/// The text formats are a contract with that script; change both together. A short text is printed
/// as it is, so a mismatch can be read; a long one as its SHA-256, first 16 hex digits.
/// </remarks>
internal static class AnswerText
{
    /// <summary>Texts up to this length print as they are; longer ones print as a hash.</summary>
    private const int _printable = 80;

    /// <summary>One span: <c>start,end</c>, in UTF-16 units, which equal code points on these subjects.</summary>
    /// <param name="match">The match; a failed one is written <c>-</c>.</param>
    /// <returns>The text.</returns>
    public static string Span(Match match) =>
        match.Success
            ? string.Create(CultureInfo.InvariantCulture, $"{match.Index},{match.Index + match.Length}")
            : "-";

    /// <summary>A span with its fuzzy counts: <c>start,end,s,i,d</c>.</summary>
    /// <param name="match">The match.</param>
    /// <returns>The text.</returns>
    public static string FuzzySpan(Match match) =>
        match.Success
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{Span(match)},{match.FuzzyCounts.Substitutions},{match.FuzzyCounts.Insertions},{match.FuzzyCounts.Deletions}"
            )
            : "-";

    /// <summary>Every span, one per line.</summary>
    /// <param name="matches">The matches.</param>
    /// <returns>The text.</returns>
    public static string Spans(IEnumerable<Match> matches) => string.Join('\n', matches.Select(Span));

    /// <summary>One character per input, <c>1</c> where it matched and <c>0</c> where it did not.</summary>
    /// <param name="hits">Whether each input matched.</param>
    /// <returns>The text.</returns>
    public static string Bitmap(IEnumerable<bool> hits) => string.Concat(hits.Select(static h => h ? '1' : '0'));

    /// <summary>The text as printed: itself when short, else <c>sha256:</c> and 16 hex digits of its UTF-8.</summary>
    /// <param name="text">The answer text.</param>
    /// <returns>What to print.</returns>
    public static string Digest(string text) =>
        text.Length <= _printable && !text.Contains('\n', StringComparison.Ordinal)
            ? text
            : "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    /// <summary>
    /// Prints every <c>[Benchmark]</c> method's return value and, where the class has one, its
    /// detail text: <c>Name value detail</c>, one row per line.
    /// </summary>
    /// <param name="benchmarks">An instance of the benchmark class.</param>
    /// <param name="details">The detail text per row name.</param>
    public static void Print(object benchmarks, IReadOnlyDictionary<string, Func<string>> details)
    {
        foreach (
            var method in benchmarks
                .GetType()
                .GetMethods()
                .Where(static m => m.IsDefined(typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute), inherit: false))
        )
        {
            string value = Convert.ToString(method.Invoke(benchmarks, null), CultureInfo.InvariantCulture) ?? "";
            string detail = details.TryGetValue(method.Name, out var text) ? " " + Digest(text()) : "";
            Console.WriteLine(method.Name.PadRight(44) + " " + value + detail);
        }
    }
}
