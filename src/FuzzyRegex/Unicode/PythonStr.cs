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
