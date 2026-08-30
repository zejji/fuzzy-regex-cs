using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.NamedLists;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_named_lists</c>
/// (lines 2558-2611).
/// </summary>
/// <remarks>
/// <para>
/// A named list is a set of literal strings supplied at compile time that the pattern refers to
/// as <c>\L&lt;name&gt;</c>. Upstream passes them as keyword arguments to <c>regex.compile</c> and
/// the module-level functions; C# has no keyword arguments, so they travel through the
/// <c>namedLists</c> parameter added to the constructor and the static conveniences in S04.
/// It is not sugar for an alternation - the engine does a set lookup, which is why it composes
/// with full case-folding and with fuzzy matching.
/// </para>
/// <para>
/// Assertions <c>#4-6</c> are the <c>bytes</c> repeats of <c>#1-3</c> and are not ported; see
/// <c>docs/PORTMAP.md</c>. Assertions <c>#15-16</c> use no named list at all - they are full
/// case-folding backreference tests that happen to live in this method - and are ported in
/// <c>Ported/CaseFolding/FullCaseBackreferenceTests.cs</c>.
/// </para>
/// <para>
/// Every subject here is BMP-only, so upstream's codepoint spans are also the UTF-16 spans.
/// Measured, not assumed: <c>straße</c> is 6 code units and <c>SKİTS</c> is 5. Spans
/// read back from the local Python oracle on 2026-08-30.
/// </para>
/// </remarks>
public sealed class NamedListTests
{
    // Written with explicit code-unit casts rather than escapes or literal UTF-8, because a
    // precomposed and a decomposed form are indistinguishable by eye in a source file.
    private static readonly string _strasseLower = "stra" + (char)0x00DF + "e"; // straße
    private static readonly string _skitsDotted = "SK" + (char)0x0130 + "TS"; // SKİTS

    private static readonly Dictionary<string, IReadOnlyCollection<string>> _bar = new(StringComparer.Ordinal)
    {
        ["bar"] = ["one", "two", "three"],
    };

    [Test]
    [Arguments("333\\L<bar>444", "333one444")]
    [Arguments("(?i)333\\L<bar>444", "333TWO444")]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#1-2")]
    public void A_named_list_matches_any_of_its_entries(string pattern, string subject) =>
        FuzzyRegex.MatchAtStart(subject, pattern, FuzzyRegexOptions.None, _bar).Value.Should().Be(subject);

    [Test]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#3")]
    public void A_named_list_does_not_match_a_word_outside_it() =>
        FuzzyRegex
            .MatchAtStart("333four444", "333\\L<bar>444", FuzzyRegexOptions.None, _bar)
            .Success.Should()
            .BeFalse();

    [Test]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#7")]
    public void The_same_named_list_can_be_referenced_twice_and_quantified()
    {
        Action act = () => _ = new FuzzyRegex("3\\L<bar>4\\L<bar>+5", FuzzyRegexOptions.None, _bar);

        act.Should().NotThrow();
    }

    // The entries are matched as literals, so the regex metacharacters in "+s\ol[i}d" are not
    // special - and "+solid" in the second case is found only because it is there literally.
    [Test]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#8")]
    public void Named_list_entries_are_literals_not_patterns()
    {
        Dictionary<string, IReadOnlyCollection<string>> options = new(StringComparer.Ordinal)
        {
            ["options"] = ["good", "brilliant", "+s\\ol[i}d"],
        };

        FuzzyRegex.Matches("solid QWERT", "^\\L<options>", FuzzyRegexOptions.None, options).Should().BeEmpty();
    }

    [Test]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#9")]
    public void A_named_list_entry_matches_where_it_occurs_literally()
    {
        Dictionary<string, IReadOnlyCollection<string>> options = new(StringComparer.Ordinal)
        {
            ["options"] = ["good", "brilliant", "+solid"],
        };

        FuzzyRegex
            .Matches("+solid QWERT", "^\\L<options>", FuzzyRegexOptions.None, options)
            .Select(m => m.Value)
            .Should()
            .Equal("+solid");
    }

    // (?f) is FULLCASE, so ß folds to ss and the six-code-unit "straße" matches "STRASSE".
    [Test]
    [Skip("needs:named-lists - needs \\L<name> and full case-folding; the parser has neither yet")]
    [Property("Upstream", "RegexTests.test_named_lists#10-11")]
    public void Full_case_folding_matches_a_named_list_entry_of_a_different_length()
    {
        // Upstream asserts the same span for a one-entry list and for a two-entry list, so the
        // extra entry must not change which one is chosen.
        foreach (string[] words in new[] { new[] { "STRASSE" }, ["STRASSE", "stress"] })
        {
            Dictionary<string, IReadOnlyCollection<string>> named = new(StringComparer.Ordinal) { ["words"] = words };

            Match m = FuzzyRegex.MatchAtStart(_strasseLower, "(?fi)\\L<words>", FuzzyRegexOptions.None, named);

            m.Success.Should().BeTrue();
            (m.Index, m.Index + m.Length).Should().Be((0, 6));
        }
    }

    // The other direction: the subject is the seven-code-unit "STRASSE".
    [Test]
    [Skip("needs:named-lists - needs \\L<name> and full case-folding; the parser has neither yet")]
    [Property("Upstream", "RegexTests.test_named_lists#12")]
    public void Full_case_folding_works_from_the_expanded_form_too()
    {
        Dictionary<string, IReadOnlyCollection<string>> named = new(StringComparer.Ordinal)
        {
            ["words"] = [_strasseLower],
        };

        Match m = FuzzyRegex.MatchAtStart("STRASSE", "(?fi)\\L<words>", FuzzyRegexOptions.None, named);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 7));
    }

    // İ (U+0130) case-folds to i, so "kit" is found inside "SKİTS" as well as inside "SKITS".
    [Test]
    [Skip("needs:named-lists - needs \\L<name> and case-insensitive matching; the parser has neither yet")]
    [Property("Upstream", "RegexTests.test_named_lists#13-14")]
    public void A_named_list_folds_case_when_searching()
    {
        Dictionary<string, IReadOnlyCollection<string>> named = new(StringComparer.Ordinal) { ["words"] = ["kit"] };

        foreach (string subject in new[] { "SKITS", _skitsDotted })
        {
            Match m = FuzzyRegex.Match(subject, "(?i)\\L<words>", FuzzyRegexOptions.None, named);

            m.Success.Should().BeTrue();
            (m.Index, m.Index + m.Length).Should().Be((1, 4));
        }
    }

    [Test]
    [Skip("needs:named-lists - the parser does not read \\L<name> yet")]
    [Property("Upstream", "RegexTests.test_named_lists#17")]
    public void An_empty_named_list_matches_the_empty_subject()
    {
        Dictionary<string, IReadOnlyCollection<string>> named = new(StringComparer.Ordinal) { ["options"] = [] };

        Match m = FuzzyRegex.Match("", "^\\L<options>$", FuzzyRegexOptions.None, named);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 0));
    }
}
