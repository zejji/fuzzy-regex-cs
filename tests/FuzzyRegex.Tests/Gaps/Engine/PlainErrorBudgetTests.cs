using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D143: which fuzzy sections compile with <see cref="Node.HasPlainErrorBudget"/>, the flag that lets
/// <c>Matcher.ThisErrorPermitted</c> and <c>Matcher.AnyErrorPermitted</c> compare the error count
/// alone instead of recomputing the cost and checking every limit.
/// </summary>
/// <remarks>
/// A budget is plain when every error costs 1, so the cost equals the error count, and no per-type
/// maximum is below the smaller of the error and cost maxima, so a per-type count can never reach its
/// own maximum first. Upstream's parser fills the limits a pattern does not name
/// (<c>upstream/regex/_regex_core.py:2794-2819</c>): <c>{e&lt;=2}</c> leaves every per-type maximum
/// unlimited and sets the cost maximum to 2. That the fast check answers exactly what the full check
/// does is asserted on every call in a Debug build, so the whole suite and the oracle prove it; these
/// tests pin which sections take it.
/// </remarks>
public sealed class PlainErrorBudgetTests
{
    [Test]
    [Arguments("(?:amber lantern works){e<=2}")]
    [Arguments("(?i)(?:amber lantern works){e<=2}")]
    [Arguments("(?:abc){e}")]
    [Arguments("(?:abc){e<3}")]
    [Arguments("(?:abc){i<=3,d<=3,s<=3,e<=2}")]
    [Arguments("(?:abc){1i+1d+1s<=2}")]
    [Arguments("(?:abc){i<=2,d<=2,s<=2,1i+1d+1s<=2}")]
    public void A_budget_of_unit_costs_with_no_tighter_per_type_maximum_is_plain(string pattern)
    {
        Sections(pattern).Should().AllSatisfy(static node => node.HasPlainErrorBudget.Should().BeTrue());
    }

    [Test]
    // A per-type maximum below the total: an insertion can run out while errors are left.
    [Arguments("(?:abc){i<=1,s<=2,e<=2}")]
    // Naming one type sets the other two to 0.
    [Arguments("(?:abc){s<=1}")]
    [Arguments("(?:abc){d<=0}")]
    // Weighted costs: the cost is not the error count.
    [Arguments("(?:abc){2i+2d+1s<=4}")]
    [Arguments("(?:abc){1i+1d+2s<=4}")]
    // A cost maximum above the error maximum, with a per-type maximum between them.
    [Arguments("(?:abc){i<=1,d<=3,s<=3,e<=2,1i+1d+1s<=3}")]
    public void Any_other_budget_is_not_plain(string pattern)
    {
        Sections(pattern).Should().ContainSingle().Which.HasPlainErrorBudget.Should().BeFalse();
    }

    [Test]
    public void Nested_sections_each_carry_their_own_flag()
    {
        IReadOnlyList<Node> sections = Sections("(?:a(?:bcd){i<=1,e<=2}e){e<=2}");

        sections.Should().HaveCount(2);
        sections.Select(static node => node.HasPlainErrorBudget).Should().Equal(true, false);
    }

    private static List<Node> Sections(string pattern) =>
        [.. new FuzzyRegex(pattern).PatternObject.NodeList.Where(static node => node.Op == Opcode.Fuzzy)];
}
