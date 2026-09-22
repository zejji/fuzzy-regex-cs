using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// The pre-scan that numbers a branch reset by option 3: at each branch start the parser reserves
/// the numbers that names appearing later in that branch already own, so no two groups in one
/// branch share a number. S82; upstream issue 425; <c>docs/DIVERGENCES.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The pre-scan reads the branch's source text before the parser does, so it has to agree with the
/// parser about what a named group definition <b>is</b>. Every test here is a place where a
/// careless reader would disagree: a name inside a character class or a comment, an escaped
/// parenthesis, a lookbehind that also starts <c>(?&lt;</c>, a name that is only referred to. Each
/// pattern is built so that a wrong answer moves a group number and the test sees it.
/// </para>
/// <para>
/// Two kinds of row. A <b>decoy</b> holds no definition, so this port numbers the branch exactly as
/// upstream does and the expected values are upstream's, measured by
/// <c>tools/probes/s82-branch-reset-option3.py</c> against <c>regex</c> 2026.9.10 on 2026-09-22.
/// A <b>definition</b> row does hold one, so the numbering diverges; upstream's answer is quoted
/// beside this port's and the rule is in <c>docs/DIVERGENCES.md</c>.
/// </para>
/// </remarks>
public sealed class BranchResetPreScanTests
{
    /// <summary>
    /// A name inside a character class is not a definition: <c>[(?&lt;n&gt;z)]</c> is seven
    /// ordinary characters.
    /// </summary>
    /// <remarks>Upstream, same pattern and subject: <c>groups=('X', 'Y')</c>.</remarks>
    [Test]
    public void A_name_written_inside_a_character_class_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)[(?<n>z)](Y))").MatchAtStart("X(Y");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>
    /// And the version-1 form of the same trap, where the class holds a nested set. The pre-scan
    /// has to leave both.
    /// </summary>
    /// <remarks>Upstream under <c>(?V1)</c>: <c>groups=('X', 'Y')</c>.</remarks>
    [Test]
    public void A_name_written_inside_a_nested_set_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)[[(?<n>z)]](Y))").MatchAtStart("X(Y");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>A name inside a <c>(?#...)</c> comment is not a definition either.</summary>
    /// <remarks>
    /// The comment closes at the first unescaped <c>)</c>, which is why the one in
    /// <c>(?&lt;n&gt;z\)</c> is escaped. Upstream: <c>groups=('X', 'Y')</c>.
    /// </remarks>
    [Test]
    public void A_name_written_inside_a_comment_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?#(?<n>z\))(Y))").MatchAtStart("XY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>An escaped parenthesis does not open a group, so <c>\(?&lt;n&gt;</c> is not one.</summary>
    /// <remarks>
    /// <c>\(?</c> is an optional literal parenthesis and <c>&lt;n&gt;z</c> is four literal
    /// characters. Upstream: <c>groups=('X', 'Y')</c>.
    /// </remarks>
    [Test]
    public void An_escaped_parenthesis_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)\(?<n>z(Y))").MatchAtStart("X(<n>zY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>A lookbehind starts <c>(?&lt;</c> and is not a name.</summary>
    /// <remarks>Upstream, both rows: <c>groups=('X', 'Y')</c>.</remarks>
    /// <param name="pattern">A branch reset whose second branch holds a lookbehind.</param>
    [Test]
    [Arguments(@"(?|(?<n>a)(b)|(X)(?<=X)(Y))")]
    [Arguments(@"(?|(?<n>a)(b)|(X)(?<!q)(Y))")]
    public void A_lookbehind_does_not_reserve_a_number(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("XY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>
    /// Referring to a name is not defining it: <c>(?P=n)</c> and <c>\g&lt;n&gt;</c> reserve
    /// nothing.
    /// </summary>
    /// <remarks>
    /// Upstream, both rows: <c>groups=('X', 'Y')</c> and <c>captures={'n': ['X']}</c>. The name
    /// assertion is the point of the row: <c>n</c> still resolves to group 1, so the reference had
    /// something to refer to and the branch was numbered as if it were not there.
    /// </remarks>
    /// <param name="pattern">A branch reset whose second branch refers to the first branch's name.</param>
    [Test]
    [Arguments(@"(?|(?<n>a)(b)|(X)(?P=n)?(Y))")]
    [Arguments(@"(?|(?<n>a)(b)|(X)\g<n>?(Y))")]
    public void A_reference_to_a_name_does_not_reserve_a_number(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("XY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
        m.Groups["n"].Value.Should().Be("X");
    }

    /// <summary>
    /// A name the pattern has not used yet claims a fresh number when it is opened, so there is
    /// nothing for the pre-scan to reserve.
    /// </summary>
    /// <remarks>Upstream: <c>groups=('X', 'y')</c>, <c>groupindex={'n': 1, 'fresh': 2}</c>.</remarks>
    [Test]
    public void A_name_the_branch_defines_for_the_first_time_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?<fresh>y))").MatchAtStart("Xy");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("y");
        m.Groups["n"].Value.Should().Be("X");
        m.Groups["fresh"].Value.Should().Be("y");
    }

    /// <summary>
    /// Under <c>IgnorePatternWhitespace</c> a <c>#</c> comment runs to the end of the line, so a
    /// name written in one is not a definition.
    /// </summary>
    /// <remarks>Upstream: <c>groups=('X', 'Y')</c>.</remarks>
    [Test]
    public void A_name_written_in_a_verbose_comment_does_not_reserve_a_number()
    {
        Match m = new FuzzyRegex("(?x)(?|(?<n>a)(b)|(X) # (?<n>z)\n (Y))").MatchAtStart("XY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("X");
        m.Groups[2].Value.Should().Be("Y");
    }

    /// <summary>
    /// <c>(?'n'...)</c> is not a spelling of a named group in this port or upstream, so the
    /// pre-scan must not read <c>'</c> as a name delimiter. Both engines reject the pattern.
    /// </summary>
    /// <remarks>
    /// Upstream: <c>error: unknown extension at position 19</c>. <c>parse_name</c> reads to
    /// <c>&gt;</c> or <c>)</c> and nothing in <c>parse_paren</c> dispatches on a quote.
    /// </remarks>
    [Test]
    public void The_quoted_name_spelling_is_not_a_definition_because_it_is_not_a_group()
    {
        Action compile = static () => _ = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?'n'z)(Y))");

        compile.Should().Throw<FuzzyRegexParseException>().WithMessage("unknown extension");
    }

    /// <summary>
    /// A definition after a character class is still found: skipping the class must stop at its
    /// own <c>]</c> and no later.
    /// </summary>
    /// <remarks>
    /// Upstream numbers <c>(X)</c> 1 and gives <c>(?&lt;n&gt;Z)</c> the same 1, so its
    /// <c>groups=('Z', None)</c> loses 'X'. Rows 2 and 3 are the classes whose end is easiest to
    /// misread: a version-1 nested set, and a set whose first member is a literal <c>]</c>.
    /// </remarks>
    /// <param name="pattern">A branch reset with a class before the second branch's named group.</param>
    /// <param name="subject">A subject the second branch matches.</param>
    [Test]
    [Arguments(@"(?|(?<n>a)(b)|(X)[qz](?<n>Z))", "XqZ")]
    [Arguments(@"(?|(?<n>a)(b)|(X)[[a-z]--[q]](?<n>Z))", "XzZ")]
    [Arguments(@"(?|(?<n>a)(b)|(X)[]q](?<n>Z))", "X]Z")]
    public void A_definition_after_a_character_class_still_reserves_its_number(string pattern, string subject)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(subject);

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("Z");
        m.Groups[2].Value.Should().Be("X");
    }

    /// <summary>A definition after a lookbehind is found too.</summary>
    /// <remarks>Upstream: <c>groups=('Z', None)</c>, 'X' unreachable by number.</remarks>
    [Test]
    public void A_definition_after_a_lookbehind_still_reserves_its_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?<=X)(?<n>Z))").MatchAtStart("XZ");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("Z");
        m.Groups[2].Value.Should().Be("X");
    }

    /// <summary>
    /// A literal backslash in front of a definition does not escape it: <c>\\</c> is the escape,
    /// and the parenthesis after it opens a group.
    /// </summary>
    /// <remarks>Upstream: <c>groups=('z', 'Y')</c> - two groups, because 'X' and 'z' share one.</remarks>
    [Test]
    public void A_definition_behind_an_escaped_backslash_still_reserves_its_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)\\(?<n>z)(Y))").MatchAtStart(@"X\zY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("z");
        m.Groups[2].Value.Should().Be("X");
        m.Groups[3].Value.Should().Be("Y");
    }

    /// <summary>
    /// A definition inside a nested branch reset counts: it uses the number in this branch, so the
    /// outer branch's earlier groups must leave it alone.
    /// </summary>
    /// <remarks>
    /// Upstream: <c>groups=('y', None, 'Z')</c>, three groups, with 'X' unreachable. Here the
    /// branch needs four: <c>(X)</c> is pushed off 1, the inner <c>(q)</c> keeps its own number
    /// and <c>(Z)</c> follows it.
    /// </remarks>
    [Test]
    public void A_definition_inside_a_nested_branch_reset_still_reserves_its_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?|(?<n>y)|(q))(Z))").MatchAtStart("XyZ");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("y");
        m.Groups[2].Value.Should().Be("X");
        m.Groups[3].Success.Should().BeFalse();
        m.Groups[4].Value.Should().Be("Z");
    }

    /// <summary>
    /// A conditional uses a name without defining one, and the <c>|</c> inside it does not end the
    /// branch - so the definition after it is still found.
    /// </summary>
    /// <remarks>
    /// This row shows the renumbering in the match itself. <c>(?(n)p|q)</c> asks whether group
    /// <c>n</c> has matched. Upstream gives <c>(X)</c> the number <c>n</c> owns, so the answer is
    /// yes and only 'XpZ' matches. Here <c>(X)</c> is group 2 and <c>n</c> is still unset when the
    /// conditional is reached, so 'XqZ' matches and 'XpZ' does not.
    /// </remarks>
    [Test]
    public void A_definition_after_a_conditional_still_reserves_its_number()
    {
        var pattern = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?(n)p|q)(?<n>Z))");

        Match m = pattern.MatchAtStart("XqZ");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("Z");
        m.Groups[2].Value.Should().Be("X");
        pattern.MatchAtStart("XpZ").Success.Should().BeFalse();
    }

    /// <summary>A definition whose own number is referred to later in the branch.</summary>
    /// <remarks>Upstream: <c>groups=('q', 'Y')</c>, two groups.</remarks>
    [Test]
    public void A_definition_referred_to_later_in_the_branch_still_reserves_its_number()
    {
        Match m = new FuzzyRegex(@"(?|(?<n>a)(b)|(X)(?<n>q)\g<n>(Y))").MatchAtStart("XqqY");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("q");
        m.Groups[2].Value.Should().Be("X");
        m.Groups[3].Value.Should().Be("Y");
    }

    /// <summary>
    /// Under <c>IgnorePatternWhitespace</c> a name may be written with spaces around it, and the
    /// pre-scan has to read it the way <c>parse_name</c> does - which drops them.
    /// </summary>
    /// <remarks>Upstream: <c>groups=('y', 'Z')</c>, two groups, with 'X' unreachable.</remarks>
    [Test]
    public void A_verbose_definition_whose_name_is_written_with_spaces_still_reserves_its_number()
    {
        Match m = new FuzzyRegex("(?x)(?|(?<n>a)(b)|(X) (?< n >y) (Z))").MatchAtStart("XyZ");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("y");
        m.Groups[2].Value.Should().Be("X");
        m.Groups[3].Value.Should().Be("Z");
    }
}
