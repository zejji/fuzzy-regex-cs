using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>Which of upstream's <c>RE_EncodingTable</c>s a set of flags selects.</summary>
internal enum CaseEncoding
{
    /// <summary>Upstream <c>ascii_encoding</c> (<c>upstream/src/_regex.c</c> line 1003).</summary>
    Ascii,

    /// <summary>
    /// Upstream <c>locale_encoding</c>. Its non-casing operations follow the Unicode path for this
    /// port's <c>string</c>-only API; its casing operations are unsupported.
    /// </summary>
    Locale,

    /// <summary>Upstream <c>unicode_encoding</c> (<c>upstream/src/_regex.c</c> line 2046).</summary>
    Unicode,
}

/// <summary>
/// The casing and property half of upstream's encoding tables
/// (<c>upstream/src/_regex.c</c> lines 941-1003 and 1362-2046).
/// </summary>
/// <remarks>
/// Upstream stores each encoding as a struct of function pointers and dispatches through it. Three
/// encodings and four operations do not justify a table here, so the encoding is an enum and the
/// dispatch is a switch; the members keep upstream's names. The <c>locale_</c> encoding's casing is
/// not ported because it depends on the C locale and is not surfaced (see <c>docs/PORTMAP.md</c>).
/// </remarks>
internal static class Encodings
{
    /// <summary>
    /// Upstream's "what's the encoding?" block, which appears verbatim in <c>fold_case</c>
    /// (<c>upstream/src/_regex.c</c> lines 26176-26184) and <c>get_all_cases</c> (lines
    /// 26362-26370).
    /// </summary>
    /// <param name="flags">The flags in force.</param>
    /// <returns>The encoding those flags select.</returns>
    internal static CaseEncoding Select(int flags)
    {
        if ((flags & RegexFlags.Unicode) != 0)
        {
            return CaseEncoding.Unicode;
        }

        if ((flags & RegexFlags.Locale) != 0)
        {
            return CaseEncoding.Locale;
        }

        if ((flags & RegexFlags.Ascii) != 0)
        {
            return CaseEncoding.Ascii;
        }

        return CaseEncoding.Unicode;
    }

    /// <summary>Rejects an attempt to use the C locale's casing through the public API.</summary>
    /// <param name="encoding">The encoding selected for the casing operation.</param>
    /// <exception cref="NotSupportedException"><paramref name="encoding"/> is <c>LOCALE</c>.</exception>
    internal static void EnsureCaseSupported(CaseEncoding encoding)
    {
        if (encoding == CaseEncoding.Locale)
        {
            throw new NotSupportedException(
                "The LOCALE flag (?L) is not supported: its casing depends on the C locale, which .NET does not expose. Use (?u) or (?a)."
            );
        }
    }

    /// <summary>
    /// Upstream <c>unicode_all_cases</c> / <c>ascii_all_cases</c> (lines 1989 and 946).
    /// </summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch">The codepoint.</param>
    /// <param name="codepoints">
    /// Receives the cases. Must hold <see cref="UnicodeTables.MaxCases"/>.
    /// </param>
    /// <returns>How many cases were written.</returns>
    internal static int AllCases(CaseEncoding encoding, uint ch, Span<uint> codepoints)
    {
        EnsureCaseSupported(encoding);

        if (encoding == CaseEncoding.Unicode)
        {
            // DIVERGES FROM UPSTREAM, deliberately - see TurkicDefaults.
            if (TurkicDefaults.TryAllCases(ch, codepoints, out int turkicCount))
            {
                return turkicCount;
            }

            return UnicodeTables.GetAllCases(ch, codepoints);
        }

        int count = 0;
        codepoints[count++] = ch;

        if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
        {
            // It's a letter, so add the other case.
            codepoints[count++] = ch ^ 0x20;
        }

        return count;
    }

    /// <summary>
    /// Upstream <c>unicode_simple_case_fold</c> / <c>ascii_simple_case_fold</c> (lines 1997
    /// and 962).
    /// </summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns>The folded codepoint.</returns>
    internal static uint SimpleCaseFold(CaseEncoding encoding, uint ch)
    {
        EnsureCaseSupported(encoding);

        if (encoding == CaseEncoding.Ascii)
        {
            // Uppercase folds to lowercase.
            return ch is >= 'A' and <= 'Z' ? ch ^ 0x20 : ch;
        }

        // DIVERGES FROM UPSTREAM, deliberately. Upstream passes the four I variants through
        // UNCHANGED here; this port returns their default (non-Turkic) simple folding instead.
        // See TurkicDefaults.
        if (TurkicDefaults.TrySimpleCaseFold(ch, out uint turkicFolded))
        {
            return turkicFolded;
        }

        return UnicodeTables.GetSimpleCaseFolding(ch);
    }

    /// <summary>
    /// Upstream <c>unicode_full_case_fold</c> / <c>ascii_full_case_fold</c> (lines 2009 and 971).
    /// </summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch">The codepoint.</param>
    /// <param name="folded">
    /// Receives the folding. Must hold <see cref="UnicodeTables.MaxFolded"/>.
    /// </param>
    /// <returns>How many codepoints were written.</returns>
    internal static int FullCaseFold(CaseEncoding encoding, uint ch, Span<uint> folded)
    {
        EnsureCaseSupported(encoding);

        if (encoding == CaseEncoding.Ascii)
        {
            // Uppercase folds to lowercase.
            folded[0] = ch is >= 'A' and <= 'Z' ? ch ^ 0x20 : ch;
            return 1;
        }

        // DIVERGES FROM UPSTREAM, deliberately. Upstream passes the four I variants through
        // UNCHANGED here, which is what loses U+0130's expansion to 'i' + U+0307.
        // See TurkicDefaults.
        if (TurkicDefaults.TryFullCaseFold(ch, folded, out int turkicCount))
        {
            return turkicCount;
        }

        return UnicodeTables.GetFullCaseFolding(ch, folded);
    }

    /// <summary>
    /// Upstream <c>unicode_is_line_sep</c> / <c>ascii_is_line_sep</c>
    /// (<c>upstream/src/_regex.c</c> lines 1936 and 894).
    /// </summary>
    /// <remarks>
    /// The position predicates built on this - <c>at_line_start</c> and <c>at_line_end</c> - take
    /// the match state, so they live in <c>Engine.Matcher</c> rather than here; this file is the
    /// half of upstream's encoding tables that needs nothing but a codepoint.
    /// </remarks>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it separates lines.</returns>
    internal static bool IsLineSep(CaseEncoding encoding, uint ch) =>
        encoding == CaseEncoding.Ascii
            ? ch is >= 0x0A and <= 0x0D
            : ch is (>= 0x0A and <= 0x0D) or 0x85 or 0x2028 or 0x2029;

    // Upstream <c>unicode_possible_turkic</c> (line 1984) - "is it one of the four variants of
    // I/i?" - is NOT ported as a predicate. Its only two readers here were the two fold functions
    // above, where it made them pass those four through unchanged; <see cref="TurkicDefaults"/>
    // replaces that with the default mapping and is keyed on the same four codepoints. Upstream's
    // other reader, <c>same_char_ign_turkic</c> inside <c>string_search_fld</c>, is a Phase 7
    // deferral (see <c>Engine.Matcher</c>'s remarks); a slice restoring it restores this too.

    /// <summary>Upstream <c>RE_PROP_GC_LU</c> (<c>upstream/src/_regex.c</c> line 66).</summary>
    /// <remarks>
    /// These three are here rather than in the generated constants because upstream defines them in
    /// <c>_regex.c</c>, not in <c>_regex_unicode.h</c> that
    /// <c>tools/transliterate-unicode.py</c> reads. Their one reader is
    /// <c>Matcher.MatchesPropertyIgn</c>. The <c>*_has_property_ign</c> encoding-table slot they
    /// also belong to is NOT ported: the matcher never reaches it - only
    /// <c>matches_PROPERTY_IGN</c> does - and its one other caller, <c>search_start</c>, is the
    /// Phase 7 deferral.
    /// </remarks>
    internal const uint PropGcLu = (UnicodeTables.PropGc << 16) | UnicodeTables.PropLu;

    /// <summary>Upstream <c>RE_PROP_GC_LL</c> (line 67).</summary>
    internal const uint PropGcLl = (UnicodeTables.PropGc << 16) | UnicodeTables.PropLl;

    /// <summary>Upstream <c>RE_PROP_GC_LT</c> (line 68).</summary>
    internal const uint PropGcLt = (UnicodeTables.PropGc << 16) | UnicodeTables.PropLt;

    /// <summary>Upstream <c>RE_ASCII_MAX</c> (<c>upstream/src/_regex_unicode.h</c> line 16).</summary>
    private const uint _asciiMax = 0x7F;

    /// <summary>Upstream <c>UNASSIGNED_CODEPOINT</c> (<c>upstream/src/_regex.c</c> line 62).</summary>
    private const uint _unassignedCodepoint = 0x10FFFF;

    /// <summary>
    /// Upstream <c>ascii_has_property</c> (<c>upstream/src/_regex.c</c> line 822): the ASCII
    /// encoding's whole property story is that everything above <c>RE_ASCII_MAX</c> is answered as
    /// though it were unassigned, and the Unicode table answers the rest.
    /// </summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="property">The packed property code.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint has that value.</returns>
    internal static bool HasProperty(CaseEncoding encoding, uint property, uint ch) =>
        HasProperty(property, encoding == CaseEncoding.Ascii && ch > _asciiMax ? _unassignedCodepoint : ch);

    /// <summary>
    /// Upstream <c>unicode_has_property</c> (<c>upstream/src/_regex.c</c> line 1362): whether a
    /// codepoint has a given value for a Unicode property.
    /// </summary>
    /// <param name="property">The packed property code: the property index in the high 16 bits, the value in the low 16.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint has that value.</returns>
    internal static bool HasProperty(uint property, uint ch)
    {
        uint prop = property >> 16;
        if (prop >= UnicodeTables.PropertyFunctionCount)
        {
            return false;
        }

        uint value = property & 0xFFFF;

        if (prop == UnicodeTables.PropScx)
        {
            Span<byte> scripts = stackalloc byte[UnicodeTables.MaxScx];
            int count = UnicodeTables.GetScriptExtensions(ch, scripts);

            for (int i = 0; i < count; i++)
            {
                if (scripts[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        uint v = UnicodeTables.GetProperty(prop, ch);

        if (v == value)
        {
            return true;
        }

        if (prop == UnicodeTables.PropGc)
        {
            // The general-category groups: C, L, M, N, P, S and Z each cover several categories,
            // and the masks say which. `1 << v` is int arithmetic upstream too - v is a category
            // index below 32, so nothing here can shift out of range.
            switch (value)
            {
                case UnicodeTables.PropAssigned:
                    return v != UnicodeTables.PropCn;
                case UnicodeTables.PropC:
                    return (UnicodeTables.PropCMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropCasedletter:
                    return v is UnicodeTables.PropLu or UnicodeTables.PropLl or UnicodeTables.PropLt;
                case UnicodeTables.PropL:
                    return (UnicodeTables.PropLMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropM:
                    return (UnicodeTables.PropMMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropN:
                    return (UnicodeTables.PropNMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropP:
                    return (UnicodeTables.PropPMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropS:
                    return (UnicodeTables.PropSMask & (1 << (int)v)) != 0;
                case UnicodeTables.PropZ:
                    return (UnicodeTables.PropZMask & (1 << (int)v)) != 0;
                default:
                    break;
            }
        }

        return false;
    }
}
