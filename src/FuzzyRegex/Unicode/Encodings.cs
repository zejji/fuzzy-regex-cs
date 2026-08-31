using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>Which of upstream's <c>RE_EncodingTable</c>s a set of flags selects.</summary>
internal enum CaseEncoding
{
    /// <summary>Upstream <c>ascii_encoding</c> (<c>upstream/src/_regex.c</c> line 1003).</summary>
    Ascii,

    /// <summary>Upstream <c>unicode_encoding</c> (<c>upstream/src/_regex.c</c> line 2046).</summary>
    Unicode,
}

/// <summary>
/// The casing and property half of upstream's encoding tables
/// (<c>upstream/src/_regex.c</c> lines 941-1003 and 1362-2046).
/// </summary>
/// <remarks>
/// Upstream stores each encoding as a struct of function pointers and dispatches through it. Two
/// encodings and four operations do not justify a table here, so the encoding is an enum and the
/// dispatch is a switch; the members keep upstream's names. The <c>locale_</c> encoding is not
/// ported - <c>LOCALE</c> depends on the C locale and is not surfaced (see <c>docs/PORTMAP.md</c>).
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
            throw new NotImplementedException(
                "needs:locale-flag - the LOCALE encoding's casing depends on the C locale"
            );
        }

        if ((flags & RegexFlags.Ascii) != 0)
        {
            return CaseEncoding.Ascii;
        }

        return CaseEncoding.Unicode;
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
        if (encoding == CaseEncoding.Unicode)
        {
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
        if (encoding == CaseEncoding.Ascii)
        {
            // Uppercase folds to lowercase.
            return ch is >= 'A' and <= 'Z' ? ch ^ 0x20 : ch;
        }

        // Is it a possible Turkic character? If so, pass it through unchanged.
        if (IsPossibleTurkic(ch))
        {
            return ch;
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
        if (encoding == CaseEncoding.Ascii)
        {
            // Uppercase folds to lowercase.
            folded[0] = ch is >= 'A' and <= 'Z' ? ch ^ 0x20 : ch;
            return 1;
        }

        // Is it a possible Turkic character? If so, pass it through unchanged.
        if (IsPossibleTurkic(ch))
        {
            folded[0] = ch;
            return 1;
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

    /// <summary>Upstream <c>unicode_possible_turkic</c> (line 1984).</summary>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> for the four variants of I/i.</returns>
    internal static bool IsPossibleTurkic(uint ch) => ch is 'I' or 'i' or 0x0130 or 0x0131;

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
