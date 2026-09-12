namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Options that change how a pattern is compiled and matched.
/// </summary>
/// <remarks>
/// <para>
/// The bit values are upstream's, from <c>RegexFlag</c> in <c>upstream/regex/_regex_core.py</c>
/// lines 73-90, so that the parser port can compare against upstream's tables directly rather
/// than through a translation layer. Nothing casts between this and
/// <see cref="System.Text.RegularExpressions.RegexOptions"/>, so the numbers need not agree with
/// the built-in engine's.
/// </para>
/// <para>
/// Where a .NET name and an upstream name exist for the same behaviour, the .NET name is used
/// and the upstream spelling is given here, so a <c>System.Text.RegularExpressions</c> user needs
/// no new vocabulary. Upstream flags this port does not yet expose - <c>ASCII</c>, <c>LOCALE</c>,
/// <c>UNICODE</c>, <c>WORD</c>, <c>DEBUG</c> and <c>TEMPLATE</c> - are simply absent rather than
/// present and ignored. Every one of them can still be set from inside a pattern, as
/// <c>(?a)</c>, <c>(?L)</c>, <c>(?u)</c> and <c>(?w)</c>.
/// </para>
/// <para>
/// There is no <c>ExplicitCapture</c>. It has no upstream counterpart, so nothing in the
/// compile-parity corpus could verify it and it would have been the only unverified logic in the
/// parser; a caller who wants a non-capturing group writes <c>(?:...)</c>, as upstream's users do
/// (DECISIONS 2026-08-30, decision C).
/// </para>
/// </remarks>
[Flags]
public enum FuzzyRegexOptions
{
    /// <summary>No options: case-sensitive, single-line, left-to-right matching.</summary>
    None = 0x0,

    /// <summary>Case-insensitive matching. Upstream <c>IGNORECASE</c> / <c>I</c>.</summary>
    IgnoreCase = 0x2,

    /// <summary>
    /// <c>^</c> and <c>$</c> match at the start and end of any line, not just of the subject.
    /// Upstream <c>MULTILINE</c> / <c>M</c>.
    /// </summary>
    Multiline = 0x8,

    /// <summary>
    /// <c>.</c> matches any character including a newline. Upstream <c>DOTALL</c> / <c>S</c>;
    /// the .NET name is <c>Singleline</c>, which means the same thing.
    /// </summary>
    Singleline = 0x10,

    /// <summary>
    /// Unescaped whitespace in the pattern is ignored and <c>#</c> starts a comment. Upstream
    /// <c>VERBOSE</c> / <c>X</c>.
    /// </summary>
    IgnorePatternWhitespace = 0x40,

    /// <summary>
    /// Search backwards, from the end of the subject towards the start. Upstream
    /// <c>REVERSE</c> / <c>R</c>.
    /// </summary>
    RightToLeft = 0x400,

    /// <summary>
    /// Find the best fuzzy match rather than the first one. Upstream <c>BESTMATCH</c> / <c>B</c>.
    /// </summary>
    BestMatch = 0x1000,

    /// <summary>
    /// After finding a fuzzy match, try to improve its fit. Upstream <c>ENHANCEMATCH</c> /
    /// <c>E</c>.
    /// </summary>
    EnhanceMatch = 0x8000,

    /// <summary>
    /// Leftmost-longest (POSIX) matching instead of leftmost-first. Upstream <c>POSIX</c> /
    /// <c>P</c>.
    /// </summary>
    /// <remarks>
    /// The match still starts as far left as it can; the flag only decides between matches that
    /// start in the same place, taking the longest rather than the first the pattern happens to
    /// produce. So <c>a|ab|abc</c> matches <c>abc</c> in <c>"abcd"</c> under this flag and <c>a</c>
    /// without it.
    /// <para>
    /// <b>It can be much slower.</b> Ordinary matching stops at the first match it reaches; this
    /// one keeps backtracking through every remaining path so that it can be sure nothing longer
    /// exists. A pattern that answers instantly without the flag can take seconds with it - so set
    /// a <c>matchTimeout</c> on any pattern you did not write yourself.
    /// </para>
    /// </remarks>
    Posix = 0x10000,

    /// <summary>
    /// Use Unicode full case-folding when matching case-insensitively, so that (for example)
    /// <c>ß</c> matches <c>SS</c>. Upstream <c>FULLCASE</c> / <c>F</c>.
    /// </summary>
    FullCase = 0x4000,

    /// <summary>
    /// Legacy behaviour, compatible with <c>System.Text.RegularExpressions</c> and Python's
    /// <c>re</c>. Upstream <c>VERSION0</c> / <c>V0</c>.
    /// </summary>
    Version0 = 0x2000,

    /// <summary>
    /// Enhanced behaviour: nested sets, set operations, and the other mrab-regex extensions.
    /// Upstream <c>VERSION1</c> / <c>V1</c>. Not the default: upstream's effective <c>DEFAULT_VERSION</c> is <c>VERSION0</c> (set in <c>_main.py</c>), and so is this port's.
    /// </summary>
    Version1 = 0x100,
}
