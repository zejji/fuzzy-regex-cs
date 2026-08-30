namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// The four generated lookup functions whose bodies do not fit the three-level table walk that
/// <c>tools/transliterate-unicode.py</c> transliterates automatically.
/// </summary>
/// <remarks>
/// <para>
/// Upstream <c>re_get_script_extensions</c>, <c>re_get_all_cases</c>,
/// <c>re_get_simple_case_folding</c> and <c>re_get_full_case_folding</c>
/// (<c>upstream/src/_regex_unicode.c</c> lines 26045, 30627, 30948 and 31452). Each walks the same
/// three levels as the other hundred, then reads a struct array or a run-terminated list instead of
/// returning a bit.
/// </para>
/// <para>
/// The generated struct arrays are flattened into one array per field
/// (<c>AllCasesTable4Delta</c>, <c>AllCasesTable4Others</c>, <c>FullFoldingTable4Data</c>), so the
/// field order cannot be transposed by accident. The transliterator pins these four C bodies by
/// exact text: an upstream change to any of them fails the script rather than passing silently.
/// </para>
/// </remarks>
internal static partial class UnicodeTables
{
    /// <summary>Upstream <c>re_get_all_cases</c> (line 30627).</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <param name="cases">
    /// Receives the codepoint and its other simple cases. Must hold <see cref="MaxCases"/>.
    /// </param>
    /// <returns>How many entries of <paramref name="cases"/> were written.</returns>
    internal static int GetAllCases(uint codepoint, Span<uint> cases)
    {
        uint field_2 = codepoint >> 10;
        uint field_1 = (codepoint >> 5) & 0x1F;
        uint field_0 = codepoint & 0x1F;

        uint v = AllCasesTable1[(int)field_2];
        v = AllCasesTable2[(int)((v << 5) | field_1)];
        v = AllCasesTable3[(int)((v << 5) | field_0)];

        cases[0] = codepoint;

        if (AllCasesTable4Delta[v] == 0)
        {
            return 1;
        }

        cases[1] = codepoint ^ AllCasesTable4Delta[v];

        // others[] has RE_MAX_CASES - 1 slots; upstream reads the first two of them.
        if (AllCasesTable4Others[(v * 3) + 0] == 0)
        {
            return 2;
        }

        cases[2] = AllCasesTable4Others[(v * 3) + 0];

        if (AllCasesTable4Others[(v * 3) + 1] == 0)
        {
            return 3;
        }

        cases[3] = AllCasesTable4Others[(v * 3) + 1];

        return 4;
    }

    /// <summary>Upstream <c>re_get_simple_case_folding</c> (line 30948).</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <returns>The simple case folding of the codepoint.</returns>
    internal static uint GetSimpleCaseFolding(uint codepoint)
    {
        uint field_2 = codepoint >> 10;
        uint field_1 = (codepoint >> 5) & 0x1F;
        uint field_0 = codepoint & 0x1F;

        uint v = SimpleFoldingTable1[(int)field_2];
        v = SimpleFoldingTable2[(int)((v << 5) | field_1)];
        v = SimpleFoldingTable3[(int)((v << 5) | field_0)];

        return codepoint ^ SimpleFoldingTable4[v];
    }

    /// <summary>Upstream <c>re_get_full_case_folding</c> (line 31452).</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <param name="folded">
    /// Receives the full case folding. Must hold <see cref="MaxFolded"/>.
    /// </param>
    /// <returns>How many entries of <paramref name="folded"/> were written.</returns>
    internal static int GetFullCaseFolding(uint codepoint, Span<uint> folded)
    {
        uint field_2 = codepoint >> 10;
        uint field_1 = (codepoint >> 5) & 0x1F;
        uint field_0 = codepoint & 0x1F;

        uint v = FullFoldingTable1[(int)field_2];
        v = FullFoldingTable2[(int)((v << 5) | field_1)];
        v = FullFoldingTable3[(int)((v << 5) | field_0)];

        // data[0] is a delta from the codepoint; data[1] and data[2] are absolute.
        uint data = v * 3;
        folded[0] = codepoint ^ FullFoldingTable4Data[data];

        if (FullFoldingTable4Data[data + 1] == 0)
        {
            return 1;
        }

        folded[1] = FullFoldingTable4Data[data + 1];

        if (FullFoldingTable4Data[data + 2] == 0)
        {
            return 2;
        }

        folded[2] = FullFoldingTable4Data[data + 2];

        return 3;
    }

    /// <summary>Upstream <c>re_get_script_extensions</c> (line 26045).</summary>
    /// <param name="codepoint">The codepoint.</param>
    /// <param name="scripts">
    /// Receives the script identifiers. Must hold <see cref="MaxScx"/>.
    /// </param>
    /// <returns>How many entries of <paramref name="scripts"/> were written.</returns>
    internal static int GetScriptExtensions(uint codepoint, Span<byte> scripts)
    {
        uint field_2 = codepoint >> 10;
        uint field_1 = (codepoint >> 5) & 0x1F;
        uint field_0 = codepoint & 0x1F;

        uint v = ScriptExtensionsTable1[(int)field_2];
        v = ScriptExtensionsTable2[(int)((v << 5) | field_1)];
        v = ScriptExtensionsTable3[(int)((v << 5) | field_0)];

        // Below 176 the value is the single script itself; at or above it, an index into a
        // table of zero-terminated runs.
        if (v < 176)
        {
            scripts[0] = (byte)v;

            return 1;
        }

        int offset = ScriptExtensionsTable4[v - 176];
        int count = 0;

        do
        {
            scripts[count] = ScriptExtensionsTable5[offset + count];
            ++count;
        } while (ScriptExtensionsTable5[offset + count] != 0);

        return count;
    }
}
