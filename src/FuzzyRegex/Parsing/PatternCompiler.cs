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
    )
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> kwargs =
            namedLists ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        // Guess the encoding from the class of the pattern string (upstream/regex/_main.py lines
        // 519-530). This port has no bytes patterns, so the guess is always UNICODE and the
        // TypeError and compiled-pattern branches have nothing to port.
        const int guessEncoding = RegexFlags.Unicode;

        Source source;
        Info info;
        RegexBase parsed;
        int globalFlags = flags;

        while (true)
        {
            try
            {
                source = new Source(pattern);
                info = new Info(globalFlags, kwargs, defaultVersion) { GuessEncoding = guessEncoding };
                source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
                parsed = ParseFunctions.ParsePattern(source, info);
                break;
            }
            catch (UnscopedFlagSetException unscoped)
            {
                // Remember the global flags for the next attempt.
                globalFlags = unscoped.GlobalFlags;
            }

            // Upstream also catches `error` here only to re-raise an identical one
            // (upstream/regex/_main.py lines 549-554), which loses the traceback in Python and
            // would do nothing at all here, so the exception simply propagates.
        }

        if (!source.AtEnd())
        {
            throw new FuzzyRegexParseException("unbalanced parenthesis", pattern, source.Pos);
        }

        // Check the global flags for conflicts.
        int version = (info.Flags & RegexFlags.AllVersions) != 0 ? info.Flags & RegexFlags.AllVersions : defaultVersion;
        if (version is not (0 or RegexFlags.Version0 or RegexFlags.Version1))
        {
            // Upstream raises a plain ValueError here (upstream/regex/_main.py lines 559-568)
            // rather than its own error type, and the ported exception type is this slice's call
            // (S06 closing notes). FuzzyRegexParseException it is, for two reasons: the corpus
            // pins Message against upstream's exact text, which rules out ArgumentException -
            // that appends "(Parameter 'x')" when given a parameter name and trips S3928 when
            // given none - and neither of these conflicts is reachable through the public
            // constructor, since FuzzyRegexOptions has no ASCII, LOCALE or UNICODE and
            // Version0|Version1 is rejected earlier by RegexFlags.DefaultFlags. There is
            // therefore no caller who would want a distinct type to catch. Revisit in the slice
            // that surfaces those flags as options.
            throw new FuzzyRegexParseException("VERSION0 and VERSION1 flags are mutually incompatible");
        }

        if (
            (info.Flags & RegexFlags.AllEncodings)
            is not (0 or RegexFlags.Ascii or RegexFlags.Locale or RegexFlags.Unicode)
        )
        {
            throw new FuzzyRegexParseException("ASCII, LOCALE and UNICODE flags are mutually incompatible");
        }

        // NOT PORTED: the "cannot use UNICODE flag with a bytes pattern" check - no bytes patterns.

        if ((info.Flags & RegexFlags.AllEncodings) == 0)
        {
            info.Flags |= RegexFlags.Unicode;
        }

        bool reverse = (info.Flags & RegexFlags.Reverse) != 0;

        // Upstream: fuzzy = isinstance(parsed, _Fuzzy). The Fuzzy node arrives in S13.
        const bool fuzzy = false;

        // Fix the group references. Upstream wraps and re-raises the error; see the loop above.
        parsed.FixGroups(pattern, reverse, false);

        // NOT PORTED: the DEBUG flag's parsed.dump(), which only prints.

        // Optimise the parsed pattern.
        parsed = parsed.Optimise(info, reverse);
        parsed = parsed.PackCharacters(info);

        // Get the required string.
        (int reqOffset, int[] reqChars, int reqFlags) = ParseFunctions.GetRequiredString(parsed, info.Flags);

        // Build the named lists.
        Dictionary<string, IReadOnlySet<string>> namedListsBuilt = new(StringComparer.Ordinal);
        IReadOnlySet<string>[] namedListIndexes = new IReadOnlySet<string>[info.NamedListsUsed.Count];
        if (info.NamedListsUsed.Count > 0)
        {
            // Upstream folds each value's case here with _fold_case when the entry carries case
            // flags, which needs the Unicode tables.
            throw new NotImplementedException("needs:named-lists - building the named lists is not ported yet (S13)");
        }

        ComplainUnusedArgs(kwargs, info);

        // Check the features of the groups.
        ParseFunctions.CheckGroupFeatures(info, parsed);

        // Compile the parsed pattern. The result is a list of tuples.
        List<uint[]> code = parsed.Compile(reverse);

        // Is there a group call to the pattern as a whole?
        if (info.CallRefs.TryGetValue((0, reverse, fuzzy), out int wholePatternRef))
        {
            code =
            [
                [(uint)Opcode.CallRef, (uint)wholePatternRef],
                .. code,
                [(uint)Opcode.End],
            ];
        }

        // Add the final 'success' opcode.
        code.Add([(uint)Opcode.Success]);

        // Compile the additional copies of the groups that we need.
        foreach ((RegexBase group, bool rev, bool fuz) in info.AdditionalGroups)
        {
            code.AddRange(group.Compile(rev, fuz));
        }

        // Flatten the code into a list of ints.
        List<uint> flatCode = ParseFunctions.FlattenCode(code);

        if (!parsed.HasSimpleStart())
        {
            // Get the first set, if possible.
            try
            {
                List<uint> firstsetCode = ParseFunctions.FlattenCode(
                    ParseFunctions.CompileFirstset(info, parsed.GetFirstset(reverse))
                );
                flatCode = [.. firstsetCode, .. flatCode];
            }
            catch (FirstSetErrorException)
            {
                // No usable first set; the engine simply scans from every position.
            }
        }

        // NOT PORTED: index_group, which CompiledPattern derives from GroupIndex on demand.

        return new CompiledPattern(
            info.Flags | version,
            flatCode,
            info.GroupIndex,
            namedListsBuilt,
            namedListIndexes,
            reqOffset,
            reqChars,
            reqFlags,
            info.GroupCount
        );
    }

    /// <summary>
    /// Upstream's <c>complain_unused_args</c> closure (<c>upstream/regex/_main.py</c> lines
    /// 482-490): a named list the pattern never references is a typo, not a no-op.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>ignore_unused</c> argument is not ported: only <c>regex.subf</c> and friends
    /// pass it true, and they do so for a pattern that was already compiled once. Upstream picks
    /// the name to name with <c>next(iter(...))</c> over a Python set, so which one appears in the
    /// message is not defined there either.
    /// </remarks>
    private static void ComplainUnusedArgs(IReadOnlyDictionary<string, IReadOnlyList<string>> kwargs, Info info)
    {
        string? unused = kwargs.Keys.FirstOrDefault(name =>
            !info.NamedListsUsed.Keys.Any(key => string.Equals(key.Name, name, StringComparison.Ordinal))
        );

        if (unused is not null)
        {
            throw new FuzzyRegexParseException($"unused keyword argument '{unused}'");
        }
    }

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
