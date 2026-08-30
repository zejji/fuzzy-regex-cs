using System.Globalization;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// The five functions upstream's C extension exports to the parser
/// (<c>upstream/src/_regex.c</c> lines 26126-26403; the table of them is at line 26406).
/// </summary>
/// <remarks>
/// <para>
/// <c>_regex_core.py</c> calls these as <c>_regex.fold_case</c>, <c>_regex.get_all_cases</c>,
/// <c>_regex.get_expand_on_folding</c>, <c>_regex.has_property_value</c> and
/// <c>_regex.get_properties</c>. The sixth, <c>get_code_size</c>, is already folded into
/// <see cref="RegexFlags.Unlimited"/> and has no other caller.
/// </para>
/// <para>
/// Upstream works in Python <c>str</c>s, which are sequences of codepoints. This port's parser is
/// codepoint-based for the same reason (<c>Source.Get</c> returns whole codepoints), so
/// <see cref="FoldCase"/> takes and returns codepoints rather than UTF-16 <see cref="string"/>s:
/// folding can map a BMP codepoint to a different BMP codepoint but never produces a surrogate
/// pair, and a codepoint array is what <c>Character.Folded</c> already holds.
/// </para>
/// </remarks>
internal static class RegexModule
{
    private static readonly Lazy<int[]> _expandOnFolding = new(() =>
    {
        ushort[] table = UnicodeTables.ExpandOnFolding;
        int[] result = new int[table.Length];
        for (int i = 0; i < table.Length; i++)
        {
            result[i] = table[i];
        }

        return result;
    });

    private static readonly Lazy<IReadOnlyDictionary<string, PropertyEntry>> _properties = new(BuildPropertyDictionary);

    /// <summary>
    /// Upstream <c>fold_case</c> (<c>upstream/src/_regex.c</c> line 26133): folds the case of a
    /// run of codepoints.
    /// </summary>
    /// <param name="flags">The flags in force. Without <c>IGNORECASE</c> the input is returned unchanged.</param>
    /// <param name="codepoints">The codepoints to fold.</param>
    /// <returns>The folded codepoints. Full folding can make the result longer than the input.</returns>
    internal static int[] FoldCase(int flags, ReadOnlySpan<int> codepoints)
    {
        if ((flags & RegexFlags.IgnoreCase) == 0)
        {
            return codepoints.ToArray();
        }

        CaseEncoding encoding = Encodings.Select(flags);

        if ((flags & RegexFlags.FullCase) == 0)
        {
            // Simple case-folding: one codepoint in, one codepoint out.
            int[] simple = new int[codepoints.Length];
            for (int i = 0; i < codepoints.Length; i++)
            {
                simple[i] = (int)Encodings.SimpleCaseFold(encoding, (uint)codepoints[i]);
            }

            return simple;
        }

        // Full case-folding: some single codepoints map to as many as RE_MAX_FOLDED.
        List<int> folded = new(codepoints.Length);
        Span<uint> buffer = stackalloc uint[UnicodeTables.MaxFolded];
        foreach (int codepoint in codepoints)
        {
            int count = Encodings.FullCaseFold(encoding, (uint)codepoint, buffer);
            for (int j = 0; j < count; j++)
            {
                folded.Add((int)buffer[j]);
            }
        }

        return [.. folded];
    }

    /// <summary>
    /// Upstream <c>get_expand_on_folding</c> (line 26285): the codepoints that grow longer under
    /// full case-folding.
    /// </summary>
    /// <returns>The codepoints, in upstream's table order.</returns>
    internal static IReadOnlyList<int> GetExpandOnFolding() => _expandOnFolding.Value;

    /// <summary>
    /// Upstream <c>has_property_value</c> (line 26327): whether a codepoint has a given value for
    /// a Unicode property.
    /// </summary>
    /// <param name="propertyValue">The packed property code.</param>
    /// <param name="character">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint has that value.</returns>
    internal static bool HasPropertyValue(uint propertyValue, uint character) =>
        Encodings.HasProperty(propertyValue, character);

    /// <summary>
    /// Upstream <c>get_all_cases</c> (line 26347): every simple case of a codepoint.
    /// </summary>
    /// <param name="flags">The flags in force.</param>
    /// <param name="character">The codepoint.</param>
    /// <returns>
    /// The cases, with a trailing <see langword="null"/> - upstream's <c>None</c> - when full
    /// case-folding is in force and the codepoint also expands under it.
    /// </returns>
    internal static int?[] GetAllCases(int flags, uint character)
    {
        CaseEncoding encoding = Encodings.Select(flags);

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        int count = Encodings.AllCases(encoding, character, cases);

        // If the character also expands on full case-folding, append a None.
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        bool expands =
            (flags & RegexFlags.FullCaseFolding) == RegexFlags.FullCaseFolding
            && Encodings.FullCaseFold(encoding, character, folded) > 1;

        int?[] result = new int?[count + (expands ? 1 : 0)];
        for (int i = 0; i < count; i++)
        {
            result[i] = (int)cases[i];
        }

        return result;
    }

    /// <summary>
    /// Upstream <c>get_properties</c> (line 26126), which hands back the dictionary
    /// <c>init_property_dict</c> built (line 26433). <c>_regex_core.PROPERTIES</c> is this.
    /// </summary>
    /// <returns>Munged property name to its id and its value set.</returns>
    internal static IReadOnlyDictionary<string, PropertyEntry> GetProperties() => _properties.Value;

    /// <summary>
    /// Upstream <c>munge_name</c> (<c>upstream/src/_regex.c</c> line 26418): drops spaces,
    /// underscores and hyphens and upper-cases the rest, keeping a leading hyphen.
    /// </summary>
    /// <param name="name">The property or value name.</param>
    /// <returns>The munged name.</returns>
    /// <remarks>
    /// C's <c>toupper</c> here runs in the C locale, so it upper-cases ASCII and nothing else.
    /// <see cref="CultureInfo.InvariantCulture"/> is the same mapping for the ASCII names these
    /// tables hold, and unlike the current culture it cannot turn <c>i</c> into <c>İ</c>.
    /// </remarks>
    internal static string MungeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        char[] munged = new char[name.Length];
        int length = 0;
        int start = 0;

        if (name.Length > 0 && name[0] == '-')
        {
            munged[length++] = '-';
            start = 1;
        }

        for (int i = start; i < name.Length; i++)
        {
            char ch = name[i];
            if (ch is ' ' or '_' or '-')
            {
                continue;
            }

            munged[length++] = char.ToUpper(ch, CultureInfo.InvariantCulture);
        }

        return new string(munged, 0, length);
    }

    /// <summary>Upstream <c>init_property_dict</c> (<c>upstream/src/_regex.c</c> line 26433).</summary>
    private static Dictionary<string, PropertyEntry> BuildPropertyDictionary()
    {
        // How many value sets are there?
        int valueSetCount = UnicodeTables.PropertyValuesValueSet.Max() + 1;

        // Build the property values dictionaries.
        var valueDicts = new Dictionary<string, int>[valueSetCount];
        for (int i = 0; i < UnicodeTables.PropertyValuesValueSet.Length; i++)
        {
            int valueSet = UnicodeTables.PropertyValuesValueSet[i];
            valueDicts[valueSet] ??= new Dictionary<string, int>(StringComparer.Ordinal);
            valueDicts[valueSet][MungeName(UnicodeTables.Strings[UnicodeTables.PropertyValuesName[i]])] =
                UnicodeTables.PropertyValuesId[i];
        }

        // Build the property dictionary.
        Dictionary<string, PropertyEntry> properties = new(StringComparer.Ordinal);
        for (int i = 0; i < UnicodeTables.PropertiesName.Length; i++)
        {
            properties[MungeName(UnicodeTables.Strings[UnicodeTables.PropertiesName[i]])] = new PropertyEntry(
                UnicodeTables.PropertiesId[i],
                valueDicts[UnicodeTables.PropertiesValueSet[i]]
            );
        }

        return properties;
    }
}

/// <summary>
/// One entry of upstream's property dictionary: the <c>(prop_id, value_dict)</c> tuple
/// <c>_regex_core.py</c> unpacks at lines 1745, 1765 and 1790.
/// </summary>
/// <param name="Id">The property's id, which becomes the high 16 bits of a packed property code.</param>
/// <param name="Values">Munged value name to value id.</param>
internal sealed record PropertyEntry(int Id, IReadOnlyDictionary<string, int> Values);
