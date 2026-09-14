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
/// The constructor compiles for real from S07 onwards, and the pattern-level members below it
/// report what it produced. The matching members still throw
/// <see cref="NotImplementedException"/>: the engine lands in phase 3.
/// </para>
/// </remarks>
public sealed class FuzzyRegex
{
    /// <summary>A <see cref="MatchTimeout"/> value meaning "never time out".</summary>
    public static readonly TimeSpan InfiniteMatchTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The flags upstream resolves a pattern to but <see cref="FuzzyRegexOptions"/> has no name
    /// for, and which <see cref="Options"/> therefore hides. <c>UNICODE</c> in particular is on
    /// every compiled pattern, because <c>_main._compile</c> ORs it into any <c>str</c> pattern
    /// that named no encoding (<c>upstream/regex/_main.py</c> lines 570-574); upstream's own
    /// <c>test_getattr</c> expects to see it and this port's translation of that test does not.
    /// </summary>
    private static readonly int _unexposedFlags = ~Enum.GetValues<FuzzyRegexOptions>()
        .Aggregate(0, static (mask, option) => mask | (int)option);

    /// <summary>
    /// Upstream <c>_METACHARS</c> (<c>upstream/regex/_main.py</c> line 445): the characters
    /// <see cref="Escape(string, bool, bool)"/> escapes when <c>specialOnly</c> is set.
    /// </summary>
    private const string _metachars = "()[]{}?*+|^$\\.-#&~";

    private readonly Parsing.CompiledPattern _compiled;
    private readonly string[] _groupNames;
    private readonly int[] _groupNumbers;

    /// <summary>Compiles a pattern with no options and no timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(string pattern)
        : this(pattern, FuzzyRegexOptions.None, InfiniteMatchTimeout) { }

    /// <summary>Compiles a pattern with the given options and no timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(string pattern, FuzzyRegexOptions options)
        : this(pattern, options, InfiniteMatchTimeout) { }

    /// <summary>
    /// Compiles a pattern with the given options and the named lists its <c>\L&lt;name&gt;</c>
    /// references need.
    /// </summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name. Upstream passes these as keyword arguments to <c>regex.compile</c>.
    /// </param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(
        string pattern,
        FuzzyRegexOptions options,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> namedLists
    )
        : this(pattern, options, InfiniteMatchTimeout, namedLists) { }

    /// <summary>Compiles a pattern with the given options and match timeout.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">
    /// How long a single matching operation may run before it is abandoned, or
    /// <see cref="InfiniteMatchTimeout"/> for no limit.
    /// </param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchTimeout"/> is neither <see cref="InfiniteMatchTimeout"/> nor positive.
    /// </exception>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    public FuzzyRegex(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    )
        : this(pattern, options, matchTimeout, namedLists, Parsing.PatternCompiler.DefaultVersion) { }

    /// <summary>
    /// Compiles a pattern against a chosen default version, for a caller that needs a version
    /// other than <see cref="Parsing.PatternCompiler.DefaultVersion"/> without saying so in the
    /// flags. Upstream's <c>DEFAULT_VERSION</c> is a module global its own test suite reads and
    /// this port's is a compile-time constant, so the only way to ask for upstream's default is to
    /// pass it - which is what the ported suite's <c>Upstream</c> helper does (S50b).
    /// </summary>
    /// <remarks>
    /// Not a flag, deliberately. <c>Version0</c> in the flags and an inline <c>(?V1)</c> in the
    /// pattern leave both version bits set, which upstream rejects as "VERSION0 and VERSION1 flags
    /// are mutually incompatible"; a default is what a pattern falls back to when it names none, so
    /// a pattern that names one still wins.
    /// </remarks>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">How long a single matching operation may run.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <param name="defaultVersion">
    /// The version the pattern gets when neither the flags nor an inline <c>(?V0)</c> /
    /// <c>(?V1)</c> pick one, as a <see cref="Parsing.RegexFlags"/> bit.
    /// </param>
    internal FuzzyRegex(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists,
        int defaultVersion
    )
    {
        // Argument validation is real and comes first: it is a trust boundary.
        ArgumentNullException.ThrowIfNull(pattern);

        if (matchTimeout != InfiniteMatchTimeout && matchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchTimeout),
                matchTimeout,
                "The match timeout must be positive, or FuzzyRegex.InfiniteMatchTimeout."
            );
        }

        Pattern = pattern;
        MatchTimeout = matchTimeout;
        _compiled = Parsing.PatternCompiler.Compile(
            pattern,
            (int)options,
            ToCompilerNamedLists(namedLists),
            defaultVersion
        );

        // Upstream's _compile hands the code list straight to _regex.compile, whose C compiler is
        // the last thing that can reject a pattern - it refuses code the parser was happy to emit
        // (upstream/src/_regex.c:25863). Building here rather than at first match keeps that
        // rejection where the caller expects it, and where upstream puts it.
        PatternObject = Engine.PatternObject.Compile(_compiled);

        // Upstream's timeout is in clock ticks; ours is in Stopwatch ticks, which is the clock the
        // engine reads. decode_timeout (upstream/src/_regex.c:21056) maps a negative number to "no
        // timeout", which is exactly what InfiniteMatchTimeout is.
        TimeoutTicks =
            matchTimeout == InfiniteMatchTimeout
                ? Engine.MatchState.NoTimeout
                : (long)(matchTimeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);

        // Group 0 is the whole match and has no name of its own, so it is listed by its number,
        // as every group without a name is.
        Dictionary<int, string> nameByNumber = _compiled.GroupIndex.ToDictionary(
            entry => entry.Value,
            entry => entry.Key
        );
        _groupNumbers = [.. Enumerable.Range(0, _compiled.GroupCount + 1)];
        _groupNames =
        [
            .. _groupNumbers.Select(number =>
                nameByNumber.TryGetValue(number, out string? name)
                    ? name
                    : number.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ),
        ];
    }

    /// <summary>The pattern this instance was compiled from.</summary>
    public string Pattern { get; }

    /// <summary>
    /// The options this instance was compiled with, plus whatever the pattern's own inline flags
    /// and the default version added - upstream's <c>info.flags | version</c>, which is what
    /// <c>Pattern.flags</c> reports. So <c>new FuzzyRegex("(?i)a").Options</c> includes
    /// <see cref="FuzzyRegexOptions.IgnoreCase"/> even though the caller passed none.
    /// </summary>
    /// <remarks>
    /// The upstream flags this port does not expose - <c>ASCII</c>, <c>LOCALE</c>, <c>UNICODE</c>,
    /// <c>WORD</c>, <c>DEBUG</c> and <c>TEMPLATE</c> - are masked off rather than surfaced as
    /// numbers with no name.
    /// </remarks>
    public FuzzyRegexOptions Options => (FuzzyRegexOptions)(_compiled.Flags & ~_unexposedFlags);

    /// <summary>
    /// The named lists this instance was compiled with, keyed by name. Upstream
    /// <c>Pattern.named_lists</c>, which returns each list as a <c>frozenset</c>; the values here
    /// are therefore sets, not the caller's original ordering.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> NamedLists => _compiled.NamedLists;

    /// <summary>
    /// How long a single matching operation may run, or <see cref="InfiniteMatchTimeout"/>.
    /// </summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>
    /// The names of the pattern's groups, by ascending group number. Unnamed groups are
    /// represented by their number as text, as the built-in <c>Regex</c> does.
    /// </summary>
    public IReadOnlyList<string> GroupNames => _groupNames;

    /// <summary>The numbers of the pattern's groups, ascending, group 0 first.</summary>
    public IReadOnlyList<int> GroupNumbers => _groupNumbers;

    /// <summary>Finds the name of a group given its number.</summary>
    /// <param name="number">The group number.</param>
    /// <returns>The group's name, or its number as text if it has none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pattern has no such group.</exception>
    public string GroupNameFromNumber(int number) =>
        number >= 0 && number < _groupNames.Length
            ? _groupNames[number]
            : throw new ArgumentOutOfRangeException(nameof(number), number, "the pattern has no such group");

    /// <summary>
    /// Finds the number of a group given its name. Upstream exposes the same mapping as
    /// <c>Pattern.groupindex</c>.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group's number, or <c>-1</c> if the pattern has no such group.</returns>
    public int GroupNumberFromName(string name) => _compiled.GroupIndex.GetValueOrDefault(name, -1);

    /// <summary>
    /// Compiles a replacement template against this pattern's groups. Upstream compiles the
    /// template before it starts matching, so a malformed one is rejected whether or not the
    /// pattern matches: measured against <c>regex</c> 2026.7.19 on 2026-08-31,
    /// <c>regex.sub('x', r'\g&lt;bad', 'z')</c> raises <c>missing &gt;</c> on a subject with no
    /// match at all, while <c>regex.sub('x', r'\2', 'z')</c> returns <c>'z'</c> - the
    /// group-number check happens during expansion, which needs a match.
    /// </summary>
    /// <remarks>
    /// "Before it starts matching" is not "before anything at all": <c>pattern_subx</c> takes its
    /// too-short-subject shortcut first, so the template is not compiled - and a malformed one not
    /// rejected - when the pattern cannot possibly fit. Measured 2026-09-01:
    /// <c>regex.sub('xx', r'\g&lt;bad', 'z')</c> returns <c>'z'</c>. See <see cref="Subx"/>.
    /// </remarks>
    /// <param name="replacement">The replacement template.</param>
    /// <returns>The compiled template: a literal run or a group number per item.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    internal IReadOnlyList<object> CompileReplacement(string replacement) =>
        Parsing.PatternCompiler.CompileReplacement(replacement, _compiled.GroupCount, _compiled.GroupIndex);

    /// <summary>
    /// Adapts the public named-list shape to the compiler's. The compiler takes a list because
    /// <c>StringSet.__init__</c> sorts its branches by length with a stable sort, so the caller's
    /// order survives among equal-length members; the public surface takes an
    /// <see cref="IReadOnlyCollection{T}"/>, which does not promise one (DECISIONS 2026-08-30).
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>>? ToCompilerNamedLists(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists
    ) =>
        namedLists?.ToDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlyList<string>)[.. entry.Value],
            StringComparer.Ordinal
        );

    /// <summary>Whether the pattern matches anywhere in the given part of the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public bool IsMatch(string input, int beginning = 0, int length = -1) =>
        Run(input, beginning, length, partial: false, search: true, matchAll: false).Success;

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    // ponytail: copies the span, because the engine indexes a string. Making it allocation-free
    // means threading a ReadOnlySpan through MatchState and every try_match_*, which is a Phase 7
    // question (the whole engine is string-based today), not a correctness one.
    public bool IsMatch(ReadOnlySpan<char> input) => IsMatch(input.ToString());

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
    public bool IsMatchAtStart(string input, int beginning = 0, int length = -1) =>
        Run(input, beginning, length, partial: false, search: false, matchAll: false).Success;

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
    public bool IsFullMatch(string input, int beginning = 0, int length = -1) =>
        Run(input, beginning, length, partial: false, search: false, matchAll: true).Success;

    /// <summary>
    /// Runs one matching operation. Port of <c>pattern_search_or_match</c>
    /// (<c>upstream/src/_regex.c</c> line 21522) less its argument parsing, which our own overloads
    /// have already done.
    /// </summary>
    /// <param name="input">The subject.</param>
    /// <param name="beginning">Upstream's <c>pos</c>.</param>
    /// <param name="length">How much of the subject to consider, or <c>-1</c> for the rest.</param>
    /// <param name="partial">Upstream's <c>partial</c>.</param>
    /// <param name="search">Whether to advance the start position (upstream's <c>search</c>).</param>
    /// <param name="matchAll">Whether the match must cover the slice (upstream's <c>match_all</c>).</param>
    /// <returns>The match, successful or not.</returns>
    private Match Run(string input, int beginning, int length, bool partial, bool search, bool matchAll)
    {
        ArgumentNullException.ThrowIfNull(input);

        (int start, int end) = Limits(input, beginning, length);

        using var state = Engine.MatchState.Create(
            PatternObject,
            input,
            start,
            end,
            overlapped: false,
            partial: partial,
            // The Match object, and therefore repeated captures, will be visible.
            visibleCaptures: true,
            matchAll: matchAll,
            timeout: TimeoutTicks
        );

        int status = Engine.Matcher.DoMatch(state, search);

        if (status == Engine.MatchStatus.Cancelled)
        {
            throw new System.Text.RegularExpressions.RegexMatchTimeoutException(input, Pattern, MatchTimeout);
        }

        return NewMatch(state, input, status);
    }

    /// <summary>
    /// Resolves this surface's <c>(beginning, length)</c> pair into upstream's <c>pos</c> and
    /// <c>endpos</c>, which <c>state_init</c> then clamps (<c>get_limits</c>,
    /// <c>upstream/src/_regex.c</c> line 21627).
    /// </summary>
    /// <param name="input">The subject.</param>
    /// <param name="beginning">Where in the subject to start, possibly negative.</param>
    /// <param name="length">How much of it to consider, or <c>-1</c> for the rest.</param>
    /// <returns>The resolved start and end.</returns>
    /// <remarks>
    /// The beginning is resolved FIRST, because a negative one counts back from the end of the
    /// subject and adding the length to the unresolved number gives an end unrelated to the start:
    /// <c>Match("abcde", beginning: -2, length: 3)</c> computed an end of 1, which clamped up to
    /// the start and searched an empty slice, where <c>beginning: 3</c> with the same length
    /// searched (3, 5) and matched. Found by the S16 blind review; shared by every entry point
    /// taking the pair, so there is one copy of the rule to get wrong.
    /// <para>
    /// <c>length: -1</c> is "the rest of the subject", which is what upstream's <c>endpos</c>
    /// default of <c>PY_SSIZE_T_MAX</c> means once it is clamped.
    /// </para>
    /// </remarks>
    private static (int Start, int End) Limits(string input, int beginning, int length)
    {
        beginning = Engine.MatchState.ClampIndex(beginning, input.Length);

        return (beginning, length < 0 || beginning > int.MaxValue - length ? int.MaxValue : beginning + length);
    }

    /// <summary>
    /// Replaces every match, up to <paramref name="count"/> of them. The loop itself is
    /// <see cref="Engine.Substitution.Subx"/>, beside the rest of the <c>_regex.c</c> port; this is
    /// the argument shuffling upstream's <c>pattern_sub</c>/<c>subn</c>/<c>subf</c>/<c>subfn</c> do
    /// before calling it.
    /// </summary>
    /// <param name="input">The subject.</param>
    /// <param name="template">The replacement or format template, or <see langword="null"/> when
    /// <paramref name="evaluator"/> is given.</param>
    /// <param name="evaluator">Computes each replacement, or <see langword="null"/> for a template.</param>
    /// <param name="isFormat">Whether the template is a <c>str.format</c> one (upstream's <c>RE_SUBF</c>).</param>
    /// <param name="count">The most replacements to make, or a negative number for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    private string Subx(
        string input,
        string? template,
        MatchEvaluator? evaluator,
        bool isFormat,
        int count,
        out int replacements
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        if (evaluator is null)
        {
            ArgumentNullException.ThrowIfNull(template);
        }

        return Engine.Substitution.Subx(this, input, template, evaluator, isFormat, count, out replacements);
    }

    /// <summary>The compiled pattern the engine runs, which <c>pattern_subx</c> reads as <c>self</c>.</summary>
    internal Engine.PatternObject PatternObject { get; }

    /// <summary>The pattern's capture group count, upstream's <c>public_group_count</c>.</summary>
    internal int GroupCount => _compiled.GroupCount;

    /// <summary>The match timeout in <see cref="System.Diagnostics.Stopwatch"/> ticks.</summary>
    internal long TimeoutTicks { get; }

    /// <summary>
    /// Port of <c>pattern_new_match</c> (<c>upstream/src/_regex.c</c> line 20738).
    /// </summary>
    /// <param name="state">The state the match ran in.</param>
    /// <param name="input">The subject.</param>
    /// <param name="status">What <c>do_match</c> returned.</param>
    /// <returns>The match, successful or not.</returns>
    internal Match NewMatch(Engine.MatchState state, string input, int status)
    {
        // Upstream's guard is `status > 0 || status == RE_ERROR_PARTIAL` (:20741): a partial match
        // is a result, not an error, even though its status code is negative.
        if (status is not (Engine.MatchStatus.Success or Engine.MatchStatus.Partial))
        {
            return NoMatch(input);
        }

        // Upstream's rule that a reverse match reports its two ends the other way round (:20795).
        int matchStart = state.Reverse ? state.TextPos : state.MatchPos;
        int matchEnd = state.Reverse ? state.MatchPos : state.TextPos;

        return new Match(
            this,
            input,
            matchStart,
            matchEnd,
            success: true,
            // Copied, because the state's arrays are about to be disposed and are reused by the next
            // match: a Match sharing them would change under its owner (copy_groups, :20621).
            Engine.GroupData.CopyGroups(state.Groups, _compiled.GroupCount),
            // The slice, not the match, because Match.NextMatch resumes inside it - and the slice the
            // CALLER asked for, not whatever a `(*SKIP)` left behind. `DoMatch` puts the slice back
            // at the start of every match (see the reset there), so a scan is unaffected by a verb in
            // the previous match; `NextMatch` rebuilds a state from these two values instead, so
            // handing it the moved slice would make `MatchState.Create` record the MOVED slice as the
            // one to restore and reintroduce the same defect on this one path. Found by S40a's blind
            // review with a reproduction: `\b(?:[^a](*SKIP))*` over "b\n\rS" overlapped gave
            // (0,4) (1,3) (3,1) (4,0) from `Matches` and (0,4) (1,3) (4,0) from a `NextMatch` walk,
            // where Match's own remarks require the two to agree.
            state.InitialSliceStart,
            state.InitialSliceEnd,
            state.Overlapped,
            state.LastIndex,
            state.LastGroup,
            // Upstream `match->partial = status == RE_ERROR_PARTIAL` (:20774).
            partial: status == Engine.MatchStatus.Partial,
            // Upstream copies the counts only for a fuzzy pattern and zeroes them otherwise
            // (:20755-20759), so an exact pattern reports (0, 0, 0) rather than whatever the state
            // happens to hold.
            fuzzyCounts: PatternObject.IsFuzzy
                ? new FuzzyCounts(
                    (int)state.FuzzyCounts[Engine.FuzzyValue.Sub],
                    (int)state.FuzzyCounts[Engine.FuzzyValue.Ins],
                    (int)state.FuzzyCounts[Engine.FuzzyValue.Del]
                )
                : default,
            // Copied for the same reason the groups are: the state is reused by the next match.
            fuzzyChanges: state.FuzzyChanges.Count > 0 ? [.. state.FuzzyChanges] : null
        );
    }

    /// <summary>
    /// The unsuccessful <see cref="RegularExpressions.Match"/> upstream reports as <c>None</c>.
    /// </summary>
    /// <param name="input">The subject that was searched.</param>
    /// <returns>An unsuccessful match over that subject.</returns>
    /// <remarks>
    /// This surface returns an unsuccessful <see cref="RegularExpressions.Match"/> instead of null, which is the
    /// built-in <c>Regex</c>'s shape and the one S01 committed to. Its groups are the pattern's,
    /// all of them absent, so <c>Groups.Count</c> still reports what the pattern declares rather
    /// than throwing. Its slice is the whole subject, which nothing reads: an unsuccessful match
    /// ends the scan, so <see cref="Match.NextMatch"/> on one never searches again.
    /// </remarks>
    internal Match NoMatch(string input)
    {
        var absent = new Engine.GroupData[_compiled.GroupCount];
        for (int g = 0; g < absent.Length; g++)
        {
            absent[g] = new Engine.GroupData();
        }

        return new Match(
            this,
            input,
            0,
            0,
            success: false,
            absent,
            sliceStart: 0,
            sliceEnd: input.Length,
            overlapped: false
        );
    }

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
    public Match Match(string input, int beginning = 0, int length = -1, bool partial = false) =>
        Run(input, beginning, length, partial, search: true, matchAll: false);

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
    public Match MatchAtStart(string input, int beginning = 0, int length = -1, bool partial = false) =>
        Run(input, beginning, length, partial, search: false, matchAll: false);

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
    public Match FullMatch(string input, int beginning = 0, int length = -1, bool partial = false) =>
        Run(input, beginning, length, partial, search: false, matchAll: true);

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
    /// <param name="partial">
    /// Whether the scan may end with a partial match. Upstream's <c>finditer(partial=True)</c>
    /// (<c>_main.py:351</c>, <c>pattern_scanner</c>'s <c>kwlist</c> at <c>:21089</c>): the partial
    /// is yielded like any other match and is always the last one.
    /// </param>
    /// <returns>The matches, leftmost first.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The scan ran out of time. The whole scan shares one budget, as upstream's does.
    /// </exception>
    public MatchCollection Matches(
        string input,
        int beginning = 0,
        int length = -1,
        bool overlapped = false,
        bool partial = false
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        (int start, int end) = Limits(input, beginning, length);

        return new MatchCollection(Engine.Iteration.FindAll(this, input, start, end, overlapped, partial));
    }

    /// <summary>Counts the matches in the given part of the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="overlapped">Whether matches may overlap. Upstream's <c>overlapped=True</c>.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// Upstream spells this <c>len(findall(...))</c>; counting without building a match per match
    /// is the only reason it is its own entry point.
    /// </remarks>
    public int Count(string input, int beginning = 0, int length = -1, bool overlapped = false)
    {
        ArgumentNullException.ThrowIfNull(input);

        (int start, int end) = Limits(input, beginning, length);

        return Engine.Iteration.Count(this, input, start, end, overlapped);
    }

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// <c>ponytail:</c> the span is copied to a string, because the engine indexes a
    /// <see cref="string"/> throughout - <c>MatchState.Text</c> is one, as upstream's subject is a
    /// Python <c>str</c>. So this overload spares the caller a conversion and not the allocation.
    /// Lift it by moving the engine onto <c>ReadOnlySpan&lt;char&gt;</c>, which is a Phase 7
    /// question and touches every opcode, not this method.
    /// </remarks>
    public int Count(ReadOnlySpan<char> input) => Count(input.ToString());

    /// <summary>Replaces matches with an expanded replacement template.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">
    /// The replacement template, in upstream's syntax: <c>\1</c> and <c>\g&lt;name&gt;</c> stand
    /// for captured groups and <c>$</c> is ordinary text. See <see cref="Match.Result(string)"/>
    /// for why this is not <c>Regex</c>'s <c>$1</c> language.
    /// </param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    public string Replace(string input, string replacement, int count = -1) =>
        Subx(input, replacement, evaluator: null, isFormat: false, count, out _);

    /// <summary>
    /// Replaces matches with an expanded replacement template, reporting how many were replaced.
    /// Upstream <c>Pattern.subn</c>, which returns the pair.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    public string Replace(string input, string replacement, int count, out int replacements) =>
        Subx(input, replacement, evaluator: null, isFormat: false, count, out replacements);

    /// <summary>Replaces matches with text computed per match.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(string input, MatchEvaluator evaluator, int count = -1)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return Subx(input, template: null, evaluator, isFormat: false, count, out _);
    }

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
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return Subx(input, template: null, evaluator, isFormat: false, count, out replacements);
    }

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
    /// <returns>
    /// The pieces of the subject, with <see langword="null"/> where a capturing group did not
    /// take part in a match. Upstream puts <c>None</c> there; the built-in <c>Regex.Split</c>
    /// instead omits the entry, which loses the difference between a group that matched nothing
    /// and one that never ran. Verified against the local oracle 2026-08-29:
    /// <c>regex.split('(x)|(1)', 'a1b')</c> is <c>['a', None, '1', 'b']</c> where
    /// <c>Regex.Split("a1b", "(x)|(1)")</c> is <c>["a", "1", "b"]</c>.
    /// </returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The split ran out of time.
    /// </exception>
    public string?[] Split(string input, int maxSplits = -1)
    {
        ArgumentNullException.ThrowIfNull(input);

        return Engine.Iteration.Split(this, input, maxSplits);
    }

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template. Upstream
    /// <c>Pattern.subf</c>; see <see cref="ReplaceFormat(string, string, string, FuzzyRegexOptions)"/>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string ReplaceFormat(string input, string format, int count = -1) =>
        Subx(input, format, evaluator: null, isFormat: true, count, out _);

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template, reporting how many were
    /// replaced. Upstream <c>Pattern.subfn</c>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string ReplaceFormat(string input, string format, int count, out int replacements) =>
        Subx(input, format, evaluator: null, isFormat: true, count, out replacements);

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public static bool IsMatch(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        new FuzzyRegex(pattern, options).IsMatch(input);

    /// <summary>Finds the first match anywhere in the subject. Upstream <c>regex.search</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match.</returns>
    public static Match Match(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists).Match(input);

    /// <summary>
    /// Finds the match starting at the start of the subject. Upstream <c>regex.match</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match there.</returns>
    public static Match MatchAtStart(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists).MatchAtStart(input);

    /// <summary>Finds the match covering the whole subject. Upstream <c>regex.fullmatch</c>.</summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match all of it.</returns>
    public static Match FullMatch(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists).FullMatch(input);

    /// <summary>Finds every match in the subject. Upstream <c>regex.finditer</c>.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <returns>The matches, leftmost first.</returns>
    public static MatchCollection Matches(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null
    ) => new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists).Matches(input);

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The number of matches.</returns>
    public static int Count(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        new FuzzyRegex(pattern, options).Count(input);

    /// <summary>
    /// Replaces matches with an expanded replacement template. Upstream <c>regex.sub</c>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">
    /// The pattern or the template is not valid.
    /// </exception>
    public static string Replace(
        string input,
        string pattern,
        string replacement,
        FuzzyRegexOptions options = FuzzyRegexOptions.None
    ) => new FuzzyRegex(pattern, options).Replace(input, replacement);

    /// <summary>
    /// Replaces matches with text computed per match. Upstream <c>regex.sub</c> with a callable.
    /// </summary>
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
    ) => new FuzzyRegex(pattern, options).Replace(input, evaluator);

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
    public static string ReplaceFormat(
        string input,
        string pattern,
        string format,
        FuzzyRegexOptions options = FuzzyRegexOptions.None
    ) => new FuzzyRegex(pattern, options).ReplaceFormat(input, format);

    /// <summary>Splits the subject around the matches. Upstream <c>regex.split</c>.</summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="pattern">The pattern to split on.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>
    /// The pieces of the subject, with <see langword="null"/> where a capturing group did not
    /// take part in a match. See <see cref="Split(string, int)"/>.
    /// </returns>
    public static string?[] Split(string input, string pattern, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        new FuzzyRegex(pattern, options).Split(input);

    /// <summary>
    /// Escapes the characters that have a special meaning in a pattern, so the result matches the
    /// input literally. Upstream <c>regex.escape</c>.
    /// </summary>
    /// <param name="input">The text to escape.</param>
    /// <param name="specialOnly">
    /// Escape only the characters that are special in a pattern. This is the default, it is
    /// upstream's, and it is also the behaviour closest to <c>Regex.Escape</c> - measured, not
    /// assumed: over printable ASCII the two disagree on 5 characters of 95, against 19 of 95
    /// for <see langword="false"/>. Pass <see langword="false"/> to escape every non-alphanumeric
    /// character instead.
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
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public static string Escape(string input, bool specialOnly = true, bool literalSpaces = false)
    {
        ArgumentNullException.ThrowIfNull(input);

        var escaped = new System.Text.StringBuilder(input.Length);

        // Whole codepoints, as upstream iterates a Python str: a non-BMP character takes one
        // backslash, not one per surrogate. Written out rather than using EnumerateRunes because
        // that replaces a lone surrogate with U+FFFD, and a lone surrogate is a legal char here
        // exactly as it is a legal element of a Python str.
        int i = 0;
        while (i < input.Length)
        {
            int length =
                char.IsHighSurrogate(input[i]) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]) ? 2 : 1;
            int c = length == 2 ? char.ConvertToUtf32(input[i], input[i + 1]) : input[i];

            // Upstream writes this as two loops, one per value of special_only; one loop with the
            // condition inside it says the same thing without repeating the surrogate walk.
            bool escape =
                (c is not ' ' || !literalSpaces)
                && (
                    specialOnly
                        ? (c <= char.MaxValue && _metachars.Contains((char)c)) || Parsing.Source.IsSpace(c)
                        : !Parsing.RegexFlags.IsAlnum(c)
                );

            if (escape)
            {
                escaped.Append('\\');
            }

            escaped.Append(input, i, length);
            i += length;
        }

        return escaped.ToString();
    }

    /// <summary>Returns <see cref="Pattern"/>.</summary>
    /// <returns>The pattern this instance was compiled from.</returns>
    public override string ToString() => Pattern;
}
