using System.Globalization;
using System.Text;

namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// The Python <c>str</c> predicates upstream's parser leans on, expressed through upstream's own
/// Unicode tables.
/// </summary>
/// <remarks>
/// <para>
/// <c>_regex_core.py</c> calls <c>name.isdigit()</c> and <c>name.isidentifier()</c> to tell a group
/// number from a group name (line 1229 onwards) and <c>word[:1].isalpha()</c> to spot a
/// <c>(*VERB)</c> (line 918). All three are CPython's, defined over CPython's own copy of the UCD,
/// so a port has to reproduce them from something. Doing it from upstream's tables keeps the port
/// to one set of Unicode data.
/// </para>
/// <para>
/// The three mappings below were measured, not reasoned about. Over every codepoint from 0 to
/// 0x10FFFF except the 4,803 added in Unicode 17.0 - which the host's 16.0.0 <c>unicodedata</c>
/// cannot answer for - CPython 3.14.6 and these lookups agree on every one, with no exceptions
/// (2026-08-30, and pinned by the <c>pythonStr</c> digest in the Unicode fixture):
/// </para>
/// <list type="bullet">
/// <item><c>str.isalpha</c> is exactly <c>\p{General_Category=L}</c>.</item>
/// <item><c>str.isdigit</c> is exactly <c>Numeric_Type</c> of <c>Decimal</c> or <c>Digit</c>
/// - which is CPython's rule too: its <c>DECIMAL_MASK</c> and <c>DIGIT_MASK</c> come from
/// UnicodeData.txt fields 6 and 7.</item>
/// <item><c>str.isidentifier</c> is <c>XID_Start</c> or <c>_</c> for the first character and
/// <c>XID_Continue</c> for the rest, with no normalisation.</item>
/// </list>
/// </remarks>
internal static class PythonStr
{
    private static readonly Lazy<Codes> _codes = new(ResolveCodes);

    /// <summary>Python's <c>str.isalpha</c>, for one codepoint.</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns><see langword="true"/> if it is a letter.</returns>
    internal static bool IsAlpha(int codepoint) => Encodings.HasProperty(_codes.Value.Letter, (uint)codepoint);

    /// <summary>Python's <c>str.isdigit</c>, for one codepoint.</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns><see langword="true"/> if it has a digit value.</returns>
    internal static bool IsDigit(int codepoint) =>
        Encodings.HasProperty(_codes.Value.NumericDecimal, (uint)codepoint)
        || Encodings.HasProperty(_codes.Value.NumericDigit, (uint)codepoint);

    /// <summary>CPython's <c>Py_UNICODE_ISIDSTART</c>.</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns><see langword="true"/> if an identifier may start with it.</returns>
    internal static bool IsIdentifierStart(int codepoint) =>
        codepoint == '_' || Encodings.HasProperty(_codes.Value.XidStart, (uint)codepoint);

    /// <summary>CPython's <c>Py_UNICODE_ISIDCONTINUE</c>.</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns><see langword="true"/> if an identifier may continue with it.</returns>
    internal static bool IsIdentifierContinue(int codepoint) =>
        Encodings.HasProperty(_codes.Value.XidContinue, (uint)codepoint);

    /// <summary>Python's <c>str.isdigit</c> over a whole string, as upstream calls it.</summary>
    /// <param name="text">The string.</param>
    /// <returns><see langword="true"/> if it is non-empty and every codepoint has a digit value.</returns>
    internal static bool IsDigitString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Runes, not chars: Python iterates codepoints, so a supplementary-plane digit such as
        // MATHEMATICAL BOLD DIGIT ZERO is one character to it and two to us.
        return text.Length > 0 && text.EnumerateRunes().All(rune => IsDigit(rune.Value));
    }

    /// <summary>
    /// Python's <c>int(text)</c> over a run of digits, as upstream calls it on a group name.
    /// </summary>
    /// <param name="text">The digits.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> where Python's <c>int()</c> raises <c>ValueError</c>.</returns>
    /// <remarks>
    /// <para>
    /// Not <see cref="System.Numerics.BigInteger.TryParse(string, out System.Numerics.BigInteger)"/>:
    /// Python's <c>int()</c> accepts any Unicode <b>decimal</b> digit, so <c>int("١٢")</c> is 12,
    /// which is why <c>(?P=١)</c> compiles upstream to a reference to group 1. It is also
    /// <i>narrower</i> than <see cref="IsDigit"/>, which is <c>Numeric_Type</c> of <c>Decimal</c>
    /// <b>or</b> <c>Digit</c>: <c>"²".isdigit()</c> is true but <c>int("²")</c> raises, and
    /// upstream lets that <c>ValueError</c> escape.
    /// </para>
    /// <para>
    /// <see cref="System.Numerics.BigInteger"/>, because Python's <c>int</c> has no width and
    /// upstream's range checks depend on the true value; the callers narrow it.
    /// </para>
    /// <para>
    /// The digit <i>value</i> comes from <see cref="DecimalValue"/> over upstream's own tables, not
    /// from <see cref="CharUnicodeInfo"/>. It has to: <see cref="IsDigit"/> reads upstream's
    /// tables, which are Unicode 17.0.0, and .NET's are older, so the ten codepoints
    /// U+11DE0-U+11DE9 added in 17.0 would be a digit to the <c>isdigit</c> gate and not a decimal
    /// digit to the conversion - and the port would throw where upstream compiles. Found by the
    /// second S11 review pass, which is the same reason the port resolves <c>\N{...}</c> against
    /// 17.0.0 rather than the host's (PORTMAP, "Character names are Unicode 17.0.0").
    /// </para>
    /// </remarks>
    internal static bool TryParseInt(string text, out System.Numerics.BigInteger value)
    {
        ArgumentNullException.ThrowIfNull(text);

        value = System.Numerics.BigInteger.Zero;
        if (text.Length == 0)
        {
            return false;
        }

        // Runes, not chars: MATHEMATICAL BOLD DIGIT ZERO and friends are decimal digits above the
        // BMP, and Python iterates codepoints.
        foreach (Rune rune in text.EnumerateRunes())
        {
            int digit = DecimalValue(rune.Value);
            if (digit < 0)
            {
                return false;
            }

            value = (value * 10) + digit;
        }

        return true;
    }

    /// <summary>
    /// CPython's <c>Py_UNICODE_TODECIMAL</c>: a codepoint's decimal value, or -1 if it has none.
    /// </summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns>0 to 9, or -1.</returns>
    /// <remarks>
    /// <para>
    /// Upstream's tables carry <c>Numeric_Type</c> but no numeric <i>value</i>, so the value is
    /// derived from where the codepoint sits in its own run of decimal digits. Every script's
    /// digits are ten consecutive codepoints starting at its zero; a few runs abut, so taking the
    /// offset from the start of the maximal run modulo ten is what recovers the digit - the five
    /// mathematical digit blocks U+1D7CE-U+1D7FF are one 50-long run, and U+1D7F9 is a 3.
    /// </para>
    /// <para>
    /// Verified exhaustively against <see cref="CharUnicodeInfo.GetDecimalDigitValue(string, int)"/>
    /// for every codepoint that table knows, by
    /// <c>Gaps/Unicode/PythonStrTests.Every_decimal_digits_value_matches_dotnets_table</c>.
    /// </para>
    /// </remarks>
    internal static int DecimalValue(int codepoint)
    {
        if (!Encodings.HasProperty(_codes.Value.NumericDecimal, (uint)codepoint))
        {
            return -1;
        }

        int start = codepoint;
        while (start > 0 && Encodings.HasProperty(_codes.Value.NumericDecimal, (uint)(start - 1)))
        {
            start--;
        }

        return (codepoint - start) % 10;
    }

    /// <summary>Python's <c>str.isidentifier</c>, as upstream calls it.</summary>
    /// <param name="text">The string.</param>
    /// <returns><see langword="true"/> if it is a legal Python identifier.</returns>
    internal static bool IsIdentifier(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // The length check is not redundant: TryGetRuneAt throws rather than answering false for
        // index 0 of "".
        if (text.Length == 0 || !Rune.TryGetRuneAt(text, 0, out Rune first) || !IsIdentifierStart(first.Value))
        {
            return false;
        }

        return text.EnumerateRunes().Skip(1).All(rune => IsIdentifierContinue(rune.Value));
    }

    /// <summary>
    /// Python's <c>str.lower()</c>, as <c>Sequence._fix_full_casefold</c> calls it
    /// (<c>upstream/regex/_regex_core.py</c> line 3643).
    /// </summary>
    /// <param name="codepoints">The codepoints to lowercase.</param>
    /// <returns>The lowercased codepoints. One codepoint can become more than one.</returns>
    /// <remarks>
    /// <para>
    /// One codepoint at a time, from <see cref="UnicodeLowercase"/>, which
    /// <c>tools/build-lowercase.py</c> builds from the Unicode 17.0.0 UCD and checks against the
    /// host CPython's <c>str.lower()</c> for all 1,109,309 codepoints that host knows.
    /// </para>
    /// <para>
    /// CPython's <c>str.lower</c> has one context-sensitive rule, final sigma, which this does not
    /// implement. It is unreachable here: the only caller lowercases text that has already been
    /// through <c>fold_case</c>, and no codepoint folds to U+03A3 - measured over all 1,114,112
    /// codepoints against the local oracle, 2026-08-30. The rule fires on nothing else.
    /// </para>
    /// </remarks>
    internal static int[] Lower(ReadOnlySpan<int> codepoints)
    {
        List<int> lowered = new(codepoints.Length);
        foreach (int codepoint in codepoints)
        {
            int index = Array.BinarySearch(UnicodeLowercase.From, codepoint);
            if (index >= 0)
            {
                lowered.Add(UnicodeLowercase.To[index]);
                continue;
            }

            int expanding = Array.FindIndex(UnicodeLowercase.Expanding, entry => entry.From == codepoint);
            if (expanding >= 0)
            {
                lowered.AddRange(UnicodeLowercase.Expanding[expanding].To);
                continue;
            }

            lowered.Add(codepoint);
        }

        return [.. lowered];
    }

    private static Codes ResolveCodes()
    {
        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();

        // Resolved through the property dictionary rather than written as literals, so the ids
        // cannot drift out of step with the tables on an upstream Unicode bump.
        return new Codes(
            Code(properties, "GENERALCATEGORY", "L"),
            Code(properties, "NUMERICTYPE", "DECIMAL"),
            Code(properties, "NUMERICTYPE", "DIGIT"),
            Code(properties, "XIDSTART", "Y"),
            Code(properties, "XIDCONTINUE", "Y")
        );
    }

    private static uint Code(IReadOnlyDictionary<string, PropertyEntry> properties, string property, string value)
    {
        PropertyEntry entry = properties[property];
        return ((uint)entry.Id << 16) | (uint)entry.Values[value];
    }

    private sealed record Codes(uint Letter, uint NumericDecimal, uint NumericDigit, uint XidStart, uint XidContinue);
}
