using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// How many errors of each kind a fuzzy match used. Upstream <c>Match.fuzzy_counts</c>, which is
/// the tuple <c>(substitutions, insertions, deletions)</c> in that order.
/// </summary>
/// <remarks>
/// <para>
/// Verified against the local oracle on 2026-08-29:
/// <c>regex.match('(?:foo){e&lt;=1}', 'fou').fuzzy_counts</c> is <c>(1, 0, 0)</c> - one
/// substitution.
/// </para>
/// <para>
/// The three kinds are edits to the pattern that turn it into the matched text, as upstream
/// counts them. Against <c>(?:foobar){e&lt;2}</c>, 'foxbar' is a substitution (the 'x' stands in for
/// an 'o'), 'fooxbar' an insertion (the 'x' is extra text) and 'fobar' a deletion (an 'o' of the
/// pattern has no character in the text). A constraint's test, as in <c>{e&lt;=1:[a-z]}</c>, applies
/// only to the text characters that substitutions and insertions bring in; a deletion brings in
/// none, so the test never sees it.
/// </para>
/// </remarks>
/// <param name="Substitutions">Text characters matched in place of a different pattern character.</param>
/// <param name="Insertions">Text characters the pattern has no character for.</param>
/// <param name="Deletions">Pattern characters with no character in the text.</param>
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
/// design spec section 4; upstream reports codepoint indices. The kinds are edits to the pattern,
/// as <see cref="FuzzyCounts"/> describes.
/// </remarks>
/// <param name="Substitutions">
/// Subject positions of the characters matched in place of a different pattern character.
/// </param>
/// <param name="Insertions">Subject positions of the characters the pattern has no character for.</param>
/// <param name="Deletions">
/// Where each pattern character with no character in the text would go. Each deletion's position
/// is its subject position plus the number of deletions before it, as upstream's README shows with
/// 'anaconda f~~oo bar', so a position can lie past the match's end:
/// <c>(?:ab){d&lt;=1}(?:ab){d&lt;=1}</c> over 'aa' deletes a 'b' at subject positions 1 and 2 and
/// reports [1, 3].
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FuzzyChanges(
    IReadOnlyList<int> Substitutions,
    IReadOnlyList<int> Insertions,
    IReadOnlyList<int> Deletions
);
