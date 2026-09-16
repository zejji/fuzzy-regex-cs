using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// <see cref="GroupCollection"/>'s <see cref="IReadOnlyDictionary{TKey, TValue}"/> face, which
/// S53b added so that this port matches the built-in <c>GroupCollection</c> and so that a caller
/// can ask about a group name the pattern may not have without catching anything.
/// </summary>
/// <remarks>
/// <para>
/// Two sources of truth, and each assertion names its own. The SHAPE - what <c>Keys</c> holds,
/// what <c>Count</c> counts, what an absent key does - is .NET's, measured on .NET 10.0.10 by
/// <c>tools/probes/dotnet-groupcollection-dictionary.ps1</c> on 2026-09-16. The VALUES are
/// upstream's, measured on <c>regex</c> 2026.9.10 by
/// <c>tools/probes/upstream-groupdict-and-keys.py</c> the same day; every expected
/// <c>groupdict</c>/<c>capturesdict</c> below is a line of that probe's output.
/// </para>
/// <para>
/// The two models disagree about GROUP NUMBERING, and this port follows upstream. For
/// <c>(?&lt;a&gt;a)(b)(?&lt;c&gt;c)?</c> upstream's <c>groupindex</c> is <c>{'a': 1, 'c': 3}</c>,
/// so the unnamed group is 2 and <c>Keys</c> is <c>["0", "a", "2", "c"]</c>; .NET renumbers the
/// unnamed groups first and answers <c>[0, 1, a, c]</c> for the same pattern. Both are "every
/// group, by ascending number, keyed by its name or its number as text" - which is the rule this
/// port implements.
/// </para>
/// </remarks>
public sealed class GroupDictionaryViewTests
{
    /// <summary>
    /// The dictionary face of a match's groups, and the whole subject of this file: every
    /// assertion below goes through the interface rather than through
    /// <see cref="GroupCollection"/>'s own members, because the interface is what S53b added.
    /// </summary>
    /// <param name="match">The match.</param>
    /// <returns>Its groups, seen as a dictionary.</returns>
    /// <remarks>
    /// CA1859 wants the concrete type back here, on the grounds that an interface return costs an
    /// interface dispatch. Disapplied for this file in <c>.editorconfig</c>: the dispatch is the
    /// thing under test, so taking the rule's advice would delete the test.
    /// </remarks>
    private static IReadOnlyDictionary<string, Group> Dictionary(Match match) => match.Groups;

    [Test]
    public void Keys_are_every_group_by_ascending_number_with_unnamed_groups_as_their_number()
    {
        // upstream groupindex {'a': 1, 'c': 3}, so group 2 is the unnamed (b).
        IReadOnlyDictionary<string, Group> groups = Dictionary(FuzzyRegex.MatchAtStart("ab", "(?<a>a)(b)(?<c>c)?"));

        groups.Keys.Should().Equal("0", "a", "2", "c");
        groups.Count.Should().Be(4);
        groups.Values.Select(static g => g.Success).Should().Equal(true, true, true, false);
    }

    [Test]
    public void A_pattern_with_no_named_groups_keys_every_group_by_its_number()
    {
        IReadOnlyDictionary<string, Group> groups = Dictionary(FuzzyRegex.MatchAtStart("ab", "(a)(b)"));

        groups.Keys.Should().Equal("0", "1", "2");
    }

    [Test]
    public void The_pair_enumerator_and_the_group_enumerator_walk_the_same_groups()
    {
        Match match = FuzzyRegex.MatchAtStart("ab", "(?<a>a)(b)(?<c>c)?");

        // foreach over the collection itself still yields groups - the one IEnumerable<T> the
        // class offers to foreach, as the built-in GroupCollection does.
        //
        // Compared by what a group SAYS rather than by reference: the indexer builds a fresh
        // Group per call, so two walks of the same collection never hand back the same object and
        // Group has no value equality of its own.
        static (string Name, int Index, int Length, bool Success) Shape(Group group) =>
            (group.Name, group.Index, group.Length, group.Success);

        List<Group> byGroup = [.. match.Groups];
        List<KeyValuePair<string, Group>> byPair = [.. Dictionary(match)];

        byPair.Select(static pair => pair.Key).Should().Equal("0", "a", "2", "c");
        byPair.Select(static pair => Shape(pair.Value)).Should().Equal(byGroup.Select(Shape));
    }

    [Test]
    public void ContainsKey_answers_for_a_group_that_did_not_take_part_and_refuses_an_unknown_name()
    {
        IReadOnlyDictionary<string, Group> groups = Dictionary(FuzzyRegex.MatchAtStart("ab", "(?<a>a)(b)(?<c>c)?"));

        // 'c' is declared and did not match - upstream's groupdict has it as None rather than
        // leaving it out - so the pattern HAS the group and the dictionary says so.
        groups.ContainsKey("c").Should().BeTrue();
        groups.ContainsKey("a").Should().BeTrue();
        groups.ContainsKey("2").Should().BeTrue();
        groups.ContainsKey("nope").Should().BeFalse();

        // The numeric spelling reaches ONLY a group with no name of its own: group 1 here is
        // named 'a', so its key is 'a' and "1" is not a key at all. That is the built-in
        // collection's answer too (probe, 2026-09-16), reached there by the same round trip.
        groups.ContainsKey("1").Should().BeFalse();
        groups.ContainsKey("3").Should().BeFalse();
        groups["2"].Value.Should().Be("b");
    }

    [Test]
    public void TryGetValue_is_the_non_throwing_way_to_ask_where_the_indexer_throws()
    {
        GroupCollection groups = FuzzyRegex.MatchAtStart("ab", "(?<a>a)(b)(?<c>c)?").Groups;

        groups.TryGetValue("a", out Group? found).Should().BeTrue();
        found!.Value.Should().Be("a");

        groups.TryGetValue("c", out Group? absent).Should().BeTrue();
        absent!.Success.Should().BeFalse();

        groups.TryGetValue("nope", out Group? missing).Should().BeFalse();
        missing.Should().BeNull();

        // The indexer keeps this port's older behaviour, which is not .NET's: .NET's
        // Groups["nope"] is an unsuccessful group with an empty Name (measured in the probe),
        // ours throws. TryGetValue is the member that closes that gap.
        Action indexer = () => _ = groups["nope"];
        indexer.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void A_null_key_is_rejected_at_the_boundary()
    {
        GroupCollection groups = FuzzyRegex.MatchAtStart("ab", "(?<a>a)(b)").Groups;

        Action contains = () => groups.ContainsKey(null!);
        Action tryGet = () => groups.TryGetValue(null!, out _);

        contains.Should().Throw<ArgumentNullException>().WithParameterName("key");
        tryGet.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    /// <summary>
    /// Upstream's <c>groupdict</c> re-expressed through the dictionary view: the named groups
    /// only, with <see langword="null"/> where upstream has <c>None</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">
    /// Upstream's <c>groupdict</c>, rendered as <c>name=value</c> lines with <c>&lt;None&gt;</c>
    /// for a group that took no part. Straight from
    /// <c>tools/probes/upstream-groupdict-and-keys.py</c>, regex 2026.9.10, 2026-09-16.
    /// </param>
    [Test]
    [Arguments("(?P<first>first) (?P<second>second)", "first second", "first=first|second=second")]
    [Arguments(@"(?&routine)(?(DEFINE)(?<routine>.))", "a", "routine=<None>")]
    [Arguments(@"(?(DEFINE)(?<func>.))(?&func)", "abc", "func=<None>")]
    [Arguments("(?<a>a)(b)(?<c>c)?", "ab", "a=a|c=<None>")]
    [Arguments("(?<x>a)|(?<y>b)", "b", "x=<None>|y=b")]
    [Arguments("(?<r>[ab])+", "aba", "r=a")]
    public void The_named_slice_of_the_dictionary_view_is_upstreams_groupdict(
        string pattern,
        string subject,
        string expected
    )
    {
        // groupdict is the NAMED groups only, so the view is filtered: a key that is just the
        // group's number is a group with no name of its own.
        string actual = string.Join(
            "|",
            Dictionary(FuzzyRegex.Match(subject, pattern))
                .Where(static pair => !pair.Key.All(char.IsAsciiDigit))
                .Select(static pair => $"{pair.Key}={(pair.Value.Success ? pair.Value.Value : "<None>")}")
        );

        actual.Should().Be(expected);
    }

    /// <summary>
    /// Upstream's <c>capturesdict</c> re-expressed through the dictionary view. It differs from
    /// <c>groupdict</c> on a <c>(?(DEFINE)...)</c> group, which records a capture while reporting
    /// no group value.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">
    /// Upstream's <c>capturesdict</c>, rendered as <c>name=[a,b]</c> lines. From the same probe
    /// run.
    /// </param>
    [Test]
    [Arguments("(?P<first>first) (?P<second>second)", "first second", "first=[first]|second=[second]")]
    [Arguments(@"(?&routine)(?(DEFINE)(?<routine>.))", "a", "routine=[a]")]
    [Arguments(@"(?(DEFINE)(?<func>.))(?&func)", "abc", "func=[a]")]
    [Arguments("(?<a>a)(b)(?<c>c)?", "ab", "a=[a]|c=[]")]
    [Arguments("(?<x>a)|(?<y>b)", "b", "x=[]|y=[b]")]
    [Arguments("(?<r>[ab])+", "aba", "r=[a,b,a]")]
    public void The_named_slice_of_the_dictionary_view_is_upstreams_capturesdict(
        string pattern,
        string subject,
        string expected
    )
    {
        string actual = string.Join(
            "|",
            Dictionary(FuzzyRegex.Match(subject, pattern))
                .Where(static pair => !pair.Key.All(char.IsAsciiDigit))
                .Select(static pair =>
                    $"{pair.Key}=[{string.Join(",", pair.Value.Captures.Select(static c => c.Value))}]"
                )
        );

        actual.Should().Be(expected);
    }
}
