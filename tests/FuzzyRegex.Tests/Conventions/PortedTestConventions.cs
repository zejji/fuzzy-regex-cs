namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// The conventions <c>tools/PortTools.psm1</c> relies on when it turns a TRX report into
/// <c>docs/STATUS.md</c>. They are enforced by a test rather than by documentation because a
/// silent mis-parse would show fake progress, which is the exact failure the generated status
/// board exists to prevent (design spec section 5).
/// </summary>
internal static class PortedTestConventions
{
    /// <summary>Namespace every ported upstream test lives under.</summary>
    public const string PortedNamespaceRoot = "Fuzzy.Text.RegularExpressions.Tests.Ported";

    /// <summary>
    /// Returns one message per violated convention; an empty sequence means all conventions hold.
    /// </summary>
    /// <param name="tests">
    /// The ported tests to check: the declaring type's full namespace, the test's display name,
    /// and the reason from its <c>[Skip]</c> attribute, or <see langword="null"/> when not skipped.
    /// </param>
    public static IReadOnlyList<string> Validate(
        IEnumerable<(string Namespace, string TestName, string? SkipReason)> tests
    )
    {
        ArgumentNullException.ThrowIfNull(tests);

        List<string> violations = [];

        foreach ((string ns, string testName, string? skipReason) in tests)
        {
            if (!TryGetFeatureArea(ns, out _))
            {
                violations.Add(
                    $"{ns}.{testName}: ported tests must live in '{PortedNamespaceRoot}.<FeatureArea>', "
                        + $"so the status board can group them by feature area."
                );
            }

            if (skipReason is not null && ParseWaitingOn(skipReason) is null)
            {
                violations.Add(
                    $"{ns}.{testName}: skip reason \"{skipReason}\" must start with needs:<capability>, "
                        + $"e.g. \"needs:lookbehind - variable-length lookbehind is not implemented\"."
                );
            }
        }

        return violations;
    }

    /// <summary>
    /// Returns one message per line of a ported test source that compiles a pattern through
    /// <c>FuzzyRegex</c> instead of through <c>Ported.Upstream</c>; an empty sequence means the
    /// file conforms.
    /// </summary>
    /// <remarks>
    /// S50b made <c>Version1</c> this port's default while upstream's suite is written against
    /// <c>VERSION0</c>, so <c>Upstream</c> pins the version every ported test compiles under. One
    /// missed call site is a test that still passes and no longer measures what its provenance
    /// says it measures, which is the same silent-mis-parse failure the conventions above exist to
    /// prevent - and a new ported test arrives with every upstream sync, so prose would not hold.
    /// Naming the type in a doc comment stays legal; the check is for code that reaches it.
    /// </remarks>
    /// <param name="fileName">The source file's name, for the message.</param>
    /// <param name="lines">The file's lines, in order.</param>
    public static IReadOnlyList<string> FindDirectEngineUses(string fileName, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<string> violations = [];
        int number = 0;

        foreach (string line in lines)
        {
            number++;
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*'))
            {
                continue;
            }

            // TWO tokens on one line, after the comment tail is cut off: the FuzzyRegex TYPE, and a
            // `new`. Between them they cover every route to a compiled pattern - `new FuzzyRegex(`,
            // `FuzzyRegex.Match(`, and the target-typed `new("a")` that names no type at all and is
            // caught by its declaration instead.
            //
            // Both halves were forced by a blind review. Listing SHAPES leaked, because
            // `private static readonly FuzzyRegex[] _p = [new("a")];` matches none of them. Then
            // "names the type and does not say Upstream" leaked too, because
            // `[Upstream.Compile("a"), new("b")]` says Upstream and still compiles one pattern
            // directly - and so does a direct construction with an `Upstream.Compile` in its trailing
            // comment. Requiring a `new` also stops a string literal or a `/* */` tail that merely
            // mentions the type from reading as a violation.
            //
            // A declaration with no `new` on it - `private static readonly FuzzyRegex _r =
            // Upstream.Compile("a");` - is legal, which is what the three real ones in the tree are.
            string code = WithoutCommentTail(line);
            bool reachesTheEngine =
                NamesTheEngineType(code)
                && (
                    ConstructsSomething(code)
                    // The static entry points, which construct nothing on the line that names them.
                    || code.Contains("FuzzyRegex.", StringComparison.Ordinal)
                    // And the receiver a formatter left alone before a `.Match(` on the next line.
                    || code.Trim().Equals("FuzzyRegex", StringComparison.Ordinal)
                );

            if (reachesTheEngine)
            {
                violations.Add(
                    $"{fileName}:{number}: ported tests compile through Ported.Upstream, which pins "
                        + $"upstream's DEFAULT_VERSION = VERSION0, not through FuzzyRegex directly. This "
                        + $"line names the FuzzyRegex type and constructs: \"{trimmed}\"."
                );
            }
        }

        return violations;
    }

    /// <summary>The line with any <c>//</c> or <c>/*</c> tail removed.</summary>
    /// <param name="line">The source line.</param>
    /// <returns>The code part of the line.</returns>
    private static string WithoutCommentTail(string line)
    {
        int slashes = line.IndexOf("//", StringComparison.Ordinal);
        int block = line.IndexOf("/*", StringComparison.Ordinal);

        // Whichever comes first, ignoring the one that is absent.
        int cut = Math.Min(slashes < 0 ? int.MaxValue : slashes, block < 0 ? int.MaxValue : block);

        return cut == int.MaxValue ? line : line[..cut];
    }

    /// <summary>Whether the line constructs anything, which every route to a pattern does.</summary>
    /// <param name="code">The code part of the line.</param>
    /// <returns><see langword="true"/> if a <c>new</c> appears.</returns>
    private static bool ConstructsSomething(string code)
    {
        int at = code.IndexOf("new", StringComparison.Ordinal);
        while (at >= 0)
        {
            int after = at + 3;
            bool beforeIsWord = at > 0 && (char.IsLetterOrDigit(code[at - 1]) || code[at - 1] == '_');
            bool afterIsWord = after < code.Length && (char.IsLetterOrDigit(code[after]) || code[after] == '_');

            if (!beforeIsWord && !afterIsWord)
            {
                return true;
            }

            at = code.IndexOf("new", after, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Whether the line uses <c>FuzzyRegex</c> as a type name, rather than as the first half of
    /// <c>FuzzyRegexOptions</c> or <c>FuzzyRegexParseException</c>.
    /// </summary>
    /// <param name="line">The code part of the line.</param>
    /// <returns><see langword="true"/> if the token appears.</returns>
    private static bool NamesTheEngineType(string line)
    {
        const string name = "FuzzyRegex";

        int at = line.IndexOf(name, StringComparison.Ordinal);
        while (at >= 0)
        {
            int after = at + name.Length;
            bool beforeIsWord = at > 0 && (char.IsLetterOrDigit(line[at - 1]) || line[at - 1] == '_');
            bool afterIsWord = after < line.Length && (char.IsLetterOrDigit(line[after]) || line[after] == '_');

            if (!beforeIsWord && !afterIsWord)
            {
                return true;
            }

            at = line.IndexOf(name, after, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Extracts the feature area from a ported test's namespace:
    /// <c>Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround</c> yields <c>Lookaround</c>.
    /// </summary>
    public static bool TryGetFeatureArea(string @namespace, out string area)
    {
        area = string.Empty;

        if (@namespace is null || !@namespace.StartsWith(PortedNamespaceRoot + ".", StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = @namespace[(PortedNamespaceRoot.Length + 1)..];
        if (remainder.Length == 0)
        {
            return false;
        }

        int dot = remainder.IndexOf('.', StringComparison.Ordinal);
        area = dot < 0 ? remainder : remainder[..dot];
        return area.Length > 0;
    }

    /// <summary>
    /// Returns the capability a skipped test is waiting on: <c>lookbehind</c> from
    /// <c>"needs:lookbehind - variable-length lookbehind is not implemented"</c>, or
    /// <see langword="null"/> when the reason does not name one.
    /// </summary>
    /// <remarks>
    /// A skipped test names a <em>capability</em>, not the slice that will deliver it. Ported
    /// tests are written in phase 1, months before the slices that enable them are authored, so
    /// naming a slice id would mean inventing a plan that has not been made yet - and rewriting
    /// every skip reason each time the plan changed. A capability is something phase 1 genuinely
    /// knows, and "tests waiting on lookbehind: 47" is what the next slice's author actually
    /// needs. Slice files declare which capabilities they enable.
    /// </remarks>
    public static string? ParseWaitingOn(string skipReason)
    {
        const string prefix = "needs:";

        if (skipReason is null || !skipReason.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        int end = prefix.Length;
        while (
            end < skipReason.Length
            && (
                char.IsAsciiLetterLower(skipReason[end]) || char.IsAsciiDigit(skipReason[end]) || skipReason[end] == '-'
            )
        )
        {
            end++;
        }

        string tag = skipReason[prefix.Length..end];

        // Long enough to be meaningful, and ending at a separator rather than mid-word, so a
        // typo such as "needs:Lookbehind" is rejected instead of silently becoming a new area.
        if (tag.Length < 3 || tag.EndsWith('-'))
        {
            return null;
        }

        bool endsCleanly =
            end == skipReason.Length || char.IsWhiteSpace(skipReason[end]) || skipReason[end] is '-' or ':';

        return endsCleanly ? tag : null;
    }
}
