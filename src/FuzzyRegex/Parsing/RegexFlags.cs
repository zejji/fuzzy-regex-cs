namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Upstream's <c>RegexFlag</c> bit values and the flag masks derived from them
/// (<c>upstream/regex/_regex_core.py</c> lines 73-207).
/// </summary>
/// <remarks>
/// <para>
/// Every upstream bit is here, including the six the public <see cref="FuzzyRegexOptions"/> does
/// not expose - <c>ASCII</c>, <c>LOCALE</c>, <c>UNICODE</c>, <c>WORD</c>, <c>DEBUG</c> and
/// <c>TEMPLATE</c>. The parser needs them internally: <c>(?a)</c>, <c>(?L)</c>, <c>(?u)</c> and
/// <c>(?w)</c> are inline flags a pattern can set for itself, and <c>_main._compile</c> ORs
/// <c>UNICODE</c> into a <c>str</c> pattern's flags before compiling
/// (<c>upstream/regex/_main.py</c> lines 570-574).
/// </para>
/// <para>
/// The values match <see cref="FuzzyRegexOptions"/> where both have a name for the same bit; that
/// is why the public enum carries upstream's numbers rather than the built-in
/// <c>RegexOptions</c>'s.
/// </para>
/// </remarks>
internal static class RegexFlags
{
    /// <summary>Upstream <c>TEMPLATE</c> / <c>T</c>.</summary>
    internal const int Template = 0x1;

    /// <summary>Upstream <c>IGNORECASE</c> / <c>I</c>.</summary>
    internal const int IgnoreCase = 0x2;

    /// <summary>Upstream <c>LOCALE</c> / <c>L</c>.</summary>
    internal const int Locale = 0x4;

    /// <summary>Upstream <c>MULTILINE</c> / <c>M</c>.</summary>
    internal const int Multiline = 0x8;

    /// <summary>Upstream <c>DOTALL</c> / <c>S</c>.</summary>
    internal const int DotAll = 0x10;

    /// <summary>Upstream <c>UNICODE</c> / <c>U</c>.</summary>
    internal const int Unicode = 0x20;

    /// <summary>Upstream <c>VERBOSE</c> / <c>X</c>.</summary>
    internal const int Verbose = 0x40;

    /// <summary>Upstream <c>ASCII</c> / <c>A</c>.</summary>
    internal const int Ascii = 0x80;

    /// <summary>Upstream <c>VERSION1</c> / <c>V1</c>.</summary>
    internal const int Version1 = 0x100;

    /// <summary>Upstream <c>DEBUG</c> / <c>D</c>.</summary>
    internal const int Debug = 0x200;

    /// <summary>Upstream <c>REVERSE</c> / <c>R</c>.</summary>
    internal const int Reverse = 0x400;

    /// <summary>Upstream <c>WORD</c> / <c>W</c>.</summary>
    internal const int Word = 0x800;

    /// <summary>Upstream <c>BESTMATCH</c> / <c>B</c>.</summary>
    internal const int BestMatch = 0x1000;

    /// <summary>Upstream <c>VERSION0</c> / <c>V0</c>.</summary>
    internal const int Version0 = 0x2000;

    /// <summary>Upstream <c>FULLCASE</c> / <c>F</c>.</summary>
    internal const int FullCase = 0x4000;

    /// <summary>Upstream <c>ENHANCEMATCH</c> / <c>E</c>.</summary>
    internal const int EnhanceMatch = 0x8000;

    /// <summary>Upstream <c>POSIX</c> / <c>P</c>.</summary>
    internal const int Posix = 0x10000;

    /// <summary>Upstream <c>_ALL_VERSIONS</c> (line 163).</summary>
    internal const int AllVersions = Version0 | Version1;

    /// <summary>Upstream <c>_ALL_ENCODINGS</c> (line 164).</summary>
    internal const int AllEncodings = Ascii | Locale | Unicode;

    /// <summary>Upstream <c>GLOBAL_FLAGS</c> (lines 170-171): flags that apply to the whole pattern.</summary>
    internal const int GlobalFlags = AllVersions | BestMatch | Debug | EnhanceMatch | Posix | Reverse;

    /// <summary>Upstream <c>SCOPED_FLAGS</c> (lines 172-173): flags a <c>(?...)</c> group can scope.</summary>
    internal const int ScopedFlags = FullCase | IgnoreCase | Multiline | DotAll | Word | Verbose | AllEncodings;

    /// <summary>Upstream <c>CASE_FLAGS</c> (line 199).</summary>
    internal const int CaseFlags = FullCase | IgnoreCase;

    /// <summary>Upstream <c>NOCASE</c> (line 200).</summary>
    internal const int NoCase = 0;

    /// <summary>Upstream <c>FULLIGNORECASE</c> (line 201).</summary>
    internal const int FullIgnoreCase = FullCase | IgnoreCase;

    /// <summary>Upstream <c>FULL_CASE_FOLDING</c> (line 203).</summary>
    internal const int FullCaseFolding = Unicode | FullIgnoreCase;

    /// <summary>Upstream <c>ASCII_ENCODING</c> (line 4602), the encoding tag a property carries.</summary>
    internal const int AsciiEncoding = 1;

    /// <summary>Upstream <c>UNICODE_ENCODING</c> (line 4603).</summary>
    internal const int UnicodeEncoding = 2;

    /// <summary>
    /// Upstream <c>UNLIMITED</c> (line 190), the repeat count that means infinity:
    /// <c>(1 &lt;&lt; BITS_PER_CODE) - 1</c> where <c>BITS_PER_CODE</c> is
    /// <c>_regex.get_code_size() * 8</c>. Measured against the built oracle on 2026-08-30:
    /// <c>regex._regex.get_code_size()</c> is 4, so this is <c>0xFFFFFFFF</c>.
    /// </summary>
    internal const long Unlimited = uint.MaxValue;

    /// <summary>
    /// Upstream <c>REGEX_FLAGS</c> (lines 193-196): the inline flag letters. <c>V0</c> and
    /// <c>V1</c> are two characters; every other key is one.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, int> InlineFlags = new Dictionary<string, int>(
        StringComparer.Ordinal
    )
    {
        ["a"] = Ascii,
        ["b"] = BestMatch,
        ["e"] = EnhanceMatch,
        ["f"] = FullCase,
        ["i"] = IgnoreCase,
        ["L"] = Locale,
        ["m"] = Multiline,
        ["p"] = Posix,
        ["r"] = Reverse,
        ["s"] = DotAll,
        ["u"] = Unicode,
        ["V0"] = Version0,
        ["V1"] = Version1,
        ["w"] = Word,
        ["x"] = Verbose,
    };

    /// <summary>
    /// Upstream <c>HEX_ESCAPES</c> (line 209): how many hex digits each escape takes.
    /// </summary>
    internal static readonly IReadOnlyDictionary<char, int> HexEscapes = new Dictionary<char, int>
    {
        ['x'] = 2,
        ['u'] = 4,
        ['U'] = 8,
    };

    /// <summary>
    /// Upstream <c>CHARACTER_ESCAPES</c> (lines 4592-4600): the alphabetic escapes that stand for
    /// a single control character.
    /// </summary>
    internal static readonly IReadOnlyDictionary<char, char> CharacterEscapes = new Dictionary<char, char>
    {
        ['a'] = '\a',
        ['b'] = '\b',
        ['f'] = '\f',
        ['n'] = '\n',
        ['r'] = '\r',
        ['t'] = '\t',
        ['v'] = '\v',
    };

    /// <summary>
    /// Upstream <c>DEFAULT_FLAGS</c> (line 167): the flags each version turns on by itself.
    /// </summary>
    /// <param name="version">The resolved version bit.</param>
    /// <returns>The flags that version implies.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="version"/> is not exactly one version. Upstream's dictionary lookup raises
    /// <c>KeyError</c> here and lets it escape <c>_compile</c>; measured against the local oracle
    /// on 2026-08-30, <c>regex.compile('x', regex.V0|regex.V1)</c> raises
    /// <c>KeyError: regex.V0|V1</c>, not the "mutually incompatible" <c>ValueError</c>, because
    /// <c>Info.__init__</c> runs before that check.
    /// </exception>
    internal static int DefaultFlags(int version) =>
        version switch
        {
            Version0 => 0,
            Version1 => FullCase,
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "not a single version flag"),
        };

    /// <summary>
    /// Upstream <c>CASE_FLAGS_COMBINATIONS</c> (lines 205-206): normalises a case-flag pair, in
    /// particular collapsing a lone <c>FULLCASE</c> (which means nothing without
    /// <c>IGNORECASE</c>) to <c>NOCASE</c>.
    /// </summary>
    /// <param name="caseFlags">The case flags to normalise; only <see cref="CaseFlags"/> bits are valid.</param>
    /// <returns>The normalised case flags.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="caseFlags"/> carries a bit outside <see cref="CaseFlags"/>. Upstream's
    /// dictionary lookup raises <c>KeyError</c> in the same situation.
    /// </exception>
    internal static int CaseFlagsCombination(int caseFlags) =>
        caseFlags switch
        {
            NoCase => NoCase,
            FullCase => NoCase,
            IgnoreCase => IgnoreCase,
            FullIgnoreCase => FullIgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(caseFlags), caseFlags, "not a case-flag combination"),
        };

    /// <summary>
    /// Upstream <c>ALPHA</c> (line 175): Python's <c>string.ascii_letters</c>, so ASCII only.
    /// </summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it is an ASCII letter.</returns>
    internal static bool IsAlpha(int ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    /// <summary>Upstream <c>DIGITS</c> (line 176): Python's <c>string.digits</c>.</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it is an ASCII digit.</returns>
    internal static bool IsDigit(int ch) => ch is >= '0' and <= '9';

    /// <summary>Upstream <c>ALNUM</c> (line 177).</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it is an ASCII letter or digit.</returns>
    internal static bool IsAlnum(int ch) => IsAlpha(ch) || IsDigit(ch);

    /// <summary>Upstream <c>OCT_DIGITS</c> (line 178): Python's <c>string.octdigits</c>.</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it is an octal digit.</returns>
    internal static bool IsOctDigit(int ch) => ch is >= '0' and <= '7';

    /// <summary>Upstream <c>HEX_DIGITS</c> (line 179): Python's <c>string.hexdigits</c>.</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it is a hexadecimal digit.</returns>
    internal static bool IsHexDigit(int ch) => IsDigit(ch) || ch is >= 'a' and <= 'f' or >= 'A' and <= 'F';

    /// <summary>Upstream <c>NAMED_CHAR_PART</c> (line 181): what may appear in <c>\N{...}</c>.</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it may appear in a character name.</returns>
    internal static bool IsNamedCharPart(int ch) => IsAlnum(ch) || ch is ' ' or '-';

    /// <summary>Upstream <c>PROPERTY_NAME_PART</c> (line 182).</summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if it may appear in a property name.</returns>
    internal static bool IsPropertyNamePart(int ch) => IsAlnum(ch) || ch is ' ' or '&' or '_' or '-' or '.';

    /// <summary>
    /// Upstream <c>SPECIAL_CHARS</c> (line 180). The frozenset unions in the empty string, which is
    /// what <c>Source.get</c> returns at the end of the pattern, so
    /// <see cref="Source.EndOfSource"/> is a member here too - that is how
    /// <c>parse_sequence</c>'s loop terminates.
    /// </summary>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if the character is special outside a set.</returns>
    internal static bool IsSpecial(int ch) =>
        ch
            is Source.EndOfSource
                or '('
                or ')'
                or '|'
                or '?'
                or '*'
                or '+'
                or '{'
                or '^'
                or '$'
                or '.'
                or '['
                or '\\'
                or '#';
}
