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
    public static IReadOnlyList<string> Validate(IEnumerable<(string Namespace, string TestName, string? SkipReason)> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);

        List<string> violations = [];

        foreach ((string ns, string testName, string? skipReason) in tests)
        {
            if (!TryGetFeatureArea(ns, out _))
            {
                violations.Add(
                    $"{ns}.{testName}: ported tests must live in '{PortedNamespaceRoot}.<FeatureArea>', " +
                    $"so the status board can group them by feature area.");
            }

            if (skipReason is not null && ParseWaitingOn(skipReason) is null)
            {
                violations.Add(
                    $"{ns}.{testName}: skip reason \"{skipReason}\" must start with needs:<capability>, " +
                    $"e.g. \"needs:lookbehind - variable-length lookbehind is not implemented\".");
            }
        }

        return violations;
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
        while (end < skipReason.Length && (char.IsAsciiLetterLower(skipReason[end])
                                           || char.IsAsciiDigit(skipReason[end])
                                           || skipReason[end] == '-'))
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

        bool endsCleanly = end == skipReason.Length
                           || char.IsWhiteSpace(skipReason[end])
                           || skipReason[end] is '-' or ':';

        return endsCleanly ? tag : null;
    }
}
