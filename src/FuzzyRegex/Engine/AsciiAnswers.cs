using System.Diagnostics;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM (D141): a one-character test's <see cref="Node.AsciiAnswers"/> without asking its
/// predicate 128 times. A case-insensitive character or range is answered by a formula, a property
/// by the committed table <see cref="AsciiPropertyAnswers"/>. A set is not answered here: its
/// answers depend on its members, and <see cref="Matcher.FillAsciiAnswers"/> asks its predicate.
/// </summary>
/// <remarks>
/// <para>
/// Both formulas rest on one fact about the case tables: the cases of an ASCII codepoint are
/// itself, the other case of an ASCII letter and, under the Unicode encoding only, U+017F LATIN
/// SMALL LETTER LONG S (a case of <c>s</c> and <c>S</c>) and U+212A KELVIN SIGN (of <c>k</c> and
/// <c>K</c>). Nothing else. <c>AsciiAnswerFormulaTests</c> pins that fact against the tables and
/// checks each formula against its predicate: every codepoint for a character, every distinct
/// range for a range.
/// </para>
/// <para>
/// Measured 2026-11-05 by a probe: a formula takes 2-6 ns where asking the predicate about 0-127
/// takes 300-1,060 ns.
/// </para>
/// </remarks>
internal static class AsciiAnswers
{
    /// <summary>The ASCII capitals, as bits of the second element: <c>A</c> (65) is bit 1.</summary>
    private const ulong _capitals = 0x0000_0000_07FF_FFFEUL;

    /// <summary>The ASCII small letters, as bits of the second element: <c>a</c> (97) is bit 33.</summary>
    private const ulong _smallLetters = _capitals << 32;

    /// <summary>U+017F LATIN SMALL LETTER LONG S, a case of <c>s</c> under the Unicode encoding.</summary>
    private const uint _longS = 0x017F;

    /// <summary>U+212A KELVIN SIGN, a case of <c>k</c> under the Unicode encoding.</summary>
    private const uint _kelvinSign = 0x212A;

    /// <summary>
    /// Fills <paramref name="answers"/> for a case-insensitive character, a case-insensitive range
    /// or a property, without asking the node's predicate.
    /// </summary>
    /// <param name="node">A node of a pattern whose compilation is otherwise complete.</param>
    /// <param name="answers">
    /// Two zeroed elements; receives bit <c>ch &amp; 63</c> of element <c>ch &gt;&gt; 6</c> for each
    /// ASCII <c>ch</c> the predicate accepts.
    /// </param>
    /// <returns>
    /// <see langword="false"/>, with <paramref name="answers"/> untouched, for a set, or for a
    /// property code the table does not hold; the caller then asks the predicate.
    /// </returns>
    internal static bool TryFill(Node node, Span<ulong> answers)
    {
        switch (node.Op)
        {
            case Opcode.CharacterIgn or Opcode.CharacterIgnRev:
                CharacterIgn(node.Encoding, node.Values[0], answers);
                return true;
            case Opcode.RangeIgn or Opcode.RangeIgnRev:
                RangeIgn(node.Encoding, node.Values[0], node.Values[1], answers);
                return true;

            // The encoding is left out: the ASCII encoding clamps only codepoints above 127
            // (Encodings.ClampToEncoding), so it cannot change an ASCII answer, and the table is
            // checked under both.
            case Opcode.Property
            or Opcode.PropertyRev:
                return AsciiPropertyAnswers.TryGet(ignoreCase: false, node.Values[0], answers);
            case Opcode.PropertyIgn or Opcode.PropertyIgnRev:
                return AsciiPropertyAnswers.TryGet(ignoreCase: true, node.Values[0], answers);
            default:
                return false;
        }
    }

    /// <summary>A property node's answers as an array shared by every node of that property.</summary>
    /// <param name="node">A node of a pattern whose compilation is otherwise complete.</param>
    /// <returns>The array, which nothing may write, or <see langword="null"/> for any other node.</returns>
    internal static ulong[]? Shared(Node node) =>
        node.Op switch
        {
            Opcode.Property or Opcode.PropertyRev => AsciiPropertyAnswers.Shared(ignoreCase: false, node.Values[0]),
            Opcode.PropertyIgn or Opcode.PropertyIgnRev => AsciiPropertyAnswers.Shared(
                ignoreCase: true,
                node.Values[0]
            ),
            _ => null,
        };

    /// <summary>
    /// The ASCII answers of <see cref="Matcher.SameCharIgn"/> with <paramref name="character"/>:
    /// the character itself if ASCII, with its other case if a letter; for LONG S and KELVIN SIGN
    /// under the Unicode encoding, the two ASCII letters they are a case of; otherwise none.
    /// </summary>
    /// <param name="encoding">The node's encoding, ASCII or Unicode.</param>
    /// <param name="character">The node's codepoint.</param>
    /// <param name="answers">Two zeroed elements, as <see cref="TryFill"/> says.</param>
    internal static void CharacterIgn(CaseEncoding encoding, uint character, Span<ulong> answers)
    {
        Debug.Assert(encoding != CaseEncoding.Locale, "a LOCALE node keeps no ASCII answers");

        if (character < 128)
        {
            Set(answers, character);
            if (IsAsciiLetter(character))
            {
                Set(answers, character ^ 0x20);
            }

            return;
        }

        if (encoding != CaseEncoding.Unicode)
        {
            return;
        }

        if (character == _longS)
        {
            Set(answers, 's');
            Set(answers, 'S');
        }
        else if (character == _kelvinSign)
        {
            Set(answers, 'k');
            Set(answers, 'K');
        }
    }

    /// <summary>
    /// The ASCII answers of <see cref="Matcher.InRangeIgn"/> over
    /// <paramref name="lower"/>-<paramref name="upper"/>: the ASCII codepoints in the range, the
    /// other case of each ASCII letter among them, and under the Unicode encoding <c>s</c> and
    /// <c>S</c> if the range holds LONG S and <c>k</c> and <c>K</c> if it holds KELVIN SIGN.
    /// </summary>
    /// <param name="encoding">The node's encoding, ASCII or Unicode.</param>
    /// <param name="lower">The range's first codepoint.</param>
    /// <param name="upper">The range's last codepoint; below <paramref name="lower"/>, the range is empty.</param>
    /// <param name="answers">Two zeroed elements, as <see cref="TryFill"/> says.</param>
    internal static void RangeIgn(CaseEncoding encoding, uint lower, uint upper, Span<ulong> answers)
    {
        Debug.Assert(encoding != CaseEncoding.Locale, "a LOCALE node keeps no ASCII answers");

        answers[0] |= RangeBits(lower, upper, 0);

        // Every ASCII letter is in the second element, and its other case is 32 bits away.
        ulong second = RangeBits(lower, upper, 64);
        answers[1] |= second | ((second & _capitals) << 32) | ((second & _smallLetters) >> 32);

        if (encoding != CaseEncoding.Unicode)
        {
            return;
        }

        if (lower <= _longS && _longS <= upper)
        {
            Set(answers, 's');
            Set(answers, 'S');
        }

        if (lower <= _kelvinSign && _kelvinSign <= upper)
        {
            Set(answers, 'k');
            Set(answers, 'K');
        }
    }

    /// <summary>The bits of <paramref name="lower"/>-<paramref name="upper"/> within the 64 codepoints from <paramref name="first"/>.</summary>
    /// <param name="lower">The range's first codepoint.</param>
    /// <param name="upper">The range's last codepoint.</param>
    /// <param name="first">0 or 64.</param>
    /// <returns>Bit <c>ch - first</c> for each <c>ch</c> of the 64 that the range holds.</returns>
    private static ulong RangeBits(uint lower, uint upper, uint first)
    {
        uint from = Math.Max(lower, first);
        uint to = Math.Min(upper, first + 63);
        return from > to ? 0 : (ulong.MaxValue >> (int)(63 - (to - from))) << (int)(from - first);
    }

    private static bool IsAsciiLetter(uint ch) => (ch | 0x20) - 'a' <= 'z' - 'a';

    private static void Set(Span<ulong> answers, uint ch) => answers[(int)(ch >> 6)] |= 1UL << (int)ch;
}
