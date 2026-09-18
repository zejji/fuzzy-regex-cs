namespace Fuzzy.Text.RegularExpressions.Tests.Ported;

/// <summary>
/// The public surface of <see cref="FuzzyRegex"/>, compiled the way upstream's own test suite
/// compiles: with <c>DEFAULT_VERSION</c> = <c>VERSION0</c>. Every ported test goes through here.
/// </summary>
/// <remarks>
/// <para>
/// S50b made <see cref="FuzzyRegexOptions.Version1"/> this port's default (spec amendment 24).
/// <c>upstream/regex/tests/test_regex.py</c> is written against <c>_main.py</c>'s
/// <c>DEFAULT_VERSION = VERSION0</c> and never changes it, so a ported test run under the new
/// default would stop asking upstream's question - measurably, on set syntax and on
/// case-insensitive folding, which is what the two versions still differ on. Pinning the version
/// here keeps <c>docs/STATUS.md</c>'s parity figure a statement about upstream's behaviour rather
/// than about ours.
/// </para>
/// <para>
/// The version is passed as a <em>default</em>, not as a flag: a pattern that names its own
/// version still wins, so the 63 ported patterns spelling <c>(?V0)</c> or <c>(?V1)</c> and the
/// tests passing <see cref="FuzzyRegexOptions.Version0"/> or
/// <see cref="FuzzyRegexOptions.Version1"/> behave exactly as they did. Setting the flag instead
/// would leave both version bits on and be rejected as "VERSION0 and VERSION1 flags are mutually
/// incompatible".
/// </para>
/// <para>
/// <c>Conventions.PortedTestConventionsTests</c> fails if anything under <c>Ported/</c> reaches
/// <see cref="FuzzyRegex"/> directly, because a single missed call site is a test that silently
/// changed what it measures. A member this class does not expose yet is a member no ported test
/// has needed yet; add it here rather than bypassing it.
/// </para>
/// </remarks>
internal static class Upstream
{
    /// <summary>Upstream's <c>regex.compile</c> with no options and no timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The compiled pattern.</returns>
    public static FuzzyRegex Compile(string pattern) => Compile(pattern, FuzzyRegexOptions.None);

    /// <summary>Upstream's <c>regex.compile</c> with the given options.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The compiled pattern.</returns>
    public static FuzzyRegex Compile(string pattern, FuzzyRegexOptions options) =>
        Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout);

    /// <summary>Upstream's <c>regex.compile</c> with the given options and named lists.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The compiled pattern.</returns>
    public static FuzzyRegex Compile(
        string pattern,
        FuzzyRegexOptions options,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> namedLists
    ) => Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout, namedLists);

    /// <summary>Upstream's <c>regex.compile</c> with the given options, timeout and named lists.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">How long a single matching operation may run.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The compiled pattern.</returns>
    public static FuzzyRegex Compile(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => FuzzyRegex.WithDefaultVersion(pattern, options, matchTimeout, namedLists, Parsing.RegexFlags.Version0);

    /// <summary>Upstream's <c>regex.search</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The match, or an unsuccessful match.</returns>
    public static Match Match(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout, namedLists).Match(input);

    /// <summary>Upstream's <c>regex.match</c>.</summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The match, or an unsuccessful match.</returns>
    public static Match MatchAtStart(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout, namedLists).MatchAtStart(input);

    /// <summary>Upstream's <c>regex.fullmatch</c>.</summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The match, or an unsuccessful match.</returns>
    public static Match FullMatch(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout, namedLists).FullMatch(input);

    /// <summary>Upstream's <c>regex.finditer</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The matches, leftmost first.</returns>
    public static MatchCollection Matches(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => Compile(pattern, options, FuzzyRegex.InfiniteMatchTimeout, namedLists).Matches(input);

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The number of matches.</returns>
    public static int Count(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        Compile(pattern, options).Count(input);

    /// <summary>Upstream's <c>regex.sub</c> with a template.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string Replace(
        string input,
        string pattern,
        string replacement,
        FuzzyRegexOptions options = FuzzyRegexOptions.None
    ) => Compile(pattern, options).Replace(input, replacement);

    /// <summary>Upstream's <c>regex.sub</c> with a callable.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string Replace(
        string input,
        string pattern,
        MatchEvaluator evaluator,
        FuzzyRegexOptions options = FuzzyRegexOptions.None
    ) => Compile(pattern, options).Replace(input, evaluator);

    /// <summary>Upstream's <c>regex.subf</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="format">The format template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string ReplaceFormat(
        string input,
        string pattern,
        string format,
        FuzzyRegexOptions options = FuzzyRegexOptions.None
    ) => Compile(pattern, options).ReplaceFormat(input, format);

    /// <summary>Upstream's <c>regex.split</c>.</summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="pattern">The pattern to split on.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The pieces of the subject.</returns>
    public static string?[] Split(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        Compile(pattern, options).Split(input);

    /// <summary>
    /// Upstream's <c>regex.escape</c>. It compiles nothing, so it has no version to pin and simply
    /// forwards; it lives here so that the ported tests reach one class rather than two.
    /// </summary>
    /// <param name="input">The text to escape.</param>
    /// <param name="specialOnly">Escape only the characters that are special in a pattern.</param>
    /// <param name="literalSpaces">Leave spaces unescaped.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string input, bool specialOnly = true, bool literalSpaces = false) =>
        FuzzyRegex.Escape(input, specialOnly, literalSpaces);
}
