using System.Buffers.Binary;

namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// NOT UPSTREAM (D141): what every property answers for the 128 ASCII codepoints, as
/// <c>PROPERTY</c> and as <c>PROPERTY_IGN</c>, so compiling <c>\w</c> or <c>\p{Lu}</c> copies 16
/// bytes rather than asking a predicate 128 times. The data is in
/// <c>AsciiPropertyAnswers.g.cs</c>, generated from the predicates themselves by
/// <c>tests/FuzzyRegex.Tests/Gaps/Unicode/AsciiPropertyAnswersTableTests.cs</c>, which also fails
/// when the data no longer matches them.
/// </summary>
/// <remarks>
/// 1,459 property codes, two answer sets each, but only 104 distinct sets (measured 2026-11-05 on
/// the Unicode 18.0.0 tables), so a slot holds a byte index into <c>Tables</c>: about 4.8 KB in
/// all, in the assembly's read-only data. Every array is bytes, wider values little-endian: a
/// <see cref="ReadOnlySpan{T}"/> of bytes points straight at that data, where one of
/// <see cref="ushort"/> or <see cref="ulong"/> goes through <c>RuntimeHelpers.CreateSpan</c>, which
/// allocated 144 B a lookup in an unoptimised (Debug) build (measured 2026-11-05; 0 B in Release).
/// </remarks>
internal static partial class AsciiPropertyAnswers
{
    /// <summary>Copies a property's ASCII answers into <paramref name="answers"/>.</summary>
    /// <param name="ignoreCase"><see langword="true"/> for <c>PROPERTY_IGN</c>'s answers.</param>
    /// <param name="property">The packed property code: the id in the high 16 bits, the value in the low 16.</param>
    /// <param name="answers">
    /// Two elements; receives bit <c>ch &amp; 63</c> of element <c>ch &gt;&gt; 6</c> for each ASCII
    /// <c>ch</c> that has the property.
    /// </param>
    /// <returns><see langword="false"/>, with <paramref name="answers"/> untouched, if the table has no slot for the code.</returns>
    internal static bool TryGet(bool ignoreCase, uint property, Span<ulong> answers)
    {
        uint id = property >> 16;
        if (id >= PropertyCount)
        {
            return false;
        }

        int slot = BinaryPrimitives.ReadUInt16LittleEndian(FirstSlot[(int)(2 * id)..]) + (int)(property & 0xFFFF);
        if (slot >= BinaryPrimitives.ReadUInt16LittleEndian(FirstSlot[(int)(2 * id + 2)..]))
        {
            return false;
        }

        ReadOnlySpan<byte> table = Tables[(16 * (ignoreCase ? IgnoreCaseTableIndex : PlainTableIndex)[slot])..];
        answers[0] = BinaryPrimitives.ReadUInt64LittleEndian(table);
        answers[1] = BinaryPrimitives.ReadUInt64LittleEndian(table[8..]);
        return true;
    }
}
