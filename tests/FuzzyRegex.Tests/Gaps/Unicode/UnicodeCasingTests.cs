using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// <c>get_all_cases</c> and <c>fold_case</c> over every codepoint from 0 to 0x10FFFF, against
/// upstream's C extension.
/// </summary>
/// <remarks>
/// One digest per encoding and folding combination rather than 1.1 million rows: the fixture stays
/// a few lines and the check stays exhaustive. A mismatch says only "something is wrong" - narrow
/// it by plane, then by block, in a throwaway script under <c>.scratch/</c>.
/// </remarks>
public sealed class UnicodeCasingTests
{
    private const int _maxCodepoint = 0x10FFFF;

    [Test]
    [Arguments("unicode", "simple", RegexFlags.Unicode | RegexFlags.IgnoreCase)]
    [Arguments("unicode", "full", RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase)]
    [Arguments("ascii", "simple", RegexFlags.Ascii | RegexFlags.IgnoreCase)]
    [Arguments("ascii", "full", RegexFlags.Ascii | RegexFlags.IgnoreCase | RegexFlags.FullCase)]
    public void Get_all_cases_matches_upstream_for_every_codepoint(string encoding, string folding, int flags)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var line = new StringBuilder(32);

        for (int codepoint = 0; codepoint <= _maxCodepoint; codepoint++)
        {
            int?[] cases = RegexModule.GetAllCases(flags, (uint)codepoint);

            line.Clear();
            Append(line, codepoint);
            line.Append(':');
            for (int i = 0; i < cases.Length; i++)
            {
                if (i > 0)
                {
                    line.Append(',');
                }

                // Upstream appends a None when full case-folding would expand the character; the
                // generator renders it as an empty field.
                if (cases[i] is int value)
                {
                    Append(line, value);
                }
            }

            line.Append('\n');
            Update(hash, line);
        }

        Digest(hash).Should().Be(UnicodeFixture.Casing[$"get_all_cases.{encoding}.{folding}"]);
    }

    [Test]
    [Arguments("unicode", "simple", RegexFlags.Unicode | RegexFlags.IgnoreCase)]
    [Arguments("unicode", "full", RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase)]
    [Arguments("ascii", "simple", RegexFlags.Ascii | RegexFlags.IgnoreCase)]
    [Arguments("ascii", "full", RegexFlags.Ascii | RegexFlags.IgnoreCase | RegexFlags.FullCase)]
    // Without IGNORECASE upstream hands the string straight back, whatever else is set.
    [Arguments("unicode", "none", RegexFlags.Unicode | RegexFlags.FullCase)]
    public void Fold_case_matches_upstream_for_every_codepoint(string encoding, string folding, int flags)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var line = new StringBuilder(32);
        int[] one = new int[1];

        for (int codepoint = 0; codepoint <= _maxCodepoint; codepoint++)
        {
            one[0] = codepoint;
            int[] folded = RegexModule.FoldCase(flags, one);

            line.Clear();
            Append(line, codepoint);
            line.Append(':');
            for (int i = 0; i < folded.Length; i++)
            {
                if (i > 0)
                {
                    line.Append(',');
                }

                Append(line, folded[i]);
            }

            line.Append('\n');
            Update(hash, line);
        }

        Digest(hash).Should().Be(UnicodeFixture.Casing[$"fold_case.{encoding}.{folding}"]);
    }

    /// <summary>
    /// Folding a run, not a single codepoint: the digests above call <c>FoldCase</c> once per
    /// codepoint, so nothing there would catch a loop that mis-places an expansion.
    /// </summary>
    /// <param name="flags">The flags in force.</param>
    /// <param name="input">The run to fold.</param>
    /// <param name="expected">Upstream's answer, measured against the oracle on 2026-08-30.</param>
    [Test]
    // Full folding: SHARP S expands to "ss", and the ffi ligature to three codepoints, so the
    // result is longer than the input and the following characters have to land after it.
    [Arguments(
        RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "StRaSSe",
        new[] { 0x73, 0x74, 0x72, 0x61, 0x73, 0x73, 0x65 }
    )]
    [Arguments(
        RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "ßẞ",
        new[] { 0x73, 0x73, 0x73, 0x73 }
    )]
    [Arguments(
        RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "AﬃB",
        new[] { 0x61, 0x66, 0x66, 0x69, 0x62 }
    )]
    // Simple folding leaves both of those alone, one codepoint in and one out.
    [Arguments(RegexFlags.Unicode | RegexFlags.IgnoreCase, "ßẞ", new[] { 0xDF, 0xDF })]
    [Arguments(RegexFlags.Unicode | RegexFlags.IgnoreCase, "AﬃB", new[] { 0x61, 0xFB03, 0x62 })]
    // The Turkic I variants pass through under Unicode, and only I folds under ASCII.
    [Arguments(
        RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "İıIi",
        new[] { 0x130, 0x131, 0x49, 0x69 }
    )]
    [Arguments(
        RegexFlags.Ascii | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "İıIi",
        new[] { 0x130, 0x131, 0x69, 0x69 }
    )]
    // Final sigma and sigma both fold to sigma under Unicode; ASCII touches neither.
    [Arguments(RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase, "Σςσ", new[] { 0x3C3, 0x3C3, 0x3C3 })]
    [Arguments(RegexFlags.Ascii | RegexFlags.IgnoreCase | RegexFlags.FullCase, "Σςσ", new[] { 0x3A3, 0x3C2, 0x3C3 })]
    public void Fold_case_folds_a_whole_run(int flags, string input, int[] expected)
    {
        int[] codepoints = [.. input.EnumerateRunes().Select(static rune => rune.Value)];

        RegexModule.FoldCase(flags, codepoints).Should().Equal(expected);
    }

    /// <summary>
    /// The four variants of I/i pass through case folding unchanged, so a Turkic pattern can be
    /// handled separately. Upstream <c>unicode_simple_case_fold</c> (<c>_regex.c</c> line 1997).
    /// </summary>
    /// <param name="codepoint">A Turkic I variant.</param>
    [Test]
    [Arguments('I')]
    [Arguments('i')]
    [Arguments(0x0130)]
    [Arguments(0x0131)]
    public void Turkic_i_variants_are_not_folded(int codepoint)
    {
        Encodings.SimpleCaseFold(CaseEncoding.Unicode, (uint)codepoint).Should().Be((uint)codepoint);
    }

    private static void Append(StringBuilder line, int value) =>
        line.Append(value.ToString(CultureInfo.InvariantCulture));

    private static void Update(IncrementalHash hash, StringBuilder line)
    {
        // The generator hashes UTF-8, and every character here is a digit or punctuation, so the
        // bytes are the characters.
        Span<byte> bytes = stackalloc byte[line.Length];
        for (int i = 0; i < line.Length; i++)
        {
            bytes[i] = (byte)line[i];
        }

        hash.AppendData(bytes);
    }

    private static string Digest(IncrementalHash hash) => Convert.ToHexStringLower(hash.GetHashAndReset());
}
