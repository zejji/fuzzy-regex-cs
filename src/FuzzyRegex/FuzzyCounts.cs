using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// How many errors of each kind a fuzzy match used. Upstream <c>Match.fuzzy_counts</c>, which is
/// the tuple <c>(substitutions, insertions, deletions)</c> in that order.
/// </summary>
/// <remarks>
/// Verified against the local oracle on 2026-08-29:
/// <c>regex.match('(?:foo){e&lt;=1}', 'fou').fuzzy_counts</c> is <c>(1, 0, 0)</c> - one
/// substitution.
/// </remarks>
/// <param name="Substitutions">Characters that had to be replaced for the pattern to match.</param>
/// <param name="Insertions">Characters that had to be inserted for the pattern to match.</param>
/// <param name="Deletions">Characters that had to be deleted for the pattern to match.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FuzzyCounts(int Substitutions, int Insertions, int Deletions)
{
    /// <summary>The total number of errors: substitutions plus insertions plus deletions.</summary>
    public int Total => Substitutions + Insertions + Deletions;
}

/// <summary>
/// Where a fuzzy match used each kind of error. Upstream <c>Match.fuzzy_changes</c>, which is the
/// tuple <c>(substitution positions, insertion positions, deletion positions)</c>.
/// </summary>
/// <remarks>
/// Slot order verified against the local oracle on 2026-08-29, one error kind at a time:
/// <c>match('(?:foo){e&lt;=1}', 'fou')</c> gives <c>([2], [], [])</c>,
/// <c>fullmatch('(?:foo){e&lt;=1}', 'fooo')</c> gives <c>([], [3], [])</c>, and
/// <c>match('(?:foo){e&lt;=1}', 'fo')</c> gives <c>([], [], [2])</c>.
/// Positions are UTF-16 code-unit indices into the subject, per the string-semantics decision in
/// design spec section 4; upstream reports codepoint indices.
/// </remarks>
/// <param name="Substitutions">Subject positions at which a character was substituted.</param>
/// <param name="Insertions">Subject positions at which a character was inserted.</param>
/// <param name="Deletions">Subject positions at which a character was deleted.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FuzzyChanges(
    IReadOnlyList<int> Substitutions,
    IReadOnlyList<int> Insertions,
    IReadOnlyList<int> Deletions
);
