namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Pattern text in, bytecode out: the port of <c>_main._compile</c>
/// (<c>upstream/regex/_main.py</c> lines 460-686), minus the pattern cache.
/// </summary>
/// <remarks>
/// The compile-parity corpus (<c>tests/FuzzyRegex.Tests/Gaps/CompileParity/corpus.json</c>) drives
/// these two methods over every pattern upstream's own test suite compiles. Through Phase 2 a slice
/// that ported one construct turned on exactly the rows that construct reached and left the rest
/// skipped; **since S13 closed the parser, all 1659 rows pass and none skips**, so any row that
/// starts skipping again is a regression, not a gap.
/// </remarks>
internal static class PatternCompiler
{
    /// <summary>
    /// The version a pattern gets when neither the flags nor an inline <c>(?V0)</c> /
    /// <c>(?V1)</c> pick one. Upstream's <c>DEFAULT_VERSION</c>.
    /// </summary>
    /// <remarks>
    /// <b>DIVERGES FROM UPSTREAM</b> (owner decision 2026-09-14, design spec amendment 24;
    /// <c>docs/DIVERGENCES.md</c>). Upstream's <c>_main.py</c> line 443 sets
    /// <c>DEFAULT_VERSION = VERSION0</c> so that <c>regex</c> stays a drop-in for Python's
    /// <c>re</c>; this port has no <c>re</c> users to protect, and the two behaviours
    /// <c>VERSION1</c> adds - nested sets with set operations, and full case-folding - are the
    /// reason to use this library over <c>System.Text.RegularExpressions</c>. Measured on
    /// 2026-09-14 (<c>tools/probes/upstream-version-defaults.py</c>), those two and a
    /// backreference to an open group are the only live differences left; the zero-width and
    /// inline-flag differences upstream's README still lists stopped existing when <c>VERSION0</c>
    /// was brought in line with <c>re</c> 3.7+.
    /// <para>
    /// A caller who wants upstream's reading passes <see cref="FuzzyRegexOptions.Version0"/> or
    /// writes <c>(?V0)</c>. The ported upstream suite does it through one helper
    /// (<c>tests/FuzzyRegex.Tests/Ported/Upstream.cs</c>), so that it keeps measuring upstream.
    /// </para>
    /// </remarks>
    internal const int DefaultVersion = (int)FuzzyRegexOptions.Version1;

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
        try
        {
            return CompileUnderVersion(pattern, flags, namedLists, defaultVersion);
        }
        catch (FuzzyRegexParseException unterminated)
            when (string.Equals(unterminated.Message, _unterminatedSet, StringComparison.Ordinal)
                && defaultVersion == RegexFlags.Version1
                && (flags & RegexFlags.AllVersions) == 0
            )
        {
            // NOT UPSTREAM'S MESSAGE (S50b; docs/DIVERGENCES.md). Under version 1 an unescaped `[`
            // inside a set opens a NESTED set, so `[[]` and `[a[b]` - accepted by `re`, by
            // `System.Text.RegularExpressions` and under `Version0` - stop compiling, and upstream's
            // bare "unterminated character set" tells a caller arriving from `Regex` nothing about
            // why their working pattern broke. Upstream can afford the bare text because its own
            // DEFAULT_VERSION is VERSION0, where the nested reading does not exist.
            //
            // The condition is the WHOLE pattern compiling under version 0, decided by compiling it,
            // because nothing cheaper is true. Two blind reviews killed two heuristics that looked
            // equivalent and were not: a parse-wide "we read a nested set" flag advised Version0 on
            // `[[a-z]--[aeiou]]x[`, whose nested part is a legal set operation and whose trailing `[`
            // is the whole problem, and scoping that flag to one top-level set still advised it on
            // `[[a]--[b`, where the nested set is inside the set that fails. Version 0 rejects both.
            //
            // It costs a second parse of a pattern that has already failed, and it cannot recurse:
            // the retry names Version0 explicitly, so this arm's `defaultVersion == Version1` guard
            // is false inside it. Skipped entirely when the caller or the pattern already chose a
            // version, since then version 1 is not what the caller got by default.
            if (!CompilesUnderVersion0(pattern, flags, namedLists))
            {
                throw;
            }

            throw new FuzzyRegexParseException(
                _unterminatedNestedSet,
                unterminated.Pattern ?? pattern,
                unterminated.Offset
            );
        }
    }

    /// <summary>Upstream's message for a set that runs off the end of the pattern.</summary>
    private const string _unterminatedSet = "unterminated character set";

    /// <summary>
    /// The same thing for a pattern that version 0 accepts, so the only thing wrong with it is that
    /// version 1 reads a <c>[</c> inside a set as a nested-set opener.
    /// </summary>
    private const string _unterminatedNestedSet =
        _unterminatedSet
        + @": under version 1 - this library's default - an unescaped '[' inside a set opens a NESTED "
        + @"set, which needs its own ']'. This pattern compiles as written under version 0. Escape "
        + @"the '[' as '\[' to keep version 1, or compile with FuzzyRegexOptions.Version0 (or write "
        + @"'(?V0)'), where a '[' inside a set is already literal, as it is in re and "
        + @"System.Text.RegularExpressions.";

    /// <summary>
    /// Whether the pattern that just failed to compile would compile under upstream's default
    /// version, which is what makes the nested-set advice above true rather than plausible.
    /// </summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="flags">The caller's flags.</param>
    /// <param name="namedLists">The caller's named lists.</param>
    /// <returns><see langword="true"/> if version 0 accepts it.</returns>
    private static bool CompilesUnderVersion0(
        string pattern,
        int flags,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? namedLists
    )
    {
        try
        {
            CompileUnderVersion(pattern, flags, namedLists, RegexFlags.Version0);
            return true;
        }
        catch (FuzzyRegexParseException)
        {
            return false;
        }
    }

    private static CompiledPattern CompileUnderVersion(
        string pattern,
        int flags,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? namedLists,
        int defaultVersion
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

        bool fuzzy = parsed is Fuzzy;

        // Fix the group references. Upstream wraps and re-raises the error; see the loop above.
        parsed.FixGroups(pattern, reverse, false);

        // NOT PORTED: the DEBUG flag's parsed.dump(), which only prints.

        // Optimise the parsed pattern.
        parsed = parsed.Optimise(info, reverse);
        parsed = parsed.PackCharacters(info);

        // Get the required string.
        (long reqOffset, int[] reqChars, int reqFlags) = ParseFunctions.GetRequiredString(parsed, info.Flags);

        // Build the named lists.
        Dictionary<string, IReadOnlySet<string>> namedListsBuilt = new(StringComparer.Ordinal);
        IReadOnlySet<string>[] namedListIndexes = new IReadOnlySet<string>[info.NamedListsUsed.Count];
        foreach (((string name, int caseFlags), int index) in info.NamedListsUsed)
        {
            HashSet<string> values = new(kwargs[name], StringComparer.Ordinal);
            IReadOnlySet<string> items =
                caseFlags != 0
                    ? new HashSet<string>(values.Select(v => FoldCase(info, v)), StringComparer.Ordinal)
                    : values;
            namedListsBuilt[name] = values;
            namedListIndexes[index] = items;

            // NOT PORTED: upstream's args_needed, which exists only to feed the pattern cache and
            // the unused-argument check; ComplainUnusedArgs reads info.NamedListsUsed instead.
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
    /// Upstream <c>_fold_case</c> (<c>upstream/regex/_regex_core.py</c> lines 354-360), over a
    /// whole string rather than a codepoint array.
    /// </summary>
    /// <remarks>
    /// The encoding fallback is upstream's and is unreachable here: <c>Compile</c> has already
    /// forced <see cref="RegexFlags.Unicode"/> on by the time the named lists are built. It is kept
    /// because a future caller earlier in the pipeline would need it.
    /// </remarks>
    private static string FoldCase(Info info, string value)
    {
        int flags = info.Flags;
        if ((flags & RegexFlags.AllEncodings) == 0)
        {
            flags |= info.GuessEncoding;
        }

        // Whole codepoints in, whole codepoints out, as upstream's str does. A lone surrogate is a
        // legal element of a Python str, so it is passed through rather than replaced.
        List<int> codepoints = [];
        int i = 0;
        while (i < value.Length)
        {
            bool pair = char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]);
            codepoints.Add(pair ? char.ConvertToUtf32(value[i], value[i + 1]) : value[i]);
            i += pair ? 2 : 1;
        }

        var folded = new System.Text.StringBuilder(value.Length);
        foreach (int c in Unicode.RegexModule.FoldCase(flags, [.. codepoints]))
        {
            _ = c <= char.MaxValue ? folded.Append((char)c) : folded.Append(char.ConvertFromUtf32(c));
        }

        return folded.ToString();
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
    /// <param name="replacement">The replacement template.</param>
    /// <param name="groupCount">The pattern's capture group count, for validating <c>\g&lt;n&gt;</c>.</param>
    /// <param name="groupIndex">The pattern's group names, for resolving <c>\g&lt;name&gt;</c>.</param>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    internal static IReadOnlyList<object> CompileReplacement(
        string replacement,
        int groupCount,
        IReadOnlyDictionary<string, int> groupIndex
    )
    {
        ArgumentNullException.ThrowIfNull(replacement);

        // NOT PORTED: the replacement cache, for the same reason the pattern cache is not.
        var source = new Source(replacement);
        List<object> compiled = [];
        List<long> literal = [];

        while (true)
        {
            int ch = source.Get();
            if (ch == Source.EndOfSource)
            {
                break;
            }

            if (ch == '\\')
            {
                // ParseFunctions.CompileReplacement returns either a group number or the character
                // codes of a literal. It returns items (plural) in order to handle a 2-character
                // literal (an invalid escape sequence).
                (bool isGroup, long[] items) = ParseFunctions.CompileReplacement(source, groupCount, groupIndex);
                if (isGroup)
                {
                    // It's a group, so first flush the literal.
                    if (literal.Count > 0)
                    {
                        compiled.Add(MakeString(literal));
                        literal.Clear();
                    }

                    foreach (long item in items)
                    {
                        compiled.Add((int)item);
                    }
                }
                else
                {
                    literal.AddRange(items);
                }
            }
            else
            {
                literal.Add(ch);
            }
        }

        // Flush the literal.
        if (literal.Count > 0)
        {
            compiled.Add(MakeString(literal));
        }

        return compiled;
    }

    /// <summary>
    /// Upstream's <c>make_string</c> closure (<c>upstream/regex/_main.py</c> lines 703-704):
    /// <c>"".join(chr(c) for c in char_codes)</c>.
    /// </summary>
    /// <remarks>
    /// A codepoint above U+10FFFF is reachable - <c>\UFFFFFFFF</c> is a well-formed escape that
    /// <c>parse_repl_hex_escape</c> does not range-check - and upstream fails it here, with an
    /// uncaught <c>ValueError</c> from <c>chr()</c> rather than with its own error type. As with
    /// the patterns in <c>Gaps/Parsing/UpstreamInternalErrorTests</c>, there is no specified
    /// behaviour to port; what matters is that the template is rejected.
    /// </remarks>
    private static string MakeString(List<long> charCodes)
    {
        var text = new System.Text.StringBuilder(charCodes.Count);
        foreach (long code in charCodes)
        {
            if (code <= char.MaxValue)
            {
                // Includes a lone surrogate, which chr() also produces happily.
                text.Append((char)code);
            }
            else if (code <= 0x10FFFF)
            {
                text.Append(char.ConvertFromUtf32((int)code));
            }
            else
            {
                throw new NotSupportedException(
                    $"chr({code}) would raise ValueError: chr() arg not in range(0x110000)"
                );
            }
        }

        return text.ToString();
    }
}
