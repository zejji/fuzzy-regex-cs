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
/// <b>Thread safety.</b> Instances are immutable and thread safe. A <see cref="FuzzyRegex"/> can be
/// compiled on any thread and shared between threads, and its matching methods can be called from
/// any thread at once; they alter no global state. A <see cref="RegularExpressions.Match"/> this class returns may
/// likewise be read from any thread. An enumerator is the exception, as it is everywhere in .NET: a
/// <see cref="MatchCollection"/> may be read from several threads, but one enumerator over it -
/// the thing a <c>foreach</c> creates - belongs to the thread that made it.
/// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-match-may-be-read-from-any-thread-where-systemtextregularexpressionsmatch-may-not">the comparison page</see>.
/// </para>
/// <para>
/// That is the guarantee the built-in <see cref="System.Text.RegularExpressions.Regex"/> makes -
/// "The Regex class itself is thread safe and immutable (read-only)" - <b>plus</b> the part it
/// explicitly withholds, which is that "result objects (Match and MatchCollection) returned by
/// Regex should be used on a single thread" because "their implementations could delay computation
/// of some results". This port makes the stronger promise instead, and pays for it by publishing
/// the one value a <see cref="RegularExpressions.Match"/> computes on demand as a single reference; the tests in
/// <c>Gaps/Api/ThreadSafetyTests.cs</c> and <c>Gaps/Api/ThreadSafetyStressTests.cs</c> hold it to
/// that. It is reached by immutability rather than by locking, which is what lets it coexist with
/// .NET's own advice to library authors to "not make instance data thread safe by default" since
/// "adding locks to create thread-safe code decreases performance".
/// </para>
/// <para>
/// Upstream's <c>concurrent</c> argument has no equivalent here and needs none. It asks upstream's
/// C to release the GIL during the match, which is a question that exists only because CPython has
/// a global lock; this port never takes one, so every call is already concurrent.
/// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#no-concurrent-parameter">the comparison page</see>.
/// </para>
/// <para>
/// Three matching operations exist where the built-in <c>Regex</c> has one, because upstream has
/// three: <see cref="Match(string, int, int, bool, TimeSpan?, CancellationToken)"/> searches anywhere (upstream <c>search</c>,
/// and what <c>Regex.Match</c> means), <see cref="MatchAtStart(string, int, int, bool, TimeSpan?, CancellationToken)"/> anchors
/// at the start position (upstream <c>match</c>) and
/// <see cref="FullMatch(string, int, int, bool, TimeSpan?, CancellationToken)"/> requires the whole subject (upstream
/// <c>fullmatch</c>). The .NET meaning of <c>Match</c> is kept, so upstream's <c>match</c> is the
/// one that had to be renamed.
/// </para>
/// <para>
/// <b><c>Match</c> means upstream's <c>search</c>; upstream's anchored <c>match</c> is
/// <c>MatchAtStart</c>.</b> Every static convenience on this class takes
/// <c>(input, pattern, options)</c>, the built-in <c>Regex</c>'s parameter order, where
/// upstream's module-level functions take the pattern before the subject.
/// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#match-means-upstreams-search-upstreams-anchored-match-is-matchatstart">the comparison page</see>.
/// </para>
/// <para>
/// Upstream's <c>pos</c> and <c>endpos</c> arguments are expressed the .NET way, as a
/// <c>beginning</c> and a <c>length</c>: <c>endpos</c> is <c>beginning + length</c>. Positions and
/// lengths are UTF-16 code units throughout (design spec section 4). The comparison page covers
/// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#upstreams-posendpos-are-reshaped-to-beginninglength">the reshaped arguments</see>
/// and <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#indices-are-utf-16-code-units-where-upstream-counts-codepoints">the UTF-16 indices</see>.
/// </para>
/// <para>
/// <b>A <see cref="CancellationToken"/> on every input-dependent method.</b> Every method that
/// reads the subject takes one as its last parameter, except <see cref="Match.NextMatch"/>;
/// upstream has none, because a Python caller interrupts a long match with Ctrl-C. See
/// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-cancellationtoken-on-every-input-dependent-method">the comparison page</see>.
/// </para>
/// <para>
/// <b>A per-call <c>timeout</c> on every input-dependent method.</b> Every one except
/// <see cref="Match.NextMatch"/> takes a <c>TimeSpan? timeout</c>, where <see langword="null"/>
/// means the pattern's own <see cref="FuzzyRegex.MatchTimeout"/>, as upstream's <c>timeout=</c>
/// does. See
/// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-per-call-timeout-on-every-input-dependent-method">the comparison page</see>.
/// </para>
/// <para>
/// <b>Not ported at all</b>: <c>Pattern.scanner</c>, <c>Match.pos</c>, <c>endpos</c>,
/// <c>string</c>, <c>re</c>, <c>regs</c> and <c>detach_string</c>, <c>regex.template</c>, the
/// <c>LOCALE</c> and <c>DEBUG</c> options, <c>cache_pattern</c>, <c>regex.prefixmatch</c> and the
/// copy protocol have no port equivalent. See
/// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#not-ported-at-all">the comparison page</see>.
/// </para>
/// </remarks>
/// <example>
/// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#en-allow-up-to-n-errors-of-any-kind">the comparison page</see>:</para>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// Match m = FuzzyRegex.Match("foxbar", "^(foobar){e&lt;=1}$");
/// Console.WriteLine(m.Success);   // True
/// (int subs, int ins, int dels) = m.FuzzyCounts;
/// Console.WriteLine($"{subs} {ins} {dels}");   // 1 0 0
/// </code>
/// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#side-separate-budgets-per-kind-of-error">the comparison page</see>:</para>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// Match m = FuzzyRegex.Match("oobargoobaploowap", "(foobar){i&lt;=2,s&lt;=2,e&lt;=2}");
/// Console.WriteLine((m.Index, m.Length));   // (5, 6)
/// </code>
/// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#cost-forms-nimdn-weights-errors-instead-of-just-counting-them">the comparison page</see>:</para>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// // "3oifaowefbaoraofuiebofasebfaobfaorfeoaro" - one insertion costs nothing here because the
/// // equation never names 'i'; two deletions or three substitutions are capped, and the total
/// // weighted cost must come to less than 4.
/// const string subject = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";
/// Match m = FuzzyRegex.Match(subject, "(foobar){i&lt;=1,d&lt;=2,s&lt;=3,2d+1s&lt;4}");
/// Console.WriteLine((m.Index, m.Length));   // (6, 7)
/// </code>
/// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#0e5-two-sided-form-at-least-one-error-and-fewer-than-five">the comparison page</see>:</para>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// Match fuzzy = FuzzyRegex.MatchAtStart("servic detection", "(?:service detection){0&lt;e&lt;5}");
/// Console.WriteLine((fuzzy.Index, fuzzy.Length));   // (0, 16)
///
/// Match exact = FuzzyRegex.MatchAtStart("service detection", "(?:service detection){0&lt;e&lt;5}");
/// Console.WriteLine((exact.Index, exact.Length, exact.FuzzyCounts.Deletions));   // (0, 16, 1)
/// </code>
/// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#enset-constrain-which-characters-an-edit-may-touch">the comparison page</see>:</para>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// Match ok = FuzzyRegex.FullMatch("ae", @"(?:a){e&lt;=1:[a-z]}");
/// Console.WriteLine(ok.Success);   // True - the inserted 'e' is in [a-z]
///
/// Match rejected = FuzzyRegex.FullMatch("a-", @"(?:a){e&lt;=1:[a-z]}");
/// Console.WriteLine(rejected.Success);   // False - the inserted '-' is not in [a-z]
/// </code>
/// <code language="csharp">
/// using Fuzzy.Text.RegularExpressions;
///
/// Match perKind = FuzzyRegex.Match("colur", @"(?:colour){i&lt;=1,d&lt;=1,s&lt;=1:[a-z]}");
/// Console.WriteLine(perKind.Value);   // colur - one substitution and one deletion, both within [a-z]
/// Console.WriteLine(FuzzyRegex.Match("col0ur", @"(?:colour){i&lt;=1,d&lt;=2,s&lt;=3,2d+1s&lt;4:[a-z]}").Success);   // False - the substitution would put a digit in
/// </code>
/// </example>
public sealed class FuzzyRegex
{
    /// <summary>A <see cref="MatchTimeout"/> value meaning "never time out".</summary>
    public static readonly TimeSpan InfiniteMatchTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The <see cref="MaxCompiledNodes"/> a pattern gets when the caller asks for none: a million
    /// nodes, which is about 250 MB and under a second of compiling.
    /// </summary>
    /// <remarks>
    /// Chosen in S56b against the measured cost of a node (about 250 bytes,
    /// <c>Engine/Node.cs</c>) and the largest repeat count the ported suite and the compile-parity
    /// corpus contain (<c>{65535}</c>, which creates about 131,000 nodes). Upstream has no such
    /// limit; see the "Compile budget" row of <c>docs/DIVERGENCES.md</c>.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#compile-budget">the comparison page</see>.
    /// </remarks>
    public const int DefaultMaxCompiledNodes = 1_000_000;

    /// <summary>
    /// The flags upstream resolves a pattern to but <see cref="FuzzyRegexOptions"/> has no name
    /// for, and which <see cref="Options"/> therefore hides: <c>LOCALE</c>, <c>DEBUG</c> and
    /// <c>TEMPLATE</c>. <c>ASCII</c>, <c>UNICODE</c> and <c>WORD</c> were in this set until S53b
    /// and are now named members, so <c>UNICODE</c> - which <c>_main._compile</c> ORs into any
    /// <c>str</c> pattern that named no encoding (<c>upstream/regex/_main.py</c> lines 570-574) -
    /// is reported on every pattern, which is what upstream's own <c>test_getattr</c> expects.
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

    /// <summary>
    /// The state a call reuses, so that a warm pattern builds nothing per call. The only thing
    /// reachable from a compiled pattern that matching writes; see
    /// <see cref="Engine.MatchStateCache"/> for why that is still safe to share.
    /// </summary>
    internal readonly Engine.MatchStateCache StateCache = new();

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
    /// <remarks>
    /// <para>
    /// <b>The "unused keyword argument" message interpolates the name as written</b>: a named list
    /// called <c>é</c> that the pattern never uses is reported as <c>'é'</c>, where upstream prints
    /// <c>'\xe9'</c>. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-unused-keyword-argument-message-interpolates-the-name-as-written">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#lname-fuzzy-matching-against-a-named-list-of-words">the comparison page</see>:</para>
    /// <code language="csharp">
    /// using Fuzzy.Text.RegularExpressions;
    ///
    /// var namedLists = new Dictionary&lt;string, IReadOnlyCollection&lt;string&gt;&gt; { ["words"] = ["cat", "dog"] };
    /// MatchCollection matches = FuzzyRegex.Matches(
    ///     " book dog cot desk ",
    ///     @"(?e)\b\L&lt;words&gt;{e&lt;=1}\b",
    ///     FuzzyRegexOptions.None,
    ///     namedLists
    /// );
    /// foreach (Match m in matches)
    /// {
    ///     Console.WriteLine(m.Value);
    /// }
    /// // dog
    /// // cot
    /// </code>
    /// </example>
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
    /// <remarks>
    /// <para>
    /// <b>Version 1 is the default</b>. A pattern that names neither version, in the flags or
    /// inline, compiles under version 1: nested sets and set operations inside a character
    /// class, full case-folding under <see cref="FuzzyRegexOptions.IgnoreCase"/>, and a
    /// backreference to a still-open group all work, where upstream defaults to version 0.
    /// Pass <see cref="FuzzyRegexOptions.Version0"/>, or write <c>(?V0)</c>, for upstream's
    /// default instead.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#version-1-is-the-default">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>(?V0)</c> really means version 0, where upstream's own algorithm would leave
    /// version 1's <c>FULLCASE</c> on.</b> Under this port's version-1 default, an inline
    /// <c>(?V0)</c> compiles bit-for-bit as <see cref="FuzzyRegexOptions.Version0"/> would.
    /// Upstream's own two-pass parser instead leaks its default version's flags into a leading
    /// <c>(?V0)</c>, so <c>compile('(?V0)a')</c> keeps full case-folding on there - unreachable
    /// upstream, where the default is version 0 already.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#v0-really-means-version-0-where-upstreams-own-algorithm-would-leave-version-1s-fullcase-on">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>The "unterminated character set" parse error names
    /// <see cref="FuzzyRegexOptions.Version0"/> and the <c>\[</c> escape.</b> It does so only
    /// for a pattern that version 0 itself accepts, such as <c>[[a-z]--[aeiou]]</c>; upstream
    /// raises the bare message with no such detail.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-unterminated-character-set-parse-error-names-fuzzyregexoptionsversion0-and-the--escape">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A pattern needing more than <paramref name="maxCompiledNodes"/> nodes is refused</b>
    /// with <see cref="FuzzyRegexParseException"/> before it can match anything. A counted repeat
    /// is expanded into one copy of its body per repetition, so nested counted repeats allocate
    /// the product of their counts: <c>((a{1000}){1000}){1000}</c> needs a thousand million nodes
    /// and exhausts memory upstream, which has no limit. Raise the budget for a pattern that
    /// genuinely needs a bigger graph. See the "Compile budget" row of <c>docs/DIVERGENCES.md</c>.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#compile-budget">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>The "unused keyword argument" message interpolates the name as written</b>: a named list
    /// called <c>é</c> that the pattern never uses is reported as <c>'é'</c>, where upstream prints
    /// <c>'\xe9'</c>. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-unused-keyword-argument-message-interpolates-the-name-as-written">the comparison page</see>.
    /// </para>
    /// </remarks>
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
    /// <param name="maxCompiledNodes">
    /// The most nodes the compiled pattern may occupy, or <see cref="DefaultMaxCompiledNodes"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchTimeout"/> is neither <see cref="InfiniteMatchTimeout"/> nor positive,
    /// or <paramref name="maxCompiledNodes"/> is not positive.
    /// </exception>
    /// <exception cref="FuzzyRegexParseException">
    /// The pattern is not valid, or needs more than <paramref name="maxCompiledNodes"/> nodes.
    /// </exception>
    public FuzzyRegex(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        int maxCompiledNodes = DefaultMaxCompiledNodes
    )
        : this(pattern, options, matchTimeout, namedLists, Parsing.PatternCompiler.DefaultVersion, maxCompiledNodes) { }

    /// <summary>
    /// Compiles a pattern against a chosen default version, for a caller that needs a version
    /// other than <see cref="Parsing.PatternCompiler.DefaultVersion"/> without saying so in the
    /// flags. Upstream's <c>DEFAULT_VERSION</c> is a module global its own test suite reads and
    /// this port's is a compile-time constant, so the only way to ask for upstream's default is to
    /// pass it - which is what the ported suite's <c>Upstream</c> helper does (S50b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a flag, deliberately. <c>Version0</c> in the flags and an inline <c>(?V1)</c> in the
    /// pattern leave both version bits set, which upstream rejects as "VERSION0 and VERSION1 flags
    /// are mutually incompatible"; a default is what a pattern falls back to when it names none, so
    /// a pattern that names one still wins.
    /// </para>
    /// <para>
    /// A METHOD rather than an internal constructor, since S56b. It was a constructor differing
    /// from the public one only by a trailing <see langword="int"/>, and the moment the public
    /// constructor gained a trailing <see langword="int"/> of its own (<c>maxCompiledNodes</c>) the
    /// one call site silently rebound to it: the ported suite started compiling under version 1
    /// with a budget of 8,192 nodes, which two ported tests caught and nothing in the signature
    /// would have. A name cannot be captured by an overload.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">How long a single matching operation may run.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <param name="defaultVersion">
    /// The version the pattern gets when neither the flags nor an inline <c>(?V0)</c> /
    /// <c>(?V1)</c> pick one, as a <see cref="Parsing.RegexFlags"/> bit.
    /// </param>
    /// <param name="upstreamReverseGrapheme">
    /// NOT UPSTREAM, and never set by this library: compile a backwards <c>\X</c> in upstream's
    /// order. The oracle sets it to show that ledger entry 43 is the whole of a divergence; see
    /// <see cref="Parsing.Info.UpstreamReverseGrapheme"/>.
    /// </param>
    /// <param name="upstreamFoldedRuns">
    /// NOT UPSTREAM, and never set by this library: compile a fuzzy full-case-folded run to its
    /// folding alone, as upstream does. The oracle sets it to show that ledger entry 49 is the whole
    /// of a divergence; see <see cref="Parsing.Info.UpstreamFoldedRuns"/>.
    /// </param>
    /// <param name="upstreamWholePatternCall">
    /// NOT UPSTREAM, and never set by this library: decide whether the pattern as a whole is a fuzzy
    /// section before optimising, as upstream does. The oracle sets it to show that known defect
    /// D37 (ledger entry 55) is the whole of a divergence; see
    /// <see cref="Parsing.Info.UpstreamWholePatternCall"/>.
    /// </param>
    /// <param name="upstreamCallFeatures">
    /// NOT UPSTREAM, and never set by this library: give each group call the direction and
    /// fuzziness upstream gives it. The oracle sets it to show that D40, D42 and D43 are the whole
    /// of a divergence; see <see cref="Parsing.Info.UpstreamCallFeatures"/>.
    /// </param>
    /// <param name="upstreamFuzzyConditionTest">
    /// NOT UPSTREAM, and never set by this library: compile a conditional's lookaround test with
    /// the enclosing fuzzy section's fuzziness, as upstream does. The oracle sets it to show that
    /// known defect D52 (ledger entry 64) is the whole of a divergence; see
    /// <see cref="Parsing.Info.UpstreamFuzzyConditionTest"/>.
    /// </param>
    /// <param name="upstreamCallCopies">
    /// NOT UPSTREAM, and never set by this library: compile a called group's copy from the group as
    /// parsed, as upstream does. The oracle sets it to show that D50 is the whole of a divergence;
    /// see <see cref="Parsing.Info.UpstreamCallCopies"/>.
    /// </param>
    /// <returns>The compiled pattern.</returns>
    internal static FuzzyRegex WithDefaultVersion(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists,
        int defaultVersion,
        bool upstreamReverseGrapheme = false,
        bool upstreamFoldedRuns = false,
        bool upstreamWholePatternCall = false,
        bool upstreamCallFeatures = false,
        bool upstreamFuzzyConditionTest = false,
        bool upstreamCallCopies = false
    ) =>
        new(
            pattern,
            options,
            matchTimeout,
            namedLists,
            defaultVersion,
            DefaultMaxCompiledNodes,
            upstreamReverseGrapheme,
            upstreamFoldedRuns,
            upstreamWholePatternCall,
            upstreamCallFeatures,
            upstreamFuzzyConditionTest,
            upstreamCallCopies
        );

    /// <summary>The one constructor that compiles: every other overload delegates to it.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">How long a single matching operation may run.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <param name="defaultVersion">
    /// The version the pattern gets when neither the flags nor an inline <c>(?V0)</c> /
    /// <c>(?V1)</c> pick one, as a <see cref="Parsing.RegexFlags"/> bit.
    /// </param>
    /// <param name="maxCompiledNodes">The compile budget, in nodes.</param>
    /// <param name="upstreamReverseGrapheme">See <see cref="WithDefaultVersion"/>.</param>
    /// <param name="upstreamFoldedRuns">See <see cref="WithDefaultVersion"/>.</param>
    /// <param name="upstreamWholePatternCall">See <see cref="WithDefaultVersion"/>.</param>
    /// <param name="upstreamCallFeatures">See <see cref="WithDefaultVersion"/>.</param>
    /// <param name="upstreamFuzzyConditionTest">See <see cref="WithDefaultVersion"/>.</param>
    /// <param name="upstreamCallCopies">See <see cref="WithDefaultVersion"/>.</param>
    private FuzzyRegex(
        string pattern,
        FuzzyRegexOptions options,
        TimeSpan matchTimeout,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists,
        int defaultVersion,
        int maxCompiledNodes,
        bool upstreamReverseGrapheme = false,
        bool upstreamFoldedRuns = false,
        bool upstreamWholePatternCall = false,
        bool upstreamCallFeatures = false,
        bool upstreamFuzzyConditionTest = false,
        bool upstreamCallCopies = false
    )
    {
        // Argument validation is real and comes first: it is a trust boundary.
        ArgumentNullException.ThrowIfNull(pattern);

        ValidateTimeout(matchTimeout, nameof(matchTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCompiledNodes);

        Pattern = pattern;
        MatchTimeout = matchTimeout;
        MaxCompiledNodes = maxCompiledNodes;
        _compiled = Parsing.PatternCompiler.Compile(
            pattern,
            (int)options,
            ToCompilerNamedLists(namedLists),
            defaultVersion,
            upstreamReverseGrapheme,
            upstreamFoldedRuns,
            upstreamWholePatternCall,
            upstreamCallFeatures,
            upstreamFuzzyConditionTest,
            upstreamCallCopies
        );

        // Upstream's _compile hands the code list straight to _regex.compile, whose C compiler is
        // the last thing that can reject a pattern - it refuses code the parser was happy to emit
        // (upstream/src/_regex.c:25863). Building here rather than at first match keeps that
        // rejection where the caller expects it, and where upstream puts it. The pattern text goes
        // with it because the compile budget's refusal is a parse failure and carries the pattern,
        // and CompiledPattern - the compile-parity seam - deliberately does not hold it.
        PatternObject = Engine.PatternObject.Compile(_compiled, pattern, maxCompiledNodes);

        TimeoutTicks = ToTicks(matchTimeout);

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
    /// <para>
    /// The upstream flags this port does not expose - <c>LOCALE</c>, <c>DEBUG</c> and
    /// <c>TEMPLATE</c> - are masked off rather than surfaced as numbers with no name.
    /// <see cref="FuzzyRegexOptions.Unicode"/> is therefore on every pattern that named no
    /// encoding, because upstream puts it there.
    /// </para>
    /// <para>
    /// <b>Version 1 is the default</b>. A plain pattern that names neither version still reports
    /// <see cref="FuzzyRegexOptions.Version1"/> plus <see cref="FuzzyRegexOptions.FullCase"/>
    /// here: <c>new FuzzyRegex("a").Options</c> is <c>Version1 | FullCase | Unicode</c>, because
    /// the version-implied defaults are folded into the compiled flags rather than kept as a
    /// separate, unreported field.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#version-1-is-the-default">the comparison page</see>.
    /// </para>
    /// </remarks>
    public FuzzyRegexOptions Options => (FuzzyRegexOptions)(_compiled.Flags & ~_unexposedFlags);

    /// <summary>
    /// The named lists this instance was compiled with, keyed by name. Upstream
    /// <c>Pattern.named_lists</c>, which returns each list as a <c>frozenset</c>; the values here
    /// are therefore sets, which drop the caller's original ordering.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> NamedLists => _compiled.NamedLists;

    /// <summary>
    /// How long a single matching operation may run, or <see cref="InfiniteMatchTimeout"/>.
    /// </summary>
    /// <remarks>
    /// Set one when a pattern mixes fuzzy sections with recursion (<c>(?R)</c>, <c>(?1)</c>,
    /// <c>(?&amp;name)</c>). The engine remembers recursive calls that failed, which keeps most such
    /// patterns fast, but a call that succeeds and whose caller then fails is tried again under
    /// every way of reaching it, and some patterns of that shape take time exponential in the
    /// length of the text. The timeout is their bound: a call that runs out of time throws
    /// <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> and never returns a
    /// wrong answer. Upstream raises <c>MemoryError</c> on these patterns. See the fuzzy recursion
    /// row of <c>docs/DIVERGENCES.md</c>.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-fuzzy-recursive-pattern-whose-calls-fail-is-matched-in-milliseconds-but-some-shapes-stay-exponential-and-matchtimeout-is-their-bound">the comparison page</see>.
    /// <para>
    /// <b>A per-call <c>timeout</c> on every input-dependent method.</b> Every one except
    /// <see cref="Match.NextMatch"/> takes a <c>TimeSpan? timeout</c>, where <see langword="null"/>
    /// means the pattern's own <see cref="FuzzyRegex.MatchTimeout"/>, as upstream's <c>timeout=</c>
    /// does. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-per-call-timeout-on-every-input-dependent-method">the comparison page</see>.
    /// </para>
    /// </remarks>
    public TimeSpan MatchTimeout { get; }

    /// <summary>
    /// The compile budget this instance was built under: the most nodes compiling the pattern was
    /// allowed to create. A pattern needing more is refused at construction rather than compiled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The companion to <see cref="MatchTimeout"/>, and the other half of what a caller compiling
    /// somebody else's pattern needs: the timeout bounds matching, this bounds compiling. Upstream
    /// has neither. There is no "unlimited" value on purpose: <see cref="int.MaxValue"/> nodes is
    /// about 500 GB at 250 bytes each, so it is already further than any process can go.
    /// </para>
    /// <para>
    /// <b>The count is of nodes created, before pruning.</b> The optimiser prunes unreachable
    /// nodes once the graph is built, and for a counted repeat that is about half of them -
    /// <c>(a{100}){100}</c> creates 20,913 nodes and keeps 10,509 - so a graph the budget refuses
    /// may be smaller than the budget once finished. Created is the number worth bounding, because
    /// the memory is spent before the pruning runs.
    /// </para>
    /// <para>
    /// <b>Compile budget.</b> A pattern whose compiling creates more nodes than
    /// <c>maxCompiledNodes</c> allows is refused at construction, where upstream builds the graph
    /// until memory runs out. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#compile-budget">the comparison page</see>.
    /// </para>
    /// </remarks>
    public int MaxCompiledNodes { get; }

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
    /// <param name="timeout">
    /// How long this call may run, or <see langword="null"/> to use the pattern's
    /// <see cref="MatchTimeout"/>. Upstream's per-call <c>timeout=</c>
    /// (<c>upstream/regex/_main.py</c> lines 253-298).
    /// </param>
    /// <param name="cancellationToken">
    /// Stops the call when it is cancelled, polled on the engine's own cancellation check.
    /// </param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The call ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public bool IsMatch(
        string input,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Test(input, beginning, length, search: true, matchAll: false, timeout, cancellationToken);

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    /// <remarks>
    /// The engine keeps the subject between steps, and a span cannot be kept, so the call copies
    /// the span into a buffer rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/> and
    /// returns it when it ends. Once the pool is warm the copy allocates nothing, but it still
    /// reads every character of <paramref name="input"/> once. The
    /// <see cref="IsMatch(ReadOnlyMemory{char}, TimeSpan?, CancellationToken)"/> overload reads
    /// the caller's buffer in place.
    /// </remarks>
    public bool IsMatch(
        ReadOnlySpan<char> input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => IsMatch(input, System.Buffers.ArrayPool<char>.Shared, timeout, cancellationToken);

    /// <summary>
    /// <see cref="IsMatch(ReadOnlySpan{char}, TimeSpan?, CancellationToken)"/> with the pool it
    /// rents its copy from, which <c>PoolDisciplineTests</c> replaces with one that tracks returns.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pool">Where the copy of <paramref name="input"/> is rented from.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    internal bool IsMatch(
        ReadOnlySpan<char> input,
        System.Buffers.ArrayPool<char> pool,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        char[] copy = pool.Rent(input.Length);

        try
        {
            input.CopyTo(copy);

            // MatchState.Release drops the subject before the cached state goes back, so nothing
            // holds the copy once it is returned.
            return Test(
                new ReadOnlyMemory<char>(copy, 0, input.Length),
                0,
                -1,
                search: true,
                matchAll: false,
                timeout,
                cancellationToken
            );
        }
        finally
        {
            pool.Return(copy);
        }
    }

    /// <summary>Whether the pattern matches anywhere in the subject, read in place.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    /// <remarks>
    /// No copy of <paramref name="input"/> is made, so a long subject costs no memory of its own.
    /// Take a slice of a buffer with <c>buffer.AsMemory(start, length)</c>.
    /// </remarks>
    // null converts to ReadOnlyMemory through char[]; the lower priority keeps IsMatch(null) on the
    // string overload rather than ambiguous (MemoryOverloadTests).
    [System.Runtime.CompilerServices.OverloadResolutionPriority(-1)]
    public bool IsMatch(
        ReadOnlyMemory<char> input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Test(input, 0, -1, search: true, matchAll: false, timeout, cancellationToken);

    /// <summary>
    /// Whether the pattern matches starting exactly at <paramref name="beginning"/>. Upstream
    /// <c>Pattern.match</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject to consider, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches there.</returns>
    public bool IsMatchAtStart(
        string input,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Test(input, beginning, length, search: false, matchAll: false, timeout, cancellationToken);

    /// <summary>
    /// Whether the pattern matches the whole of the given part of the subject. Upstream
    /// <c>Pattern.fullmatch</c>.
    /// </summary>
    /// <param name="input">The subject to match.</param>
    /// <param name="beginning">Where in the subject the match must start.</param>
    /// <param name="length">
    /// How much of the subject the match must cover, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches all of it.</returns>
    public bool IsFullMatch(
        string input,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Test(input, beginning, length, search: false, matchAll: true, timeout, cancellationToken);

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
    /// <param name="timeout">The call's own time budget, or <see langword="null"/> for the pattern's.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The match, successful or not.</returns>
    private Match Run(
        string input,
        int beginning,
        int length,
        bool partial,
        bool search,
        bool matchAll,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        return Execute(
            input.AsMemory(),
            input,
            beginning,
            length,
            partial,
            search,
            matchAll,
            // The Match object, and therefore repeated captures, will be visible.
            visibleCaptures: true,
            readsGroups: true,
            timeout,
            static (regex, state, subject, status) => regex.NewMatch(state, subject, status),
            cancellationToken
        );
    }

    /// <summary>
    /// <see cref="Run(string, int, int, bool, bool, bool, TimeSpan?, CancellationToken)"/> for a
    /// caller that wants only whether it matched, so it builds no <see cref="RegularExpressions.Match"/>. Not
    /// upstream's: its <c>Pattern.search</c> always builds the match object, and a Python caller
    /// asks <c>bool(m)</c> of it.
    /// </summary>
    /// <param name="input">The subject.</param>
    /// <param name="beginning">Upstream's <c>pos</c>.</param>
    /// <param name="length">How much of the subject to consider, or <c>-1</c> for the rest.</param>
    /// <param name="search">Whether to advance the start position (upstream's <c>search</c>).</param>
    /// <param name="matchAll">Whether the match must cover the slice (upstream's <c>match_all</c>).</param>
    /// <param name="timeout">The call's own time budget, or <see langword="null"/> for the pattern's.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>Whether the pattern matched.</returns>
    /// <remarks>
    /// It passes <c>visibleCaptures: false</c>, which is safe because nothing in the engine reads
    /// the flag: upstream stores <c>state-&gt;visible_captures</c> at <c>_regex.c:18314</c> and
    /// never reads it (<c>grep -n visible_captures upstream/src/_regex.c</c>, 2026-09-22: every
    /// other hit is the compile-time <c>args</c> field), and neither does this port.
    /// </remarks>
    private bool Test(
        string input,
        int beginning,
        int length,
        bool search,
        bool matchAll,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        return Test(input.AsMemory(), beginning, length, search, matchAll, timeout, cancellationToken);
    }

    /// <summary>
    /// <see cref="Test(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/> over a
    /// subject held as memory, which the engine reads in place.
    /// </summary>
    /// <param name="input">The subject.</param>
    /// <param name="beginning">Upstream's <c>pos</c>.</param>
    /// <param name="length">How much of the subject to consider, or <c>-1</c> for the rest.</param>
    /// <param name="search">Whether to advance the start position (upstream's <c>search</c>).</param>
    /// <param name="matchAll">Whether the match must cover the slice (upstream's <c>match_all</c>).</param>
    /// <param name="timeout">The call's own time budget, or <see langword="null"/> for the pattern's.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>Whether the pattern matched.</returns>
    private bool Test(
        ReadOnlyMemory<char> input,
        int beginning,
        int length,
        bool search,
        bool matchAll,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    ) =>
        Execute(
            input,
            input,
            beginning,
            length,
            partial: false,
            search,
            matchAll,
            visibleCaptures: false,
            // Only the status is read.
            readsGroups: false,
            timeout,
            // What NewMatch reports as a successful match.
            static (_, _, _, status) => status is Engine.MatchStatus.Success or Engine.MatchStatus.Partial,
            cancellationToken
        );

    /// <summary>The body both overloads above share.</summary>
    /// <typeparam name="TSubject">What <paramref name="result"/> is handed as the subject.</typeparam>
    /// <typeparam name="TResult">What the caller builds from the finished state.</typeparam>
    /// <param name="input">The subject, as the engine reads it.</param>
    /// <param name="subject">
    /// The subject as <paramref name="result"/> wants it: the <see cref="string"/> a
    /// <see cref="RegularExpressions.Match"/> holds, or anything at all for a caller that builds none.
    /// </param>
    /// <param name="beginning">Upstream's <c>pos</c>.</param>
    /// <param name="length">How much of the subject to consider, or <c>-1</c> for the rest.</param>
    /// <param name="partial">Upstream's <c>partial</c>.</param>
    /// <param name="search">Whether to advance the start position (upstream's <c>search</c>).</param>
    /// <param name="matchAll">Whether the match must cover the slice (upstream's <c>match_all</c>).</param>
    /// <param name="visibleCaptures">Upstream's <c>visible_captures</c>.</param>
    /// <param name="readsGroups">
    /// Whether <paramref name="result"/> reads any group; when it does not, the engine may skip the
    /// captures (<see cref="Engine.MatchState.SkipCapturesIfUnread"/>).
    /// </param>
    /// <param name="timeout">The call's own time budget, or <see langword="null"/> for the pattern's.</param>
    /// <param name="result">Builds the answer from the state, the subject and the status.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>What <paramref name="result"/> built.</returns>
    private TResult Execute<TSubject, TResult>(
        ReadOnlyMemory<char> input,
        TSubject subject,
        int beginning,
        int length,
        bool partial,
        bool search,
        bool matchAll,
        bool visibleCaptures,
        bool readsGroups,
        TimeSpan? timeout,
        Func<FuzzyRegex, Engine.MatchState, TSubject, int, TResult> result,
        CancellationToken cancellationToken
    )
    {
        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);
        (int start, int end) = Limits(input.Length, beginning, length);

        Engine.MatchState state = StateCache.Rent(
            PatternObject,
            input,
            start,
            end,
            overlapped: false,
            partial,
            visibleCaptures,
            matchAll,
            limits
        );

        try
        {
            if (!readsGroups)
            {
                state.SkipCapturesIfUnread();
            }

            int status = Engine.Matcher.DoMatch(state, search);

            if (status == Engine.MatchStatus.Cancelled)
            {
                throw limits.Cancelled(input.ToString(), Pattern);
            }

            return result(this, state, subject, status);
        }
        finally
        {
            StateCache.Return(state);
        }
    }

    /// <summary>
    /// Rejects a time budget that is neither positive nor <see cref="InfiniteMatchTimeout"/>. A
    /// trust boundary, and shared so the constructor and every per-call overload reject the same
    /// values with the same message.
    /// </summary>
    /// <param name="value">The budget to check.</param>
    /// <param name="parameterName">The parameter the caller named it with.</param>
    /// <exception cref="ArgumentOutOfRangeException">The budget is zero or negative.</exception>
    private static void ValidateTimeout(TimeSpan value, string parameterName)
    {
        if (value != InfiniteMatchTimeout && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The match timeout must be positive, or FuzzyRegex.InfiniteMatchTimeout."
            );
        }
    }

    /// <summary>
    /// Converts a time budget into the <see cref="System.Diagnostics.Stopwatch"/> ticks the engine
    /// counts in.
    /// </summary>
    /// <param name="timeout">A validated budget.</param>
    /// <returns>The budget in Stopwatch ticks, or <see cref="Engine.MatchState.NoTimeout"/>.</returns>
    /// <remarks>
    /// Upstream's timeout is in clock ticks; ours is in Stopwatch ticks, which is the clock the
    /// engine reads. <c>decode_timeout</c> (<c>upstream/src/_regex.c:21056</c>) maps a negative
    /// number to "no timeout", which is exactly what <see cref="InfiniteMatchTimeout"/> is.
    /// </remarks>
    private static long ToTicks(TimeSpan timeout) =>
        timeout == InfiniteMatchTimeout
            ? Engine.MatchState.NoTimeout
            : (long)(timeout.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);

    /// <summary>
    /// Resolves one call's bounds: its time budget and its cancellation token. Called once per
    /// public operation, never on the matching path.
    /// </summary>
    /// <param name="timeout">
    /// The call's own budget, or <see langword="null"/> to use the pattern's <see cref="MatchTimeout"/>.
    /// </param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The bounds to run under.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/> is neither <see cref="InfiniteMatchTimeout"/> nor positive.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was already cancelled. Read here rather than only in
    /// the engine so that the contract holds on every path: a subject too short for the pattern
    /// never reaches the engine at all (see <see cref="Engine.Substitution.Subx"/>'s min-width
    /// shortcut), and "a cancelled token throws unless the work happened to be trivial" is
    /// unusable as a contract.
    /// </exception>
    private Engine.MatchLimits LimitsFor(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        // Validation FIRST, and before the token is read: a bad timeout is a mistake in the
        // caller's code that they have to fix, where a cancelled token is this call's outcome.
        // Reading the token first hides the mistake behind an OperationCanceledException on
        // exactly the runs where it is hardest to notice. Found by S51's blind review; the
        // built-in Regex agrees - Regex.Match("a", "a", RegexOptions.None, TimeSpan.Zero) throws
        // ArgumentOutOfRangeException whatever else is going on.
        if (timeout is { } value)
        {
            ValidateTimeout(value, nameof(timeout));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return timeout is { } budget
            ? new Engine.MatchLimits(ToTicks(budget), budget, cancellationToken, ExpiresOnCheck)
            : new Engine.MatchLimits(TimeoutTicks, MatchTimeout, cancellationToken, ExpiresOnCheck);
    }

    /// <summary>
    /// The pattern's own bounds, with no token: what an operation that takes no per-call arguments
    /// runs under. <see cref="Match.NextMatch"/> is the one such operation, and it matches the
    /// built-in <c>Regex</c>, whose <c>Match.NextMatch</c> likewise runs under the pattern's
    /// <c>MatchTimeout</c> and has nowhere to take a per-call one.
    /// </summary>
    internal Engine.MatchLimits PatternLimits =>
        new(TimeoutTicks, MatchTimeout, CancellationToken.None, ExpiresOnCheck);

    /// <summary>
    /// Resolves this surface's <c>(beginning, length)</c> pair into upstream's <c>pos</c> and
    /// <c>endpos</c>, which <c>state_init</c> then clamps (<c>get_limits</c>,
    /// <c>upstream/src/_regex.c</c> line 21627).
    /// </summary>
    /// <param name="inputLength">The subject's length.</param>
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
    private static (int Start, int End) Limits(int inputLength, int beginning, int length)
    {
        beginning = Engine.MatchState.ClampIndex(beginning, inputLength);

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
    /// <param name="beginning">Where in the subject replacing may start. Upstream's <c>pos</c>.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <param name="timeout">The call's own time budget, or <see langword="null"/> for the pattern's.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The subject with the matches replaced.</returns>
    private string Subx(
        string input,
        string? template,
        MatchEvaluator? evaluator,
        bool isFormat,
        int count,
        int beginning,
        int length,
        out int replacements,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        if (evaluator is null)
        {
            ArgumentNullException.ThrowIfNull(template);
        }

        (int start, int end) = Limits(input.Length, beginning, length);

        return Engine.Substitution.Subx(
            this,
            input,
            start,
            end,
            template,
            evaluator,
            isFormat,
            count,
            out replacements,
            LimitsFor(timeout, cancellationToken)
        );
    }

    /// <summary>The compiled pattern the engine runs, which <c>pattern_subx</c> reads as <c>self</c>.</summary>
    internal Engine.PatternObject PatternObject { get; }

    /// <summary>The pattern's capture group count, upstream's <c>public_group_count</c>.</summary>
    internal int GroupCount => _compiled.GroupCount;

    /// <summary>The match timeout in <see cref="System.Diagnostics.Stopwatch"/> ticks.</summary>
    internal long TimeoutTicks { get; }

    /// <summary>
    /// A test seam, 0 (off) everywhere but the test suite: when positive, every operation on this
    /// instance that runs under a time budget finds the budget spent at its k-th clock check, as if
    /// the deadline had passed, whatever the clock says. An operation with no budget is unaffected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The timeout tests used to need a pattern too slow to finish inside the budget, and each
    /// failure-memo extension made some of those patterns fast, so the tests broke for reasons that
    /// had nothing to do with the timeout (the seam section of
    /// <c>docs/plan/2026-10-13-linear-nested-repeats-design.md</c>). With this a test needs only a
    /// pattern that reaches the engine's first check: the one at <c>start_match</c>, which every
    /// match attempt makes before its first step because <c>MatchState.InitMatch</c> zeroes the
    /// iteration counter. A call refused before that (the minimum-width check, a prefilter) never
    /// checks the clock, so it cannot time out this way, exactly as it cannot time out for real.
    /// </para>
    /// <para>
    /// The count runs for the whole operation, so a lazy walk's k-th check can fall on its third
    /// match, which is how a test says "two matches fit inside the budget and the third does not".
    /// It lives on the instance so that tests running in parallel each have their own, and it is
    /// <c>init</c>-only, so a compiled pattern stays immutable. The cost outside tests is one more
    /// integer carried into the state and one test of it where the clock is read, which happens
    /// once per 256 turns of the matching loop.
    /// </para>
    /// </remarks>
    internal int ExpiresOnCheck { get; init; }

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

        System.Diagnostics.Debug.Assert(
            !state.SkipsCaptures,
            "C4: a Match reads its groups, so its call never skips the captures"
        );

        // Upstream's rule that a reverse match reports its two ends the other way round (:20795).
        int matchStart = state.Reverse ? state.TextPos : state.MatchPos;
        int matchEnd = state.Reverse ? state.MatchPos : state.TextPos;
        int? attemptStart = PatternObject.KeepInSubmatch ? state.AttemptStart : null;

        return new Match(
            this,
            input,
            matchStart,
            matchEnd,
            success: true,
            // Copied, because the state's arrays are about to be disposed and are reused by the next
            // match: a Match sharing them would change under its owner (copy_groups, :20621).
            // D87: with the attempt's start after them for a pattern that can need it (see
            // Match.AttemptStart).
            Engine.GroupData.PackSpans(state.Groups, _compiled.GroupCount, attemptStart),
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
            fuzzyChanges: state.FuzzyChanges.Count > 0 ? [.. state.FuzzyChanges] : null,
            scan: state.Scan
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
    /// than throwing. It holds no group data to say so: since S61 an unsuccessful match reads every
    /// group as absent, which saves a <see cref="Engine.GroupData"/> per group on every miss. Its
    /// slice is the whole subject, which nothing reads: an unsuccessful match ends the scan, so
    /// <see cref="Match.NextMatch"/> on one never searches again.
    /// </remarks>
    internal Match NoMatch(string input) =>
        new(this, input, 0, 0, success: false, [], sliceStart: 0, sliceEnd: input.Length, overlapped: false);

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
    /// or fail. Upstream's <c>partial=True</c>. A partial promises only that more text may complete
    /// the match; sometimes no text can. See <c>Match.PartialMatch</c>.
    /// </param>
    /// <param name="timeout">
    /// How long this call may run, or <see langword="null"/> to use the pattern's
    /// <see cref="MatchTimeout"/>. Upstream's per-call <c>timeout=</c>.
    /// </param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The call ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// <b>Upstream's <c>pos</c>/<c>endpos</c> are reshaped to <c>beginning</c>/<c>length</c>.</b>
    /// <paramref name="length"/> counts code units from <paramref name="beginning"/>, and
    /// <c>-1</c> means "the rest of the subject" - not upstream's Python-slice reading of a
    /// negative <c>endpos</c>, which counts back from the end. Measured 2026-09-12:
    /// <c>regex.search('abc', 0, -1)</c> searches <c>[0, 2)</c> upstream, where this port's
    /// <c>length: -1</c> means unlimited.
    /// </para>
    /// <para>
    /// <b>Reversed partial matches run out of text at the slice start.</b> With <c>(?r)</c> and
    /// <paramref name="partial"/>: <see langword="true"/>, a match that still needs characters
    /// reports a partial at <paramref name="beginning"/> - upstream answers no match on some
    /// shapes there instead. Nothing else about <paramref name="beginning"/> moves: an anchor
    /// or a lookbehind still sees it as the edge of the text.
    /// </para>
    /// <para>
    /// <b>BestMatch and EnhanceMatch rank complete matches only.</b> With either flag and
    /// <paramref name="partial"/>: <see langword="true"/>, a partial match starts at the leftmost
    /// position whose match was still open when the subject ran out, with the first fit found
    /// there, as upstream answers. It is not the start the flagged search would report once the
    /// subject is complete: <c>(?e)(dog){e&lt;=1}</c> over <c>"cat and d"</c> is a partial at
    /// (7, 9) with one insertion, while over <c>"cat and dog"</c> it is (8, 11) with no errors. A
    /// partial match has no final error count to rank by, only a lower bound.
    /// </para>
    /// <para>
    /// <b><c>Match</c> means upstream's <c>search</c>; upstream's anchored <c>match</c> is
    /// <c>MatchAtStart</c>.</b> <c>Match</c> searches anywhere, as <c>Regex.Match</c> does, and
    /// <c>MatchAtStart</c> matches only at the start position. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#match-means-upstreams-search-upstreams-anchored-match-is-matchatstart">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>This port's search prefilters never change the slow path's answer.</b> Where upstream's
    /// prefilter and its own matcher disagree, this port answers what upstream's anchored
    /// <c>match</c> answers, which a second engine confirms. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#this-ports-search-prefilters-never-change-the-slow-paths-answer">the comparison page</see>.
    /// </para>
    /// </remarks>
    public Match Match(
        string input,
        int beginning = 0,
        int length = -1,
        bool partial = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Run(input, beginning, length, partial, search: true, matchAll: false, timeout, cancellationToken);

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
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match there.</returns>
    /// <remarks>
    /// See <see cref="Match(string, int, int, bool, TimeSpan?, CancellationToken)"/>'s remarks for
    /// how a reversed partial match behaves at the slice start under <paramref name="partial"/>.
    /// <para>
    /// <b><c>Match</c> means upstream's <c>search</c>; upstream's anchored <c>match</c> is
    /// <c>MatchAtStart</c>.</b> <c>Match</c> searches anywhere, as <c>Regex.Match</c> does, and
    /// <c>MatchAtStart</c> matches only at the start position. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#match-means-upstreams-search-upstreams-anchored-match-is-matchatstart">the comparison page</see>.
    /// </para>
    /// </remarks>
    public Match MatchAtStart(
        string input,
        int beginning = 0,
        int length = -1,
        bool partial = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Run(input, beginning, length, partial, search: false, matchAll: false, timeout, cancellationToken);

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
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match all of it.</returns>
    /// <remarks>
    /// See <see cref="Match(string, int, int, bool, TimeSpan?, CancellationToken)"/>'s remarks for
    /// how a reversed partial match behaves at the slice start under <paramref name="partial"/>.
    /// <para>
    /// <b><c>BestMatch</c> and <c>EnhanceMatch</c> keep a fit that ends in trailing insertions</b>,
    /// so a <c>FullMatch</c> or a pattern ending in <c>$</c> answers what it answers without the
    /// flag; upstream can lose that match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-keep-a-fit-that-ends-in-trailing-insertions">the comparison page</see>.
    /// </para>
    /// </remarks>
    public Match FullMatch(
        string input,
        int beginning = 0,
        int length = -1,
        bool partial = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Run(input, beginning, length, partial, search: false, matchAll: true, timeout, cancellationToken);

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
    /// is yielded like any other match and is always the last one. Under BestMatch or
    /// EnhanceMatch it starts at the leftmost position some continuation could match from; see
    /// <see cref="Match(string, int, int, bool, TimeSpan?, CancellationToken)"/>'s remarks.
    /// </param>
    /// <param name="timeout">
    /// How long finding ONE match may take, or <see langword="null"/> for the pattern's budget. The
    /// collection finds its matches as they are read, so this bounds each match rather than the
    /// whole scan; see the remarks.
    /// </param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled, at whichever read is matching then.</param>
    /// <returns>
    /// The matches, leftmost first, found as they are read: nothing is matched until the collection
    /// is read.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is neither positive nor infinite.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was already cancelled. A cancellation after the call
    /// surfaces from reading the collection instead.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>There is no <c>findall</c>.</b> Upstream's <c>findall</c> returns the text of the
    /// pattern's single capturing group when it has exactly one, and a tuple of group texts when
    /// it has more - not the match text. This method returns <see cref="RegularExpressions.Match"/>
    /// objects instead, so a caller after upstream's group-text shape reads
    /// <c>Match.Groups[1].Value</c> over the result. See
    /// <see cref="Count(string, int, int, bool, TimeSpan?, CancellationToken)"/> for a count with
    /// no per-match allocation.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#there-is-no-findall">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>Lazy, as the built-in <c>Regex.Matches</c> is</b> (since 2026-10-14). The returned
    /// <see cref="MatchCollection"/> finds each match the first time a read needs it and keeps it,
    /// so reading the first two matches of a huge subject costs two matches, and
    /// <see cref="MatchCollection.Count"/> finds them all. A timeout or cancellation therefore
    /// surfaces later, from <see cref="MatchCollection.Count"/>, the indexer or enumeration, and <paramref name="timeout"/> bounds each match, as it does for
    /// <see cref="EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// and for the built-in collection. Upstream's <c>finditer</c> times the whole scan instead; a
    /// caller who needs the scan bounded as a whole times it themselves or passes a token. The
    /// answers are exactly <c>EnumerateMatches</c>' answers.
    /// </para>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// </remarks>
    public MatchCollection Matches(
        string input,
        int beginning = 0,
        int length = -1,
        bool overlapped = false,
        bool partial = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);
        (int start, int end) = Limits(input.Length, beginning, length);

        // pattern_scanner (:21122): "The MatchObject, and therefore repeated captures, will be
        // visible." Enumerate rents with visible captures, as FindAll did.
        return new MatchCollection(Engine.Iteration.Enumerate(this, input, start, end, overlapped, partial, limits));
    }

    /// <summary>
    /// Finds every match in the given part of the subject, one at a time. The lazy twin of
    /// <see cref="Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>, and what
    /// upstream's <c>finditer</c> is: a scanner the caller pulls from.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="overlapped">Whether matches may overlap. Upstream's <c>overlapped=True</c>.</param>
    /// <param name="partial">
    /// Whether the walk may end with a partial match. Upstream's <c>finditer(partial=True)</c>:
    /// the partial is yielded like any other match and is always the last one. Under BestMatch or
    /// EnhanceMatch it starts at the leftmost position some continuation could match from; see
    /// <see cref="Match(string, int, int, bool, TimeSpan?, CancellationToken)"/>'s remarks.
    /// </param>
    /// <param name="timeout">
    /// How long ONE step may run, or <see langword="null"/> for the pattern's budget. See the
    /// remarks: this is not the whole walk's budget.
    /// </param>
    /// <param name="cancellationToken">Stops the walk when it is cancelled.</param>
    /// <returns>The matches, leftmost first, found as they are asked for.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// A step ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// Yields the same matches, in the same order, as
    /// <see cref="Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>, and like
    /// it finds each one only when asked, so <c>EnumerateMatches(huge).Take(2)</c> costs two
    /// matches rather than all of them. The difference is that this keeps nothing: walking it again
    /// matches again, where a <see cref="MatchCollection"/> keeps what it found for the next read.
    /// </para>
    /// <para>
    /// <b>The clock restarts at every step</b>, because the time between two steps is the caller's,
    /// so <paramref name="timeout"/> bounds each step rather than the walk. A caller who needs the
    /// whole walk bounded should time it themselves or pass a token.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>There is no <c>findall</c>.</b> Upstream's <c>findall</c> returns the text of the
    /// capturing groups when the pattern has any; this method returns the matches, so read
    /// <c>Groups[1].Value</c> for upstream's shape. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#there-is-no-findall">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>This port's search prefilters never change the slow path's answer.</b> Where upstream's
    /// prefilter and its own matcher disagree, this port answers what upstream's anchored
    /// <c>match</c> answers, which a second engine confirms. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#this-ports-search-prefilters-never-change-the-slow-paths-answer">the comparison page</see>.
    /// </para>
    /// </remarks>
    public IEnumerable<Match> EnumerateMatches(
        string input,
        int beginning = 0,
        int length = -1,
        bool overlapped = false,
        bool partial = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        // Resolved BEFORE the iterator starts, so a bad timeout or an already-cancelled token is
        // reported from the call rather than from the first MoveNext. An iterator method's body
        // does not run until then, and "the argument you passed was invalid" arriving at the
        // foreach instead of at the call is the classic deferred-execution trap.
        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);
        (int start, int end) = Limits(input.Length, beginning, length);

        return Engine.Iteration.Enumerate(this, input, start, end, overlapped, partial, limits);
    }

    /// <summary>Finds the matches in a span one at a time, reporting only where each one lies.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="timeout">
    /// How long ONE step may run, or <see langword="null"/> for the pattern's budget, as on the
    /// <see cref="EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// overload.
    /// </param>
    /// <param name="cancellationToken">Stops the walk when it is cancelled.</param>
    /// <returns>The matches, leftmost first, found as they are asked for.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// A step ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// Shaped after <c>Regex.EnumerateMatches(ReadOnlySpan&lt;char&gt;)</c>: each match is a
    /// <see cref="ValueMatch"/>, its <see cref="ValueMatch.Index"/> and
    /// <see cref="ValueMatch.Length"/> and nothing else, so a warm walk allocates no match objects.
    /// Take the text with <c>input.Slice(match.Index, match.Length)</c>. For the groups, the
    /// fuzzy counts or the matched text as a string, use the string overload.
    /// </para>
    /// <para>
    /// The walk copies the span once, into a buffer rented from
    /// <see cref="System.Buffers.ArrayPool{T}.Shared"/>, and returns it when the walk ends. Use it
    /// in a <c>foreach</c>, which returns the buffer however the loop ends; see
    /// <see cref="ValueMatchEnumerator"/>.
    /// </para>
    /// <para>
    /// <b>There is no <c>findall</c>.</b> Upstream's <c>findall</c> returns the text of the
    /// capturing groups when the pattern has any; this method returns the matches, so read
    /// <c>Groups[1].Value</c> for upstream's shape. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#there-is-no-findall">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>This port's search prefilters never change the slow path's answer.</b> Where upstream's
    /// prefilter and its own matcher disagree, this port answers what upstream's anchored
    /// <c>match</c> answers, which a second engine confirms. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#this-ports-search-prefilters-never-change-the-slow-paths-answer">the comparison page</see>.
    /// </para>
    /// </remarks>
    public ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<char> input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => EnumerateMatches(input, System.Buffers.ArrayPool<char>.Shared, timeout, cancellationToken);

    /// <summary>
    /// <see cref="EnumerateMatches(ReadOnlySpan{char}, TimeSpan?, CancellationToken)"/> with the
    /// pool it rents its copy from, which <c>PoolDisciplineTests</c> replaces with one that tracks
    /// returns.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pool">Where the copy of <paramref name="input"/> is rented from.</param>
    /// <param name="timeout">How long one step may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the walk when it is cancelled.</param>
    /// <returns>The matches, leftmost first, found as they are asked for.</returns>
    internal ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<char> input,
        System.Buffers.ArrayPool<char> pool,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        // Checked before anything is rented, so a bad argument leaves nothing to hand back.
        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);

        return new ValueMatchEnumerator(this, input, limits, pool);
    }

    /// <summary>Counts the matches in the given part of the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="beginning">Where in the subject to start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject to consider, in UTF-16 code units, or <c>-1</c> for the rest of it.
    /// </param>
    /// <param name="overlapped">Whether matches may overlap. Upstream's <c>overlapped=True</c>.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// Upstream spells this <c>len(findall(...))</c>; counting without building a match per match
    /// is the only reason it is its own entry point.
    /// </remarks>
    public int Count(
        string input,
        int beginning = 0,
        int length = -1,
        bool overlapped = false,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);
        (int start, int end) = Limits(input.Length, beginning, length);

        return Engine.Iteration.Count(this, input.AsMemory(), start, end, overlapped, limits);
    }

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// The engine keeps the subject between steps, and a span cannot be kept, so the call copies
    /// the span into a buffer rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/> and
    /// returns it when it ends. Once the pool is warm the copy allocates nothing, but it still
    /// reads every character of <paramref name="input"/> once. The
    /// <see cref="Count(ReadOnlyMemory{char}, TimeSpan?, CancellationToken)"/> overload reads the
    /// caller's buffer in place.
    /// </remarks>
    public int Count(
        ReadOnlySpan<char> input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Count(input, System.Buffers.ArrayPool<char>.Shared, timeout, cancellationToken);

    /// <summary>
    /// <see cref="Count(ReadOnlySpan{char}, TimeSpan?, CancellationToken)"/> with the pool it
    /// rents its copy from, which <c>PoolDisciplineTests</c> replaces with one that tracks returns.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pool">Where the copy of <paramref name="input"/> is rented from.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled.</param>
    /// <returns>The number of matches.</returns>
    internal int Count(
        ReadOnlySpan<char> input,
        System.Buffers.ArrayPool<char> pool,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    )
    {
        char[] copy = pool.Rent(input.Length);

        try
        {
            input.CopyTo(copy);

            // As in IsMatch: MatchState.Release drops the subject before the state is cached.
            return Count(new ReadOnlyMemory<char>(copy, 0, input.Length), timeout, cancellationToken);
        }
        finally
        {
            pool.Return(copy);
        }
    }

    /// <summary>Counts the matches in the subject, read in place.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// No copy of <paramref name="input"/> is made, so a long subject costs no memory of its own.
    /// Take a slice of a buffer with <c>buffer.AsMemory(start, length)</c>.
    /// </remarks>
    // As for IsMatch(ReadOnlyMemory<char>): keeps Count(null) on the string overload.
    [System.Runtime.CompilerServices.OverloadResolutionPriority(-1)]
    public int Count(
        ReadOnlyMemory<char> input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);
        (int start, int end) = Limits(input.Length, 0, -1);

        return Engine.Iteration.Count(this, input, start, end, overlapped: false, limits);
    }

    /// <summary>Replaces matches with an expanded replacement template.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">
    /// The replacement template, in upstream's syntax: <c>\1</c> and <c>\g&lt;name&gt;</c> stand
    /// for captured groups and <c>$</c> is ordinary text. See <see cref="Match.Result(string)"/>
    /// for why this is not <c>Regex</c>'s <c>$1</c> language.
    /// </param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it. See <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/> for what the slice means.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The call ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// <b>Replace's <c>count</c> is inverted the same way</b> as
    /// <see cref="Split(string, int, TimeSpan?, CancellationToken)"/>'s <c>maxSplits</c>: here
    /// <c>-1</c> means "no limit" and <c>0</c> means "replace nothing", where upstream's
    /// <c>count=0</c> means "no limit" and a negative count replaces nothing.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>Replacement templates speak upstream's language.</b> <c>\1</c>, <c>\g&lt;name&gt;</c>,
    /// <c>\n</c>, <c>\x41</c> and <c>\N{...}</c> mean what they mean upstream, and <c>$</c> is
    /// ordinary text. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replacement-templates-speak-upstreams-language">the comparison page</see>.
    /// </para>
    /// </remarks>
    public string Replace(
        string input,
        string replacement,
        int count = -1,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Subx(
            input,
            replacement,
            evaluator: null,
            isFormat: false,
            count,
            beginning,
            length,
            out _,
            timeout,
            cancellationToken
        );

    /// <summary>
    /// Replaces matches with an expanded replacement template, reporting how many were replaced.
    /// Upstream <c>Pattern.subn</c>, which returns the pair.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it.
    /// </param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    /// <remarks>
    /// <b>What the slice means.</b> Matching happens only inside it, exactly as it does for
    /// <see cref="Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/> - so an
    /// anchor or a lookbehind at the edge sees the slice's start as the start - and the text
    /// OUTSIDE it is copied through unchanged rather than dropped. That is upstream's
    /// <c>sub(pos, endpos)</c>: measured on <c>regex</c> 2026.9.10, 2026-09-16,
    /// <c>regex.compile('a').sub('X', 'aaaaa', pos=1, endpos=3)</c> is <c>'aXXaa'</c>, and
    /// <c>regex.compile('^a').sub('X', 'aaaaa', pos=1)</c> is <c>'aaaaa'</c> because the anchor
    /// cannot reach position 1. The C says so as well: <c>pattern_subx</c>'s trailing segment runs
    /// to <c>str_info.length</c> rather than to the slice end
    /// (<c>upstream/src/_regex.c:22092-22097</c>).
    /// <para>
    /// <b><c>Replace</c>'s <c>count</c> is inverted the same way.</b> <c>-1</c> means no limit and
    /// <c>0</c> replaces nothing, where upstream's <c>count=0</c> replaces every match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>Replacement templates speak upstream's language.</b> <c>\1</c>, <c>\g&lt;name&gt;</c>,
    /// <c>\n</c>, <c>\x41</c> and <c>\N{...}</c> mean what they mean upstream, and <c>$</c> is
    /// ordinary text. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replacement-templates-speak-upstreams-language">the comparison page</see>.
    /// </para>
    /// </remarks>
    // The slice comes after `replacements` here and before it in every other overload, because an
    // `out` parameter has to stay where it was: moving it would be a source break for no gain.
    public string Replace(
        string input,
        string replacement,
        int count,
        out int replacements,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Subx(
            input,
            replacement,
            evaluator: null,
            isFormat: false,
            count,
            beginning,
            length,
            out replacements,
            timeout,
            cancellationToken
        );

    /// <summary>Replaces matches with text computed per match.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>Replace</c>'s <c>count</c> is inverted the same way.</b> <c>-1</c> means no limit and
    /// <c>0</c> replaces nothing, where upstream's <c>count=0</c> replaces every match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it. See <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/> for what the slice means.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(
        string input,
        MatchEvaluator evaluator,
        int count = -1,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return Subx(
            input,
            template: null,
            evaluator,
            isFormat: false,
            count,
            beginning,
            length,
            out _,
            timeout,
            cancellationToken
        );
    }

    /// <summary>
    /// Replaces matches with text computed per match, reporting how many were replaced. Upstream
    /// <c>Pattern.subn</c> with a callable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Replace</c>'s <c>count</c> is inverted the same way.</b> <c>-1</c> means no limit and
    /// <c>0</c> replaces nothing, where upstream's <c>count=0</c> replaces every match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string Replace(
        string input,
        MatchEvaluator evaluator,
        int count,
        out int replacements,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return Subx(
            input,
            template: null,
            evaluator,
            isFormat: false,
            count,
            beginning,
            length,
            out replacements,
            timeout,
            cancellationToken
        );
    }

    /// <summary>
    /// Splits the subject around the matches, including the text captured by any groups, as both
    /// upstream and the built-in <c>Regex</c> do.
    /// </summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="maxSplits">
    /// The most splits to make, or <c>-1</c> for no limit. This is upstream's <c>maxsplit</c>, a
    /// count of splits, where the <c>count</c> argument of <c>Regex.Split</c> counts the resulting
    /// pieces. The names differ because the meanings do.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the split when it is cancelled.</param>
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
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// <para>
    /// <b>Split spells "no limit" as <c>maxSplits = -1</c>.</b> Upstream spells it
    /// <c>maxsplit=0</c>, and reads a negative number as "no splits" - the opposite
    /// convention, applied at the boundary only.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-spells-no-limit-as-maxsplits---1">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>Split</c> returns <c>string?[]</c> and puts <see langword="null"/> where a capturing
    /// group did not take part</b>, as upstream puts <c>None</c>; the built-in <c>Regex.Split</c>
    /// leaves the entry out. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-returns-string-and-puts-null-where-a-capturing-group-did-not-take-part">the comparison page</see>.
    /// </para>
    /// </remarks>
    public string?[] Split(
        string input,
        int maxSplits = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        return Engine.Iteration.Split(this, input, maxSplits, LimitsFor(timeout, cancellationToken));
    }

    /// <summary>
    /// Splits the subject around the matches, producing the pieces one at a time. Upstream
    /// <c>splititer</c>; the lazy twin of
    /// <see cref="Split(string, int, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    /// <param name="input">The subject to split.</param>
    /// <param name="maxSplits">The most splits to make, or <c>-1</c> for no limit.</param>
    /// <param name="timeout">
    /// How long ONE step may run, or <see langword="null"/> for the pattern's budget. Per step,
    /// for the reason
    /// <see cref="EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// gives.
    /// </param>
    /// <param name="cancellationToken">Stops the split when it is cancelled.</param>
    /// <returns>
    /// The same pieces, in the same order, that
    /// <see cref="Split(string, int, TimeSpan?, CancellationToken)"/> returns - including the
    /// <see langword="null"/> where a capturing group took no part in a match, and including a
    /// reverse pattern's back-to-front order, which upstream does not undo. Verified against
    /// upstream 2026-09-16: <c>list(regex.compile('(?r)x').splititer('xaxbxc'))</c> and
    /// <c>regex.compile('(?r)x').split('xaxbxc')</c> are both <c>['c', 'b', 'a', '']</c>.
    /// </returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// A step ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <remarks>
    /// There is no <c>beginning</c>/<c>length</c> pair here, because
    /// <see cref="Split(string, int, TimeSpan?, CancellationToken)"/> has none and upstream has
    /// none: <c>pattern_split</c>'s keyword list is <c>string, maxsplit, concurrent, timeout</c>,
    /// so a split is always of the whole subject.
    /// <para>
    /// <b><c>Split</c> spells "no limit" as <c>maxSplits = -1</c>.</b> <c>0</c> splits nowhere,
    /// where upstream's <c>maxsplit=0</c> means no limit and a negative one means no splits. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-spells-no-limit-as-maxsplits---1">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>Split</c> returns <c>string?[]</c> and puts <see langword="null"/> where a capturing
    /// group did not take part</b>, as upstream puts <c>None</c>; the built-in <c>Regex.Split</c>
    /// leaves the entry out. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-returns-string-and-puts-null-where-a-capturing-group-did-not-take-part">the comparison page</see>.
    /// </para>
    /// </remarks>
    public IEnumerable<string?> EnumerateSplits(
        string input,
        int maxSplits = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        // Resolved before the iterator starts, as EnumerateMatches does and for the same reason.
        Engine.MatchLimits limits = LimitsFor(timeout, cancellationToken);

        return Engine.Iteration.EnumerateSplits(this, input, maxSplits, limits);
    }

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template. Upstream
    /// <c>Pattern.subf</c>; see <see cref="ReplaceFormat(string, string, string, FuzzyRegexOptions, int, int, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it. See <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/> for what the slice means.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <remarks>
    /// See <see cref="Replace(string, string, int, int, int, TimeSpan?, CancellationToken)"/>'s
    /// remarks: <b>Replace's <c>count</c> is inverted the same way</b> applies here too -
    /// <c>-1</c> is "no limit" and <c>0</c> is "replace nothing".
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </remarks>
    public string ReplaceFormat(
        string input,
        string format,
        int count = -1,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Subx(
            input,
            format,
            evaluator: null,
            isFormat: true,
            count,
            beginning,
            length,
            out _,
            timeout,
            cancellationToken
        );

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template, reporting how many were
    /// replaced. Upstream <c>Pattern.subfn</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Replace</c>'s <c>count</c> is inverted the same way.</b> <c>-1</c> means no limit and
    /// <c>0</c> replaces nothing, where upstream's <c>count=0</c> replaces every match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replaces-count-is-inverted-the-same-way">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="format">The format template.</param>
    /// <param name="count">The most replacements to make, or <c>-1</c> for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, in UTF-16 code units, or <c>-1</c> for the rest
    /// of it.
    /// </param>
    /// <param name="timeout">How long this call may run, or <see langword="null"/> for the pattern's budget.</param>
    /// <param name="cancellationToken">Stops the call when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public string ReplaceFormat(
        string input,
        string format,
        int count,
        out int replacements,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Subx(
            input,
            format,
            evaluator: null,
            isFormat: true,
            count,
            beginning,
            length,
            out replacements,
            timeout,
            cancellationToken
        );

    /// <summary>
    /// The most patterns the static conveniences on this class keep compiled at once. Fifteen by
    /// default, which is what <c>Regex.CacheSize</c> is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every static convenience here - <see cref="IsMatch(string, string, FuzzyRegexOptions, TimeSpan?, CancellationToken)"/>,
    /// <see cref="Match(string, string, FuzzyRegexOptions, IReadOnlyDictionary{string, IReadOnlyCollection{string}}, TimeSpan?, CancellationToken)"/>
    /// and the rest - compiles its pattern and then keeps it, so calling one in a loop parses the
    /// pattern once rather than once a call. The cache is keyed on the pattern text, the options
    /// exactly as they were passed, the version and the match timeout; the most recently used entry
    /// is kept and the least recently used is evicted at the bound.
    /// </para>
    /// <para>
    /// <b>A call that carries a <c>namedLists</c> dictionary is not cached.</b> That dictionary is
    /// the caller's, mutable and of no bounded size, so it cannot be part of a key, and a cache that
    /// ignored it would answer the second call from the first call's lists. Those calls compile per
    /// call, as every call did before this cache existed.
    /// </para>
    /// <para>
    /// <b>The constructors never consult it</b>, exactly as <c>new Regex(...)</c> does not. A
    /// program that compiles its patterns once and keeps them - which is still the fastest thing to
    /// do - is unaffected by this property.
    /// </para>
    /// <para>
    /// Setting it to <c>0</c> empties the cache and stops it storing anything, which is this port's
    /// answer to upstream's <c>regex.purge</c> and a little stronger - <c>purge</c> clears, this
    /// clears and disables. A smaller value evicts down to the new bound at once. Upstream's
    /// <c>regex.cache_all</c>, which decides whether an explicit <c>regex.compile</c> is cached too
    /// (<c>_main.py:359-382</c>, default on), is here fixed at its off setting: the constructors
    /// never store. See <c>docs/DIVERGENCES.md</c>.
    /// </para>
    /// </remarks>
    /// <value>The bound, which must not be negative.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public static int CacheSize
    {
        get => Cache.Size;
        set => Cache.Size = value;
    }

    /// <summary>
    /// The patterns the static conveniences have compiled lately, so a loop calling one of them
    /// does not re-parse the same pattern on every pass. The library's only shared mutable state;
    /// see <see cref="PatternCache"/> for how it is guarded. Internal rather than private because
    /// a cache is invisible in the answers it gives, so the tests that measure it need the object.
    /// </summary>
    internal static PatternCache Cache { get; } = new();

    /// <summary>
    /// The compiled pattern a static convenience should use: the cached one when the cache holds
    /// it, a freshly compiled one otherwise.
    /// </summary>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <returns>The compiled pattern.</returns>
    private static FuzzyRegex Cached(string pattern, FuzzyRegexOptions options) =>
        Cache.GetOrAdd(pattern, options, InfiniteMatchTimeout, Parsing.PatternCompiler.DefaultVersion);

    /// <summary>
    /// The compiled pattern a static convenience that takes named lists should use. A caller's
    /// dictionary is never a cache key (see <see cref="CacheSize"/>), so passing one bypasses the
    /// cache entirely rather than reading it under a key that does not describe the call.
    /// </summary>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">Values for the pattern's <c>\L&lt;name&gt;</c> references.</param>
    /// <returns>The compiled pattern.</returns>
    private static FuzzyRegex Cached(
        string pattern,
        FuzzyRegexOptions options,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists
    ) =>
        namedLists is null
            ? Cached(pattern, options)
            : new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists);

    /// <summary>Whether the pattern matches anywhere in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="timeout">How long the match may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the match when it is cancelled.</param>
    /// <returns><see langword="true"/> if the pattern matches.</returns>
    public static bool IsMatch(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options).IsMatch(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>Finds the first match anywhere in the subject. Upstream <c>regex.search</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>Match</c> means upstream's <c>search</c>; upstream's anchored <c>match</c> is
    /// <c>MatchAtStart</c>.</b> <c>Match</c> searches anywhere, as <c>Regex.Match</c> does, and
    /// <c>MatchAtStart</c> matches only at the start position. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#match-means-upstreams-search-upstreams-anchored-match-is-matchatstart">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>This port's search prefilters never change the slow path's answer.</b> Where upstream's
    /// prefilter and its own matcher disagree, this port answers what upstream's anchored
    /// <c>match</c> answers, which a second engine confirms. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#this-ports-search-prefilters-never-change-the-slow-paths-answer">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <param name="timeout">How long the match may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the match when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match.</returns>
    public static Match Match(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options, namedLists).Match(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Finds the match starting at the start of the subject. Upstream <c>regex.match</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Match</c> means upstream's <c>search</c>; upstream's anchored <c>match</c> is
    /// <c>MatchAtStart</c>.</b> <c>Match</c> searches anywhere, as <c>Regex.Match</c> does, and
    /// <c>MatchAtStart</c> matches only at the start position. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#match-means-upstreams-search-upstreams-anchored-match-is-matchatstart">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <param name="timeout">How long the match may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the match when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match there.</returns>
    public static Match MatchAtStart(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Cached(pattern, options, namedLists)
            .MatchAtStart(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>Finds the match covering the whole subject. Upstream <c>regex.fullmatch</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>BestMatch</c> and <c>EnhanceMatch</c> keep a fit that ends in trailing insertions</b>,
    /// so a <c>FullMatch</c> or a pattern ending in <c>$</c> answers what it answers without the
    /// flag; upstream can lose that match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-keep-a-fit-that-ends-in-trailing-insertions">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to match.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <param name="timeout">How long the match may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the match when it is cancelled.</param>
    /// <returns>The match, or an unsuccessful match if the pattern does not match all of it.</returns>
    public static Match FullMatch(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options, namedLists).FullMatch(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>Finds every match in the subject. Upstream <c>regex.finditer</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b>There is no <c>findall</c>.</b> Upstream's <c>findall</c> returns the text of the
    /// capturing groups when the pattern has any; this method returns the matches, so read
    /// <c>Groups[1].Value</c> for upstream's shape. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#there-is-no-findall">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <param name="timeout">
    /// How long finding one match may take, or <see langword="null"/> for no limit. The collection
    /// is lazy, so a timeout surfaces from reading it; see the instance
    /// <see cref="Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>.
    /// </param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled, at whichever read is matching then.</param>
    /// <returns>The matches, leftmost first, found as they are read.</returns>
    public static MatchCollection Matches(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options, namedLists).Matches(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Finds every match in the subject, one at a time. Upstream <c>regex.finditer</c>; the lazy
    /// twin of <see cref="Matches(string, string, FuzzyRegexOptions, IReadOnlyDictionary{string, IReadOnlyCollection{string}}, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no <c>findall</c>.</b> Upstream's <c>findall</c> returns the text of the
    /// capturing groups when the pattern has any; this method returns the matches, so read
    /// <c>Groups[1].Value</c> for upstream's shape. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#there-is-no-findall">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>This port's search prefilters never change the slow path's answer.</b> Where upstream's
    /// prefilter and its own matcher disagree, this port answers what upstream's anchored
    /// <c>match</c> answers, which a second engine confirms. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#this-ports-search-prefilters-never-change-the-slow-paths-answer">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="namedLists">
    /// The set of literal strings each <c>\L&lt;name&gt;</c> in the pattern stands for, keyed by
    /// name, or <see langword="null"/> when the pattern references none.
    /// </param>
    /// <param name="timeout">How long one step may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the walk when it is cancelled.</param>
    /// <returns>The matches, leftmost first, found as they are asked for.</returns>
    public static IEnumerable<Match> EnumerateMatches(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? namedLists = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Cached(pattern, options, namedLists)
            .EnumerateMatches(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Splits the subject around the matches, producing the pieces one at a time. Upstream
    /// <c>regex.splititer</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A lazy walk times each step, <c>Matches</c> included.</b> The clock restarts for every
    /// match, because the time between two reads is the caller's; upstream's <c>finditer</c> has
    /// one budget for the whole walk. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-walk-times-each-step-matches-included">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>Split</c> returns <c>string?[]</c> and puts <see langword="null"/> where a capturing
    /// group did not take part</b>, as upstream puts <c>None</c>; the built-in <c>Regex.Split</c>
    /// leaves the entry out. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-returns-string-and-puts-null-where-a-capturing-group-did-not-take-part">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to split.</param>
    /// <param name="pattern">The pattern to split on.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="timeout">How long one step may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the split when it is cancelled.</param>
    /// <returns>
    /// The pieces, as <see cref="Split(string, string, FuzzyRegexOptions, TimeSpan?, CancellationToken)"/>
    /// returns them.
    /// </returns>
    public static IEnumerable<string?> EnumerateSplits(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options).EnumerateSplits(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>Counts the matches in the subject.</summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="timeout">How long the scan may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the scan when it is cancelled.</param>
    /// <returns>The number of matches.</returns>
    public static int Count(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options).Count(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Replaces matches with an expanded replacement template. Upstream <c>regex.sub</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Replacement templates speak upstream's language.</b> <c>\1</c>, <c>\g&lt;name&gt;</c>,
    /// <c>\n</c>, <c>\x41</c> and <c>\N{...}</c> mean what they mean upstream, and <c>$</c> is
    /// ordinary text. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#replacement-templates-speak-upstreams-language">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="replacement">The replacement template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, or <c>-1</c> for the rest of it. See
    /// <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/>.
    /// </param>
    /// <param name="timeout">How long the operation may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the operation when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="FuzzyRegexParseException">
    /// The pattern or the template is not valid.
    /// </exception>
    public static string Replace(
        string input,
        string pattern,
        string replacement,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Cached(pattern, options)
            .Replace(
                input,
                replacement,
                beginning: beginning,
                length: length,
                timeout: timeout,
                cancellationToken: cancellationToken
            );

    /// <summary>
    /// Replaces matches with text computed per match. Upstream <c>regex.sub</c> with a callable.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="evaluator">Computes the replacement for each match.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, or <c>-1</c> for the rest of it. See
    /// <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/>.
    /// </param>
    /// <param name="timeout">How long the operation may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the operation when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string Replace(
        string input,
        string pattern,
        MatchEvaluator evaluator,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Cached(pattern, options)
            .Replace(
                input,
                evaluator,
                beginning: beginning,
                length: length,
                timeout: timeout,
                cancellationToken: cancellationToken
            );

    /// <summary>
    /// Replaces matches by expanding a <c>str.format</c>-style template, where <c>{0}</c> is the
    /// whole match and <c>{1}</c> is group 1. Upstream <c>regex.subf</c>. This is a different
    /// templating language from the one <see cref="Replace(string, string, int, int, int, TimeSpan?, CancellationToken)"/> takes,
    /// rather than a formatting option on it.
    /// </summary>
    /// <param name="input">The subject to search.</param>
    /// <param name="pattern">The pattern to apply.</param>
    /// <param name="format">The format template.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="beginning">Where in the subject replacing may start, in UTF-16 code units.</param>
    /// <param name="length">
    /// How much of the subject may be replaced in, or <c>-1</c> for the rest of it. See
    /// <see cref="Replace(string, string, int, out int, int, int, TimeSpan?, CancellationToken)"/>.
    /// </param>
    /// <param name="timeout">How long the operation may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the operation when it is cancelled.</param>
    /// <returns>The subject with the matches replaced.</returns>
    public static string ReplaceFormat(
        string input,
        string pattern,
        string format,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        int beginning = 0,
        int length = -1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) =>
        Cached(pattern, options)
            .ReplaceFormat(
                input,
                format,
                beginning: beginning,
                length: length,
                timeout: timeout,
                cancellationToken: cancellationToken
            );

    /// <summary>Splits the subject around the matches. Upstream <c>regex.split</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b><c>Split</c> returns <c>string?[]</c> and puts <see langword="null"/> where a capturing
    /// group did not take part</b>, as upstream puts <c>None</c>; the built-in <c>Regex.Split</c>
    /// leaves the entry out. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#split-returns-string-and-puts-null-where-a-capturing-group-did-not-take-part">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <param name="input">The subject to split.</param>
    /// <param name="pattern">The pattern to split on.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="timeout">How long the operation may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Stops the operation when it is cancelled.</param>
    /// <returns>
    /// The pieces of the subject, with <see langword="null"/> where a capturing group did not
    /// take part in a match. See <see cref="Split(string, int, TimeSpan?, CancellationToken)"/>.
    /// </returns>
    public static string?[] Split(
        string input,
        string pattern,
        FuzzyRegexOptions options = FuzzyRegexOptions.None,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    ) => Cached(pattern, options).Split(input, timeout: timeout, cancellationToken: cancellationToken);

    /// <summary>
    /// Escapes the characters that have a special meaning in a pattern, so the result matches the
    /// input literally. Upstream <c>regex.escape</c>.
    /// </summary>
    /// <param name="input">The text to escape.</param>
    /// <param name="specialOnly">
    /// Escape only the characters that are special in a pattern. This is the default and it is
    /// upstream's. It is also the behaviour closest to <c>Regex.Escape</c>, measured over
    /// printable ASCII: the two disagree on 5 characters of 95, against 19 of 95 for
    /// <see langword="false"/>. Pass <see langword="false"/> to escape every non-alphanumeric
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
