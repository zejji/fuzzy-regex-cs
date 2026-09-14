namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// The DEFAULT (non-Turkic) case data for the four dotted and dotless I codepoints, which this port
/// substitutes for upstream's. <b>This is a deliberate divergence from upstream, not a port defect.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>What the definitive source says.</b> <c>CaseFolding.txt</c> (17.0.0, 2025-07-30) gives these
/// rows for the four codepoints - and gives no other row for any of them:
/// </para>
/// <code>
/// 0049; C; 0069; # LATIN CAPITAL LETTER I
/// 0049; T; 0131; # LATIN CAPITAL LETTER I
/// 0130; F; 0069 0307; # LATIN CAPITAL LETTER I WITH DOT ABOVE
/// 0130; T; 0069; # LATIN CAPITAL LETTER I WITH DOT ABOVE
/// </code>
/// <para>
/// and its header defines the status letters and how to use them:
/// </para>
/// <code>
/// # T: special case for uppercase I and dotted uppercase I
/// #    - For non-Turkic languages, this mapping is normally not used.
/// #    - For Turkic languages (tr, az), this mapping can be used instead of the normal mapping
/// #      for these characters.
/// #
/// # Usage:
/// #  A. To do a simple case folding, use the mappings with status C + S.
/// #  B. To do a full case folding, use the mappings with status C + F.
/// #
/// #    The mappings with status T can be used or omitted depending on the desired case-folding
/// #    behavior. (The default option is to exclude them.)
/// </code>
/// <para>
/// UTS #18 RL1.5 requires "at least the simple, <b>default</b> Unicode case-insensitive matching"
/// and "at least the simple, <b>default</b> Unicode case folding" (emphasis added); the core
/// specification section 5.18.2 calls the Turkish mapping "a case mapping that depends on the
/// locale". Neither the pattern nor the subject carries a locale here, so the default applies.
/// </para>
/// <para>
/// <b>What upstream does instead, and why it is wrong.</b>
/// <c>upstream/tools/build_regex_unicode.py</c> merges the <c>T</c> rows into BOTH default tables -
/// <c>kind in {'S', 'C', 'T'}</c> at line 455 and <c>kind in {'F', 'C', 'T'}</c> at line 459 - and
/// then hard-codes the Turkic pairing into the all-cases table at lines 1071-1074
/// (<c>all_cases[0x49] = {0x49, 0x69, 0x131}</c> and <c>all_cases[0x69] = {0x69, 0x49, 0x130}</c>).
/// Two consequences follow, and this port had both before S45:
/// </para>
/// <list type="number">
/// <item>
/// Every <c>_IGN</c> opcode reads <c>re_get_all_cases</c>, so <c>(?i)I</c> matched <c>ı</c> and
/// <c>(?i)i</c> matched <c>İ</c> - the Turkic behaviour, applied with no locale asked for.
/// </item>
/// <item>
/// <c>unicode_possible_turkic</c> (<c>upstream/src/_regex.c</c> line 1984) papers over the folding
/// half by passing all four codepoints through the fold functions UNCHANGED. That is not the
/// default mapping either: it loses <c>0049; C; 0069</c>, so <c>I</c> folded to <c>I</c>, and it
/// loses <c>0130; F; 0069 0307</c>, so <c>İ</c> never reached the full fold at all. That second
/// loss is the whole of ledger entry 7.
/// </item>
/// </list>
/// <para>
/// <b>Three second engines were run on the 25-cell grid before this was written</b>
/// (2026-09-14, <c>.scratch/s45-definition.py</c>, <c>.scratch/s45-perl.pl</c>,
/// <c>.scratch/s45-dotnet.ps1</c>). PCRE2 10.47 under <c>PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS</c>
/// and .NET 10.0.10 under <c>RegexOptions.IgnoreCase | RegexOptions.CultureInvariant</c> agree cell
/// for cell with the simple column below; Perl 5.42.2's <c>/i</c> under <c>(?u:...)</c>, which folds
/// fully, agrees cell for cell with the full column - <c>İ</c> matches <c>i</c> + U+0307 there and
/// nowhere else. <c>regex 2026.9.10</c> is the only one of the four that answers the Turkic way.
/// </para>
/// <para>
/// <b>The table.</b> Simple folding is C + S, full folding is C + F, and the all-cases set is the
/// closure of "shares a simple folding", which is how <c>generate_all_cases</c> builds it from the
/// simple table (line 1056) before it overrides these four:
/// </para>
/// <code>
///  codepoint   simple fold   full fold    all cases
///  U+0049 I    U+0069        U+0069       { U+0049, U+0069 }
///  U+0069 i    U+0069        U+0069       { U+0069, U+0049 }
///  U+0130 I-.  U+0130        U+0069 0307  { U+0130 }
///  U+0131 i.   U+0131        U+0131       { U+0131 }
/// </code>
/// <para>
/// U+0130 and U+0131 have no <c>C</c> or <c>S</c> row, so they simple-fold to themselves, and
/// nothing else folds to them - which is why each is alone in its case set.
/// </para>
/// <para>
/// <b>A Turkic mode is NOT provided.</b> Upstream exposes no locale to select one, and adding a flag
/// this port's API does not have would be new surface, not a fix. If one is ever wanted, it goes
/// here: the <c>T</c> rows are exactly <c>0049 -&gt; 0131</c> and <c>0130 -&gt; 0069</c>, which is
/// what <c>build_regex_unicode.py</c> line 468 asserts the Turkic set still is.
/// </para>
/// </remarks>
internal static class TurkicDefaults
{
    /// <summary>
    /// The default simple case folding of the four, if <paramref name="ch"/> is one of them.
    /// </summary>
    /// <param name="ch">The codepoint.</param>
    /// <param name="folded">Receives the folding.</param>
    /// <returns><see langword="true"/> if the codepoint is one of the four.</returns>
    internal static bool TrySimpleCaseFold(uint ch, out uint folded)
    {
        switch (ch)
        {
            // 0049; C; 0069 - a common mapping, so it is in the simple folding too.
            case 'I':
                folded = 'i';
                return true;

            // No C or S row for any of these three, so each folds to itself.
            case 'i':
            case 0x0130:
            case 0x0131:
                folded = ch;
                return true;

            default:
                folded = ch;
                return false;
        }
    }

    /// <summary>
    /// The default full case folding of the four, if <paramref name="ch"/> is one of them.
    /// </summary>
    /// <param name="ch">The codepoint.</param>
    /// <param name="folded">
    /// Receives the folding. Must hold <see cref="UnicodeTables.MaxFolded"/>.
    /// </param>
    /// <param name="count">Receives how many entries were written.</param>
    /// <returns><see langword="true"/> if the codepoint is one of the four.</returns>
    internal static bool TryFullCaseFold(uint ch, Span<uint> folded, out int count)
    {
        switch (ch)
        {
            // 0130; F; 0069 0307 - the row upstream loses, and the one that grows.
            // 0x0307 is COMBINING DOT ABOVE.
            case 0x0130:
                folded[0] = 'i';
                folded[1] = 0x0307;
                count = 2;
                return true;

            // 0049; C; 0069 again: C is common to both simple and full.
            case 'I':
                folded[0] = 'i';
                count = 1;
                return true;

            case 'i':
            case 0x0131:
                folded[0] = ch;
                count = 1;
                return true;

            default:
                count = 0;
                return false;
        }
    }

    /// <summary>
    /// The default case set of the four, if <paramref name="ch"/> is one of them.
    /// </summary>
    /// <remarks>
    /// The codepoint itself is always first, because every caller of
    /// <c>Encodings.AllCases</c> reads <c>cases[0]</c> as the codepoint it asked about -
    /// <c>Matcher.SameCharIgn</c> skips it outright.
    /// </remarks>
    /// <param name="ch">The codepoint.</param>
    /// <param name="cases">
    /// Receives the cases. Must hold <see cref="UnicodeTables.MaxCases"/>.
    /// </param>
    /// <param name="count">Receives how many entries were written.</param>
    /// <returns><see langword="true"/> if the codepoint is one of the four.</returns>
    internal static bool TryAllCases(uint ch, Span<uint> cases, out int count)
    {
        cases[0] = ch;

        switch (ch)
        {
            // I and i share the simple folding U+0069, so they share a case set.
            case 'I':
                cases[1] = 'i';
                count = 2;
                return true;

            case 'i':
                cases[1] = 'I';
                count = 2;
                return true;

            // Each simple-folds to itself and nothing simple-folds to it, so each is alone.
            case 0x0130:
            case 0x0131:
                count = 1;
                return true;

            default:
                count = 0;
                return false;
        }
    }
}
