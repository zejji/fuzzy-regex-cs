namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Scanner for the regular expression source string. Port of <c>Source</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 4110-4354).
/// </summary>
/// <remarks>
/// <para>
/// <c>char_type</c> and the <c>bytes</c> branch of upstream's constructor are not ported: this
/// port is <c>char</c>-based (design spec section 4).
/// </para>
/// <para>
/// <b>Two things differ from a line-by-line reading of the Python, both deliberate.</b>
/// </para>
/// <para>
/// First, upstream's "character" is a one-character <c>str</c> and the end of the pattern is the
/// empty string, which compares equal to nothing and is a member of <c>SPECIAL_CHARS</c>. Here a
/// character is an <see cref="int"/> codepoint and the end of the pattern is
/// <see cref="EndOfSource"/>, which <see cref="RegexFlags.IsSpecial"/> reports as special for the
/// same reason.
/// </para>
/// <para>
/// Second, <see cref="Pos"/> counts UTF-16 code units, as every public index in this port does
/// (AGENTS.md), while <see cref="Get"/> and <see cref="Peek"/> return whole codepoints: a
/// surrogate pair advances <see cref="Pos"/> by two and yields one character. The parser therefore
/// emits the same bytecode as upstream for a non-BMP literal, and
/// <see cref="FuzzyRegexParseException.Offset"/> stays a UTF-16 offset. Upstream's <c>pos</c> is a
/// codepoint offset, so the two agree exactly for a BMP-only pattern - which every pattern in the
/// compile-parity corpus is (S06).
/// </para>
/// </remarks>
internal sealed class Source
{
    /// <summary>What <see cref="Get"/> returns at the end of the pattern.</summary>
    /// <remarks>
    /// Upstream returns <c>string[:0]</c>, the empty string. -1 is used rather than 0 because
    /// <c>\0</c> is a legal pattern character and the corpus contains patterns made of it.
    /// </remarks>
    internal const int EndOfSource = -1;

    /// <summary>Initializes a scanner over the given pattern.</summary>
    /// <param name="text">The pattern text.</param>
    internal Source(string text)
    {
        String = text;
        Pos = 0;
        IgnoreSpace = false;
    }

    /// <summary>The pattern being scanned. Upstream <c>Source.string</c>.</summary>
    internal string String { get; }

    /// <summary>The scan position, in UTF-16 code units. Upstream <c>Source.pos</c>.</summary>
    internal int Pos { get; set; }

    /// <summary>
    /// Whether whitespace and <c>#</c> comments are skipped, which is what
    /// <see cref="FuzzyRegexOptions.IgnorePatternWhitespace"/> turns on. Upstream
    /// <c>Source.ignore_space</c>.
    /// </summary>
    internal bool IgnoreSpace { get; set; }

    /// <summary>Reads the next character without consuming it.</summary>
    /// <param name="overrideIgnore">Read the raw next character even when skipping whitespace.</param>
    /// <returns>The codepoint, or <see cref="EndOfSource"/> at the end of the pattern.</returns>
    internal int Peek(bool overrideIgnore = false)
    {
        int pos = Pos;

        if (IgnoreSpace && !overrideIgnore && SkipIgnorable(ref pos) != SkipOutcome.Found)
        {
            return EndOfSource;
        }

        return pos < String.Length ? CharacterAt(pos) : EndOfSource;
    }

    /// <summary>Reads and consumes the next character.</summary>
    /// <param name="overrideIgnore">Read the raw next character even when skipping whitespace.</param>
    /// <returns>The codepoint, or <see cref="EndOfSource"/> at the end of the pattern.</returns>
    internal int Get(bool overrideIgnore = false)
    {
        int pos = Pos;

        if (IgnoreSpace && !overrideIgnore)
        {
            switch (SkipIgnorable(ref pos))
            {
                case SkipOutcome.PastEnd:
                    // Upstream's IndexError branch: pos keeps whatever the skip reached.
                    Pos = pos;
                    return EndOfSource;
                case SkipOutcome.CommentToEnd:
                    // Upstream's ValueError branch: the comment ran off the end.
                    Pos = String.Length;
                    return EndOfSource;
                default:
                    break;
            }
        }

        if (pos >= String.Length)
        {
            Pos = pos;
            return EndOfSource;
        }

        int ch = CharacterAt(pos);
        Pos = pos + CharacterLength(ch);
        return ch;
    }

    /// <summary>
    /// Reads characters while <paramref name="test"/> agrees with <paramref name="include"/>.
    /// Upstream <c>get_while</c>.
    /// </summary>
    /// <param name="test">Whether a codepoint is in the set being scanned for.</param>
    /// <param name="include">Collect the characters the test accepts, or the ones it rejects.</param>
    /// <param name="keepSpaces">Do not skip whitespace even when <see cref="IgnoreSpace"/> is set.</param>
    /// <returns>The characters read, as they appear in the pattern.</returns>
    internal string GetWhile(Func<int, bool> test, bool include = true, bool keepSpaces = false)
    {
        string string_ = String;
        int pos = Pos;

        if (IgnoreSpace && !keepSpaces)
        {
            var substring = new System.Text.StringBuilder();

            while (true)
            {
                SkipOutcome outcome = SkipIgnorable(ref pos);
                if (outcome != SkipOutcome.Found)
                {
                    // Both of upstream's exception branches end the scan at the end of the string.
                    Pos = string_.Length;
                    return substring.ToString();
                }

                int ch = CharacterAt(pos);
                if (test(ch) != include)
                {
                    break;
                }

                substring.Append(string_, pos, CharacterLength(ch));
                pos += CharacterLength(ch);
            }

            Pos = pos;
            return substring.ToString();
        }

        int start = pos;
        while (pos < string_.Length)
        {
            int ch = CharacterAt(pos);
            if (test(ch) != include)
            {
                break;
            }

            pos += CharacterLength(ch);
        }

        Pos = pos;
        return string_[start..pos];
    }

    /// <summary>
    /// Consumes <paramref name="substring"/> if it is next. Upstream <c>match</c> - renamed
    /// because <c>Match</c> is a public type in this namespace.
    /// </summary>
    /// <param name="substring">The text to consume.</param>
    /// <returns><see langword="true"/> if it was there and has been consumed.</returns>
    internal bool MatchText(string substring)
    {
        string string_ = String;
        int pos = Pos;

        if (IgnoreSpace)
        {
            foreach (char c in substring)
            {
                if (SkipIgnorable(ref pos) != SkipOutcome.Found || string_[pos] != c)
                {
                    return false;
                }

                pos++;
            }

            Pos = pos;
            return true;
        }

        if (!string_.AsSpan(pos).StartsWith(substring, StringComparison.Ordinal))
        {
            return false;
        }

        Pos = pos + substring.Length;
        return true;
    }

    /// <summary>Consumes <paramref name="substring"/> or fails. Upstream <c>expect</c>.</summary>
    /// <param name="substring">The text that must be next.</param>
    /// <exception cref="FuzzyRegexParseException">It was not there.</exception>
    internal void Expect(string substring)
    {
        if (!MatchText(substring))
        {
            throw new FuzzyRegexParseException($"missing {substring}", String, Pos);
        }
    }

    /// <summary>Whether the whole pattern has been read. Upstream <c>at_end</c>.</summary>
    /// <returns><see langword="true"/> if nothing but ignorable text remains.</returns>
    internal bool AtEnd()
    {
        int pos = Pos;

        if (IgnoreSpace && SkipIgnorable(ref pos) != SkipOutcome.Found)
        {
            return true;
        }

        return pos >= String.Length;
    }

    /// <summary>
    /// Python's <c>str.isspace</c>, which is not <see cref="char.IsWhiteSpace(char)"/>: the four
    /// C0 separators U+001C to U+001F are whitespace to Python and not to .NET.
    /// </summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if Python would call it whitespace.</returns>
    /// <remarks>
    /// The full set is pinned by
    /// <c>Gaps.Parsing.PythonWhitespaceTests</c>, measured against CPython on 2026-08-30: 29
    /// codepoints, all in the BMP.
    /// </remarks>
    internal static bool IsSpace(int ch) =>
        ch is >= 0x1C and <= 0x1F || (ch <= char.MaxValue && ch >= 0 && char.IsWhiteSpace((char)ch));

    private int CharacterAt(int pos) =>
        char.IsHighSurrogate(String[pos]) && pos + 1 < String.Length && char.IsLowSurrogate(String[pos + 1])
            ? char.ConvertToUtf32(String[pos], String[pos + 1])
            : String[pos];

    private static int CharacterLength(int ch) => ch > char.MaxValue ? 2 : 1;

    /// <summary>
    /// Upstream's whitespace-and-comment skip loop, shared by every <c>Source</c> method that has
    /// one. The three outcomes are upstream's three control-flow exits: falling out of the loop,
    /// <c>IndexError</c> from indexing past the end, and <c>ValueError</c> from
    /// <c>string.index("\n", pos)</c> finding no newline.
    /// </summary>
    private SkipOutcome SkipIgnorable(ref int pos)
    {
        while (true)
        {
            if (pos >= String.Length)
            {
                return SkipOutcome.PastEnd;
            }

            char c = String[pos];
            if (IsSpace(c))
            {
                pos++;
            }
            else if (c == '#')
            {
                int newline = String.IndexOf('\n', pos);
                if (newline < 0)
                {
                    return SkipOutcome.CommentToEnd;
                }

                pos = newline;
            }
            else
            {
                return SkipOutcome.Found;
            }
        }
    }

    private enum SkipOutcome
    {
        Found,
        PastEnd,
        CommentToEnd,
    }
}
