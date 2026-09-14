using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Splitting;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_issue_18468</c>
/// (lines 2955-3030), the <c>regex.split</c> assertions.
/// </summary>
/// <remarks>
/// <para>
/// Assertions #1-6 (<c>regex.sub</c>, at the top of the upstream method) are ported in
/// <c>Ported/Substitution/Issue18468Tests.cs</c>.
/// </para>
/// <para>
/// Upstream loops <c>for string in ":a:b::c", StrSubclass(":a:b::c"):</c> and repeats each
/// assertion once per loop iteration; both iterations run the same source line, so they share one
/// assertion index here rather than getting two - the <c>StrSubclass</c> iteration is not
/// separately ported (this port has no string-subclass equivalent).
/// </para>
/// <para>
/// Assertions #10-11 are the <c>else</c> branch of a <c>sys.version_info &gt;= (3, 7, 0)</c>
/// guard, for a Python version this port does not target; per the port-tests convention, both
/// branches are counted but only the <c>&gt;= (3, 7, 0)</c> branch (#8-9) is ported. Assertions
/// #12-16 repeat the same three splits over <c>bytes</c>/<c>bytearray</c>/<c>memoryview</c>/
/// <c>BytesSubclass</c> subjects, which this char-based engine has no equivalent for, and are not
/// ported.
/// </para>
/// </remarks>
public sealed class Issue18468Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#7")]
    public void Split_on_a_literal_colon_keeps_empty_pieces() =>
        Upstream.Split(":a:b::c", ":").Should().Equal("", "a", "b", "", "c");

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#8")]
    public void Split_on_a_star_quantifier_also_splits_on_the_empty_matches_between_characters() =>
        Upstream.Split(":a:b::c", ":*").Should().Equal("", "", "a", "", "b", "", "c", "");

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#9")]
    public void Split_with_a_capturing_star_group_keeps_the_empty_captures_too() =>
        Upstream
            .Split(":a:b::c", "(:*)")
            .Should()
            .Equal("", ":", "", "", "a", ":", "", "", "b", "::", "", "", "c", "", "");
}
