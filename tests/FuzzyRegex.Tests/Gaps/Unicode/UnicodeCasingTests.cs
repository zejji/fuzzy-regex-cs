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

    /// <summary>
    /// The four codepoints where this port DELIBERATELY differs from upstream, rendered as the
    /// digest lines UPSTREAM produces for them, so that both sweeps below still run over the whole
    /// plane against upstream's own fixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the blast-radius proof, and it is why the substitution is upstream's values
    /// rather than an exclusion.</b> S45 replaced upstream's Turkic case data with the default one
    /// <c>CaseFolding.txt</c> specifies (see
    /// <see cref="Fuzzy.Text.RegularExpressions.Unicode.TurkicDefaults"/>). Hashing upstream's
    /// answer for exactly these four and this port's for the other 1,114,108 keeps the recorded
    /// digest valid, so the sweeps still say "nothing else moved" - which no exclusion could. A
    /// fifth codepoint that moves turns them red, and so does a change to one of the four that is
    /// not the one recorded here, because
    /// <see cref="Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine.CaseFoldingTests"/> and
    /// <see cref="The_four_turkic_codepoints_carry_the_default_case_data_not_upstreams"/> pin this
    /// port's own values separately.
    /// </para>
    /// <para>
    /// Recorded 2026-09-14 against <c>regex 2026.9.10</c> by <c>.scratch/s45-upstream-four.py</c>,
    /// which calls <c>_regex.get_all_cases</c> and <c>_regex.fold_case</c> directly. Upstream's
    /// answer is the same under simple and full folding for all four - that identity IS the defect -
    /// so one table serves both arms. The ASCII arms are untouched by S45 and are not substituted.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<int, string> _upstreamAllCases = new()
    {
        [0x49] = "73,105,305",
        [0x69] = "105,73,304",
        [0x130] = "304,105",
        [0x131] = "305,73",
    };

    /// <summary>Upstream's <c>fold_case</c> lines for the same four, recorded the same way.</summary>
    private static readonly Dictionary<int, string> _upstreamFoldCase = new()
    {
        [0x49] = "73",
        [0x69] = "105",
        [0x130] = "304",
        [0x131] = "305",
    };

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
            if (TrySubstitute(encoding, folding, codepoint, _upstreamAllCases, line))
            {
                Update(hash, line);

                continue;
            }

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
            if (TrySubstitute(encoding, folding, codepoint, _upstreamFoldCase, line))
            {
                Update(hash, line);

                continue;
            }

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
    // The dotted capital expands and I folds to i, both under the default (non-Turkic) case data
    // S45 substituted for upstream's; the dotless small has no default mapping at all.
    // upstream: _regex.fold_case(regex.U|regex.I|regex.F, 'İıIi') == 'İıIi', unchanged.
    [Arguments(
        RegexFlags.Unicode | RegexFlags.IgnoreCase | RegexFlags.FullCase,
        "İıIi",
        new[] { 0x69, 0x307, 0x131, 0x69, 0x69 }
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
    /// The four variants of I/i carry the DEFAULT case data, not upstream's Turkic data. This is
    /// the pin the two whole-plane sweeps hand off to, because they hash upstream's answer for
    /// exactly these four - see <see cref="_upstreamAllCases"/>.
    /// </summary>
    /// <remarks>
    /// Every expected value here is a <c>CaseFolding.txt</c> row, quoted in
    /// <see cref="Fuzzy.Text.RegularExpressions.Unicode.TurkicDefaults"/>, and every one of them
    /// disagrees with <c>regex 2026.9.10</c> in at least one column. Upstream's own answers, from
    /// <c>.scratch/s45-upstream-four.py</c> on 2026-09-14: <c>fold_case</c> is the identity for all
    /// four under both foldings, and <c>get_all_cases</c> is
    /// <c>73 -&gt; [73, 105, 305]</c>, <c>105 -&gt; [105, 73, 304]</c>, <c>304 -&gt; [304, 105]</c>,
    /// <c>305 -&gt; [305, 73]</c>.
    /// </remarks>
    /// <param name="codepoint">One of the four.</param>
    /// <param name="simpleFolded">Its default simple case folding (C + S).</param>
    /// <param name="fullFolded">Its default full case folding (C + F).</param>
    /// <param name="allCases">Its default case set, the codepoint itself first.</param>
    [Test]
    // 0049; C; 0069 - a common mapping, so it holds under both foldings.
    [Arguments('I', 'i', new[] { 0x69 }, new[] { 0x49, 0x69 })]
    // No row at all for 0069: it is already folded.
    [Arguments('i', 'i', new[] { 0x69 }, new[] { 0x69, 0x49 })]
    // 0130; F; 0069 0307 - the row upstream loses. No C or S row, so simple folding is identity.
    [Arguments(0x0130, 0x0130, new[] { 0x69, 0x307 }, new[] { 0x130 })]
    // No row at all for 0131 either, so it folds to itself and is alone in its case set.
    [Arguments(0x0131, 0x0131, new[] { 0x131 }, new[] { 0x131 })]
    public void The_four_turkic_codepoints_carry_the_default_case_data_not_upstreams(
        int codepoint,
        int simpleFolded,
        int[] fullFolded,
        int[] allCases
    )
    {
        Encodings.SimpleCaseFold(CaseEncoding.Unicode, (uint)codepoint).Should().Be((uint)simpleFolded);

        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        int foldedCount = Encodings.FullCaseFold(CaseEncoding.Unicode, (uint)codepoint, folded);
        folded[..foldedCount].ToArray().Should().Equal(fullFolded.Select(static c => (uint)c));

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        int caseCount = Encodings.AllCases(CaseEncoding.Unicode, (uint)codepoint, cases);
        cases[..caseCount].ToArray().Should().Equal(allCases.Select(static c => (uint)c));
    }

    /// <summary>
    /// The ASCII encoding is untouched: it folds A-Z and nothing else, so all four answer as they
    /// did before S45 and as upstream still does. This is the control that the change is confined
    /// to the Unicode encoding - the two ASCII sweep arms above substitute nothing and stay green.
    /// </summary>
    /// <param name="codepoint">One of the four.</param>
    /// <param name="folded">Its ASCII simple case folding.</param>
    [Test]
    [Arguments('I', 'i')]
    [Arguments('i', 'i')]
    [Arguments(0x0130, 0x0130)]
    [Arguments(0x0131, 0x0131)]
    public void The_ascii_encoding_still_folds_only_the_plain_pair(int codepoint, int folded) =>
        Encodings.SimpleCaseFold(CaseEncoding.Ascii, (uint)codepoint).Should().Be((uint)folded);

    /// <summary>
    /// Writes upstream's own digest line for one of the four Turkic codepoints, under the Unicode
    /// encoding with IGNORECASE in force - the only arms S45 changed.
    /// </summary>
    /// <param name="encoding">The sweep's encoding name.</param>
    /// <param name="folding">The sweep's folding name.</param>
    /// <param name="codepoint">The codepoint.</param>
    /// <param name="upstream">Upstream's recorded lines.</param>
    /// <param name="line">Receives the line, ready to hash.</param>
    /// <returns><see langword="true"/> if a substitution was made.</returns>
    private static bool TrySubstitute(
        string encoding,
        string folding,
        int codepoint,
        Dictionary<int, string> upstream,
        StringBuilder line
    )
    {
        if (
            !string.Equals(encoding, "unicode", StringComparison.Ordinal)
            || string.Equals(folding, "none", StringComparison.Ordinal)
            || !upstream.TryGetValue(codepoint, out string? fields)
        )
        {
            return false;
        }

        line.Clear();
        Append(line, codepoint);
        line.Append(':').Append(fields).Append('\n');

        return true;
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
