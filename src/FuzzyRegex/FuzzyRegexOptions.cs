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
/// no new vocabulary. Upstream flags this port does not expose - <c>LOCALE</c>, <c>DEBUG</c> and
/// <c>TEMPLATE</c> - are simply absent rather than present and ignored. <c>LOCALE</c> can still be
/// set from inside a pattern, as <c>(?L)</c>; it is left out here because it asks for the C
/// library's current locale, which .NET has no equivalent of and which no oracle row could pin.
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
    /// The character classes <c>\w</c>, <c>\W</c>, <c>\s</c>, <c>\S</c>, <c>\d</c>, <c>\D</c> and
    /// the word boundaries <c>\b</c>, <c>\B</c> cover the whole of Unicode. Upstream
    /// <c>UNICODE</c> / <c>U</c>, and its default for a text pattern; the inline form is
    /// <c>(?u)</c>.
    /// </summary>
    /// <remarks>
    /// <b>Already on.</b> Upstream's <c>_main._compile</c> ORs it into every text pattern that
    /// names no encoding (<c>upstream/regex/_main.py</c> lines 570-574), so this port does too and
    /// <see cref="FuzzyRegex.Options"/> reports it on every pattern. It is exposed so that a
    /// caller can say so explicitly and read it back, not because passing it changes anything.
    /// Combining it with <see cref="Ascii"/> is rejected, as upstream rejects it.
    /// </remarks>
    Unicode = 0x20,

    /// <summary>
    /// Unescaped whitespace in the pattern is ignored and <c>#</c> starts a comment. Upstream
    /// <c>VERBOSE</c> / <c>X</c>.
    /// </summary>
    IgnorePatternWhitespace = 0x40,

    /// <summary>
    /// The character classes <c>\w</c>, <c>\W</c>, <c>\s</c>, <c>\S</c>, <c>\d</c>, <c>\D</c> and
    /// the word boundaries <c>\b</c>, <c>\B</c> cover ASCII only, so everything above U+007F is
    /// answered as if it were unassigned. Upstream <c>ASCII</c> / <c>A</c>; the inline form is
    /// <c>(?a)</c>.
    /// </summary>
    /// <remarks>
    /// Mutually incompatible with <see cref="Unicode"/> - and with upstream's <c>LOCALE</c>, which
    /// this port does not expose - so passing both throws
    /// <see cref="FuzzyRegexParseException"/> with upstream's own message, "ASCII, LOCALE and
    /// UNICODE flags are mutually incompatible".
    /// </remarks>
    Ascii = 0x80,

    /// <summary>
    /// Search backwards, from the end of the subject towards the start. Upstream
    /// <c>REVERSE</c> / <c>R</c>.
    /// </summary>
    RightToLeft = 0x400,

    /// <summary>
    /// <c>\b</c> and <c>\B</c> use the default Unicode word-boundary rules (UAX #29) instead of
    /// the <c>\w</c>-to-<c>\W</c> transition. Upstream <c>WORD</c> / <c>W</c>; the inline form is
    /// <c>(?w)</c>.
    /// </summary>
    /// <remarks>
    /// The rules join a word across an apostrophe and across other characters a simple
    /// <c>\w</c> test would break on, so this changes what matches, not just how fast.
    /// Measured against <c>regex</c> 2026.9.10 on 2026-09-16:
    /// <c>regex.findall(r'\b\w+\b', "can't", regex.WORD)</c> is <c>[]</c> where the same call
    /// without the flag is <c>['can', 't']</c> - UAX #29 keeps <c>can't</c> whole, so no boundary
    /// sits where <c>\w+</c> has to stop.
    /// </remarks>
    Word = 0x800,

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
    /// <c>ß</c> matches <c>SS</c> and <c>ﬁ</c> matches <c>fi</c>. Upstream <c>FULLCASE</c> /
    /// <c>F</c>.
    /// </summary>
    /// <remarks>
    /// <b>Already on</b> under <see cref="Version1"/>, which is the default, so passing it changes
    /// nothing unless you also pass <see cref="Version0"/>. To turn it off, pass
    /// <see cref="Version0"/> or write <c>(?-f)</c> in the pattern. It does not turn
    /// case-insensitive matching on by itself; it changes what <see cref="IgnoreCase"/> means.
    /// </remarks>
    FullCase = 0x4000,

    /// <summary>
    /// Legacy behaviour, compatible with <c>System.Text.RegularExpressions</c> and Python's
    /// <c>re</c>: simple case-folding, and an unescaped <c>[</c> inside a set is a literal.
    /// Upstream <c>VERSION0</c> / <c>V0</c>, and upstream's own default.
    /// </summary>
    /// <remarks>
    /// Pass this to compile a pattern written for <c>Regex</c> or for <c>re</c> unchanged. The two
    /// live differences from <see cref="Version1"/> are the ones named above; measured on
    /// <c>regex</c> 2026.9.10 and .NET 10 (<c>tools/probes/upstream-version-defaults.py</c> and its
    /// <c>.ps1</c> twin), the zero-width and inline-flag differences upstream's README also lists
    /// no longer exist. There is a third, undocumented one: a backreference to a group that is
    /// still open is a compile error here and is accepted under <see cref="Version1"/>.
    /// </remarks>
    Version0 = 0x2000,

    /// <summary>
    /// <b>The default.</b> Nested sets and set operations (<c>[[a-z]--[aeiou]]</c>), and full case
    /// folding when matching case-insensitively. Upstream <c>VERSION1</c> / <c>V1</c>.
    /// </summary>
    /// <remarks>
    /// <b>This is the one place this library deliberately does not follow mrab-regex's default.</b>
    /// Upstream's front end sets <c>DEFAULT_VERSION = VERSION0</c> so that <c>regex</c> stays a
    /// drop-in replacement for Python's <c>re</c>; this library has no such users to protect, and
    /// the two behaviours version 1 adds are the reason to use it over
    /// <c>System.Text.RegularExpressions</c>. See <c>docs/DIVERGENCES.md</c>.
    /// <para>
    /// The one pattern that changes meaning is an unescaped <c>[</c> inside a set: <c>[[]</c> is a
    /// set containing <c>[</c> under <see cref="Version0"/> and an unterminated nested set here.
    /// The parse error says so and names both ways out.
    /// </para>
    /// </remarks>
    Version1 = 0x100,
}
