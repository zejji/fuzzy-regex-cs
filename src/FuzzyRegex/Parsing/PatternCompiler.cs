namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Pattern text in, bytecode out: the port of <c>_main._compile</c>
/// (<c>upstream/regex/_main.py</c> lines 460-686), minus the pattern cache.
/// </summary>
/// <remarks>
/// Everything here throws until the parser slices land. The compile-parity corpus
/// (<c>tests/FuzzyRegex.Tests/Gaps/CompileParity/corpus.json</c>) drives these two methods over
/// every pattern upstream's own test suite compiles, so a slice that ports one construct turns on
/// exactly the rows that construct reaches and leaves the rest skipped.
/// </remarks>
internal static class PatternCompiler
{
    /// <summary>
    /// Upstream's <c>DEFAULT_VERSION</c> (<c>upstream/regex/__init__.py</c>): the version a
    /// pattern gets when neither the flags nor an inline <c>(?V0)</c> / <c>(?V1)</c> pick one.
    /// </summary>
    internal const int DefaultVersion = (int)FuzzyRegexOptions.Version0;

    /// <summary>
    /// Compiles a pattern to upstream's bytecode.
    /// </summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="flags">
    /// The caller's flags, as upstream's <c>RegexFlag</c> bit values. A plain <see cref="int"/>
    /// rather than <see cref="FuzzyRegexOptions"/> because upstream's suite compiles patterns
    /// under <c>ASCII</c>, <c>LOCALE</c>, <c>UNICODE</c> and <c>WORD</c>, which this port does not
    /// expose on its public enum.
    /// </param>
    /// <param name="namedLists">
    /// Values for each <c>\L&lt;name&gt;</c> in the pattern, or null when it has none. Upstream
    /// takes these as keyword arguments. A list, not a set, because the caller's order reaches
    /// the bytecode: <c>StringSet.__init__</c> (<c>upstream/regex/_regex_core.py</c> lines
    /// 4088-4100) sorts its branches by length with a stable sort, so two equal-length members
    /// keep the order they came in. The public <c>FuzzyRegex</c> surface types this as
    /// <c>IReadOnlyCollection</c> (S04), which does not carry that order - see DECISIONS
    /// 2026-08-30.
    /// </param>
    /// <param name="defaultVersion">
    /// The version to use when the pattern picks none. Upstream reads a module global here, which
    /// its own test suite never changes; a parameter keeps that visible rather than hidden in
    /// static state.
    /// </param>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    internal static CompiledPattern Compile(
        string pattern,
        int flags = 0,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? namedLists = null,
        int defaultVersion = DefaultVersion
    ) => throw new NotImplementedException("needs:parser - no construct is ported yet");

    /// <summary>
    /// Compiles a replacement template into the alternating form the substitution code consumes:
    /// a boxed <see cref="int"/> for a group reference, a <see cref="string"/> for a literal run.
    /// Port of <c>_main._compile_replacement_helper</c> (<c>upstream/regex/_main.py</c> lines
    /// 687-741).
    /// </summary>
    /// <remarks>
    /// Takes the group count and the group index rather than a <see cref="CompiledPattern"/>,
    /// because those two are all upstream's <c>compile_repl_group</c>
    /// (<c>upstream/regex/_regex_core.py</c> lines 1902-1918) reads from the pattern. Templates
    /// are therefore verifiable before any pattern compiles.
    /// </remarks>
    /// <param name="template">The replacement template.</param>
    /// <param name="groupCount">The pattern's capture group count, for validating <c>\g&lt;n&gt;</c>.</param>
    /// <param name="groupIndex">The pattern's group names, for resolving <c>\g&lt;name&gt;</c>.</param>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    internal static IReadOnlyList<object> CompileReplacement(
        string template,
        int groupCount,
        IReadOnlyDictionary<string, int> groupIndex
    ) => throw new NotImplementedException("needs:substitution - the template compiler is not ported yet");
}
