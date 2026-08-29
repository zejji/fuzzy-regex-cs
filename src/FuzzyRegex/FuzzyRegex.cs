namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Computes the replacement text for one match. Shaped after
/// <see cref="System.Text.RegularExpressions.MatchEvaluator"/>; upstream passes a callable as the
/// <c>repl</c> argument of <c>sub</c>.
/// </summary>
/// <param name="match">The match to compute a replacement for.</param>
/// <returns>The text to put in place of the match.</returns>
public delegate string MatchEvaluator(Match match);

/// <summary>
/// A compiled regular expression with fuzzy (approximate) matching. A port of mrab-regex's
/// <c>Pattern</c> (<c>upstream/regex/_main.py</c>), shaped after
/// <see cref="System.Text.RegularExpressions.Regex"/> so that a .NET caller needs no new
/// vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// Instances are immutable and safe to share between threads, as both upstream and the built-in
/// <c>Regex</c> promise.
/// </para>
/// <para>
/// Three matching operations exist where the built-in <c>Regex</c> has one, because upstream has
/// three: <see cref="Match(string, int, int, bool)"/> searches anywhere (upstream <c>search</c>,
/// and what <c>Regex.Match</c> means), <see cref="MatchAtStart(string, int, int, bool)"/> anchors
/// at the start position (upstream <c>match</c>) and
/// <see cref="FullMatch(string, int, int, bool)"/> requires the whole subject (upstream
/// <c>fullmatch</c>). The .NET meaning of <c>Match</c> is kept, so upstream's <c>match</c> is the
/// one that had to be renamed.
/// </para>
/// <para>
/// Upstream's <c>pos</c> and <c>endpos</c> arguments are expressed the .NET way, as a
/// <c>beginning</c> and a <c>length</c>: <c>endpos</c> is <c>beginning + length</c>. Positions and
/// lengths are UTF-16 code units throughout (design spec section 4).
/// </para>
/// <para>
/// This type is a surface stub. Every member throws <see cref="NotImplementedException"/> until
/// phase 2 puts a parser and an engine behind it.
/// </para>
/// </remarks>
public sealed class FuzzyRegex
{
    /// <summary>A <see cref="MatchTimeout"/> value meaning "never time out".</summary>
    public static readonly TimeSpan InfiniteMatchTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>Compiles a pattern with no options and no timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(string pattern)
        : this(pattern, FuzzyRegexOptions.None, InfiniteMatchTimeout)
    {
    }

    /// <summary>Compiles a pattern with the given options and no timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(string pattern, FuzzyRegexOptions options)
        : this(pattern, options, InfiniteMatchTimeout)
    {
    }

    /// <summary>Compiles a pattern with the given options and match timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">
    /// How long a single matching operation may run before it is abandoned, or
    /// <see cref="InfiniteMatchTimeout"/> for no limit.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchTimeout"/> is neither <see cref="InfiniteMatchTimeout"/> nor positive.
    /// </exception>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(string pattern, FuzzyRegexOptions options, TimeSpan matchTimeout)
    {
        // Argument validation is real even while the body is a stub: it is a trust boundary, and
        // phase 2 replaces the throw below, not these checks.
        ArgumentNullException.ThrowIfNull(pattern);

        if (matchTimeout != InfiniteMatchTimeout && matchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchTimeout),
                matchTimeout,
                "The match timeout must be positive, or FuzzyRegex.InfiniteMatchTimeout.");
        }

        throw new NotImplementedException("The parser and engine land in phase 2.");
    }

    /// <summary>The pattern this instance was compiled from.</summary>
    public string Pattern => throw new NotImplementedException();

    /// <summary>The options this instance was compiled with.</summary>
    public FuzzyRegexOptions Options => throw new NotImplementedException();

    /// <summary>
    /// How long a single matching operation may run, or <see cref="InfiniteMatchTimeout"/>.
    /// </summary>
    public TimeSpan MatchTimeout => throw new NotImplementedException();

    /// <summary>
    /// The names of the pattern's groups, by ascending group number. Unnamed groups are
    /// represented by their number as text, as the built-in <c>Regex</c> does.
    /// </summary>
    public IReadOnlyList<string> GroupNames => throw new NotImplementedException();

    /// <summary>The numbers of the pattern's groups, ascending, group 0 first.</summary>
    public IReadOnlyList<int> GroupNumbers => throw new NotImplementedException();

    /// <summary>Finds the name of a group given its number.</summary>
    /// <param name="number">The group number.</param>
    /// <returns>The group's name, or its number as text if it has none.</returns>
    public string GroupNameFromNumber(int number) => throw new NotImplementedException();

    /// <summary>
    /// Finds the number of a group given its name. Upstream exposes the same mapping as
    /// <c>Pattern.groupindex</c>.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group's number, or <c>-1</c> if the pattern has no such group.</returns>
    public int GroupNumberFromName(string name) => throw new NotImplementedException();

    /// <summary>Whether the pattern matches anywhere in the given part of the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public bool IsMatch(string input, int beginning = 0, int length = -1)
        => throw new NotImplementedException();

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public bool IsMatch(ReadOnlySpan<char> input) => throw new NotImplementedException();

    /// <summary>
    /// Whether the pattern matches starting exactly at <paramref name="beginning"/>. Upstream
    /// <c>Pattern.match</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject to consider, or <c>-1</c> for the rest of it.
    /// </param>
    /// <returns><see langword="true"/> if the pattern matches there.</returns>
    public bool IsMatchAtStart(string input, int beginning = 0, int length = -1)
        => throw new NotImplementedException();

    /// <summary>
    /// Whether the pattern matches the whole of the given part of the subject. Upstream
    /// <c>Pattern.fullmatch</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject the match must cover, or <c>-1</c> for the rest of it.
    /// </param>
    /// <returns><see langword="true"/> if the pattern matches all of it.</returns>
    public bool IsFullMatch(string input, int beginning = 0, int length = -1)
        => throw new NotImplementedException();

    /// <summary>
    /// Finds the first match anywhere in the given part of the subject. Upstream
    /// <c>Pattern.search</c>, and what <c>Regex.Match</c> means.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="partial">
    /// Whether to report a partial match when the subject runs out before the pattern can succeed
    /// or fail. Upstream's <c>partial=True</c>; see <c>Match.PartialMatch</c>.
    /// </param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match.</returns>
    public Match Match(string input, int beginning = 0, int length = -1, bool partial = false)
        => throw new NotImplementedException();

    /// <summary>
    /// Finds the match starting exactly at <paramref name="beginning"/>. Upstream
    /// <c>Pattern.match</c>, which the built-in <c>Regex</c> has no equivalent of - hence the name.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject to consider, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="partial">Whether to report a partial match. Upstream's <c>partial=True</c>.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match there.</returns>
    public Match MatchAtStart(string input, int beginning = 0, int length = -1, bool partial = false)
        => throw new NotImplementedException();

    /// <summary>
    /// Finds the match covering the whole of the given part of the subject. Upstream
    /// <c>Pattern.fullmatch</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject the match must cover, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="partial">Whether to report a partial match. Upstream's <c>partial=True</c>.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match all of it.</returns>
    public Match FullMatch(string input, int beginning = 0, int length = -1, bool partial = false)
        => throw new NotImplementedException();

    /// <summary>
    /// Finds every match in the given part of the subject. Upstream <c>Pattern.finditer</c>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="overlapped">
    /// Whether matches may overlap. Upstream's <c>overlapped=True</c>; the built-in <c>Regex</c>
    /// always resumes after the previous match.
    /// </param>
    /// <returns>The matches, leftmost first.</returns>
    public MatchCollection Matches(string input, int beginning = 0, int length = -1, bool overlapped = false)
        => throw new NotImplementedException();

    /// <summary>Counts the matches in the given part of the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="overlapped">Whether matches may overlap. Upstream's <c>overlapped=True</c>.</param>
    /// <returns>The number of matches.</returns>
    public int Count(string input, int beginning = 0, int length = -1, bool overlapped = false)
        => throw new NotImplementedException();

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <returns>The number of matches.</returns>
    public int Count(ReadOnlySpan<char> input) => throw new NotImplementedException();

    /// <summary>Replaces matches with an expanded replacement template.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">
    /// The replacement template, in which the usual group references stand for captured groups.
    /// </param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(string input, string replacement, int count = -1)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches with an expanded replacement template, reporting how many were replaced.
    /// Upstream <c>Pattern.subn</c>, which returns the pair.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(string input, string replacement, int count, out int replacements)
        => throw new NotImplementedException();

    /// <summary>Replaces matches with text computed per match.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(string input, MatchEvaluator evaluator, int count = -1)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches with text computed per match, reporting how many were replaced. Upstream
    /// <c>Pattern.subn</c> with a callable.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(string input, MatchEvaluator evaluator, int count, out int replacements)
        => throw new NotImplementedException();

    /// <summary>
    /// Splits the subject around the matches, including the text captured by any groups, as both
    /// upstream and the built-in <c>Regex</c> do.
    /// </summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="maxSplits">
    /// The most splits to make, or <c>-1</c> for no limit. This is upstream's <c>maxsplit</c>: a
    /// count of splits, not of resulting pieces, which is what the <c>count</c> argument of
    /// <c>Regex.Split</c> means. The names differ because the meanings do.
    /// </param>
    /// <returns>The pieces of the subject.</returns>
    public string[] Split(string input, int maxSplits = -1) => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template. Upstream
    /// <c>Pattern.subf</c>; see <see cref="ReplaceFormat(string, string, string, FuzzyRegexOptions)"/>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string ReplaceFormat(string input, string format, int count = -1)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template, reporting how many were
    /// replaced. Upstream <c>Pattern.subfn</c>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string ReplaceFormat(string input, string format, int count, out int replacements)
        => throw new NotImplementedException();

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public static bool IsMatch(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>Finds the first match anywhere in the subject. Upstream <c>regex.search</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match.</returns>
    public static Match Match(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>
    /// Finds the match starting at the start of the subject. Upstream <c>regex.match</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match there.</returns>
    public static Match MatchAtStart(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>Finds the match covering the whole subject. Upstream <c>regex.fullmatch</c>.</summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match all of it.</returns>
    public static Match FullMatch(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>Finds every match in the subject. Upstream <c>regex.finditer</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The matches, leftmost first.</returns>
    public static MatchCollection Matches(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The number of matches.</returns>
    public static int Count(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches with an expanded replacement template. Upstream <c>regex.sub</c>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string Replace(string input, string pattern, string replacement, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches with text computed per match. Upstream <c>regex.sub</c> with a callable.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string Replace(string input, string pattern, MatchEvaluator evaluator, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template, where <c>{0}</c> is the
    /// whole match and <c>{1}</c> is group 1. Upstream <c>regex.subf</c>. This is a different
    /// templating language from the one <see cref="Replace(string, string, int)"/> takes, not a
    /// formatting option on it.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="format">The format template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string ReplaceFormat(string input, string pattern, string format, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>Splits the subject around the matches. Upstream <c>regex.split</c>.</summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="pattern">The pattern to split on.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The pieces of the subject.</returns>
    public static string[] Split(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None)
        => throw new NotImplementedException();

    /// <summary>
    /// Escapes the characters that have a special meaning in a pattern, so the result matches the
    /// input literally. Upstream <c>regex.escape</c>.
    /// </summary>
    /// <param name="input">The text to escape.</param>
    /// <param name="specialOnly">
    /// Escape only the characters that are special in a pattern (the default, and upstream's).
    /// Pass <see langword="false"/> to escape every non-alphanumeric character, which is what
    /// <c>Regex.Escape</c> is closest to.
    /// </param>
    /// <param name="literalSpaces">
    /// Leave spaces unescaped. Upstream escapes them by default so the result still matches
    /// literally under <see cref="FuzzyRegexOptions.IgnorePatternWhitespace"/>.
    /// </param>
    /// <returns>The escaped text.</returns>
    /// <remarks>
    /// Both flags are upstream's, and all four combinations differ. Verified against the local
    /// oracle 2026-08-29: <c>escape('foo!?')</c> is <c>foo!\?</c> but
    /// <c>escape('foo!?', special_only=False)</c> is <c>foo\!\?</c>; <c>escape('a b')</c> is
    /// <c>a\ b</c> but <c>escape('a b', literal_spaces=True)</c> is <c>a b</c>.
    /// </remarks>
    public static string Escape(string input, bool specialOnly = true, bool literalSpaces = false)
        => throw new NotImplementedException();

    /// <summary>Reverses <see cref="Escape(string, bool, bool)"/>.</summary>
    /// <param name="input">The escaped text.</param>
    /// <returns>The unescaped text.</returns>
    public static string Unescape(string input) => throw new NotImplementedException();

    /// <summary>Returns <see cref="Pattern"/>.</summary>
    /// <returns>The pattern this instance was compiled from.</returns>
    public override string ToString() => throw new NotImplementedException();
}
