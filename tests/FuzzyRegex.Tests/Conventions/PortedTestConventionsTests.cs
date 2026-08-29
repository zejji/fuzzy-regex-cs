using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// Guards the conventions the generated status board parses. See
/// <see cref="PortedTestConventions"/> for why these are tests rather than prose.
/// </summary>
public sealed class PortedTestConventionsTests
{
    [Test]
    [Arguments("Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround", "Lookaround")]
    [Arguments("Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors", "Anchors")]
    [Arguments("Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy.BestMatch", "Fuzzy")]
    public void TryGetFeatureArea_takes_the_segment_after_the_ported_root(string ns, string expected)
    {
        PortedTestConventions.TryGetFeatureArea(ns, out string area).Should().BeTrue();
        area.Should().Be(expected);
    }

    [Test]
    [Arguments("Fuzzy.Text.RegularExpressions.Tests.Ported")]
    [Arguments("Fuzzy.Text.RegularExpressions.Tests.Gaps.Surrogates")]
    [Arguments("SomethingElse.Ported.Anchors")]
    public void TryGetFeatureArea_rejects_namespaces_outside_the_ported_root(string ns)
    {
        PortedTestConventions.TryGetFeatureArea(ns, out string area).Should().BeFalse();
        area.Should().BeEmpty();
    }

    [Test]
    [Arguments("needs:lookbehind - variable-length lookbehind is not implemented", "lookbehind")]
    [Arguments("needs:fuzzy-bestmatch", "fuzzy-bestmatch")]
    [Arguments("needs:quantifiers: the VM has no repeat opcode", "quantifiers")]
    [Arguments("needs:posix0 - POSIX leftmost-longest", "posix0")]
    public void ParseWaitingOn_reads_the_capability_the_test_waits_on(string reason, string expected) =>
        PortedTestConventions.ParseWaitingOn(reason).Should().Be(expected);

    [Test]
    [Arguments("not implemented yet")]
    [Arguments("needs: lookbehind")]
    [Arguments("needs:")]
    [Arguments("needs:UPPER")]
    [Arguments("needs:x")]
    [Arguments("S07 lookaround")]
    [Arguments("")]
    public void ParseWaitingOn_rejects_reasons_that_do_not_name_a_capability(string reason) =>
        PortedTestConventions.ParseWaitingOn(reason).Should().BeNull();

    [Test]
    public void Validate_accepts_a_conforming_test() =>
        PortedTestConventions
            .Validate([("Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors", "Caret_matches_start", "needs:anchors")])
            .Should()
            .BeEmpty();

    [Test]
    public void Validate_reports_a_test_outside_the_ported_root() =>
        PortedTestConventions
            .Validate([("Fuzzy.Text.RegularExpressions.Tests.Misplaced", "Some_test", "needs:anchors")])
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("Fuzzy.Text.RegularExpressions.Tests.Ported.<FeatureArea>");

    [Test]
    public void Validate_reports_a_skip_reason_that_names_no_capability() =>
        PortedTestConventions
            .Validate([("Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors", "Some_test", "todo")])
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("must start with needs:");

    /// <summary>
    /// The end-to-end guard: every ported test actually present in this assembly conforms, so
    /// the status board can classify all of them.
    /// </summary>
    [Test]
    public void Every_ported_test_in_this_assembly_conforms()
    {
        // 'Assembly' alone is ambiguous here: TUnit declares HookType.Assembly.
        var portedTests =
            from type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            where
                type.Namespace?.StartsWith(PortedTestConventions.PortedNamespaceRoot, StringComparison.Ordinal) == true
            from method in type.GetMethods(
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly
            )
            where System.Reflection.CustomAttributeExtensions.GetCustomAttribute<TestAttribute>(method) is not null
            select (
                Namespace: type.Namespace!,
                TestName: method.Name,
                SkipReason: System
                    .Reflection.CustomAttributeExtensions.GetCustomAttribute<SkipAttribute>(method)
                    ?.Reason
                    ?? System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SkipAttribute>(type)?.Reason
            );

        PortedTestConventions.Validate(portedTests).Should().BeEmpty();
    }
}
