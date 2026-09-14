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

    [Test]
    [Arguments("var r = new FuzzyRegex(\"a\");")]
    [Arguments("FuzzyRegex.Match(subject, \"a\").Success.Should().BeTrue();")]
    [Arguments("private static readonly FuzzyRegex _r = new(\"a\");")]
    [Arguments("            FuzzyRegex")]
    // The blind review's hole: a target-typed `new(...)` on its own line inside a collection
    // expression names no type at all, so only the DECLARATION can catch it.
    [Arguments("    private static readonly FuzzyRegex[] _patterns = [new(\"a\")];")]
    [Arguments("    private static FuzzyRegex Build(string p) => new(p);")]
    // The second pass's two: an Upstream call on the line does not excuse a second construction
    // beside it, and neither does one in a trailing comment.
    [Arguments("    private static readonly FuzzyRegex[] _p = [Upstream.Compile(\"a\"), new(\"b\")];")]
    [Arguments("        FuzzyRegex r = new(\"[a[b\"); // Upstream.Compile pins VERSION0")]
    public void FindDirectEngineUses_reports_a_ported_test_that_compiles_outside_Upstream(string line) =>
        PortedTestConventions
            .FindDirectEngineUses("Some.cs", [line])
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("Ported.Upstream");

    [Test]
    [Arguments("Upstream.Match(subject, \"a\").Success.Should().BeTrue();")]
    [Arguments("var r = Upstream.Compile(\"a\", FuzzyRegexOptions.IgnoreCase);")]
    [Arguments("private static readonly FuzzyRegex _r = Upstream.Compile(\"a\");")]
    [Arguments("/// <see cref=\"FuzzyRegex.Matches(string, string)\"/> takes the options.")]
    [Arguments("        // new FuzzyRegex(\"a\") would bypass the pin.")]
    // The two names that merely START with FuzzyRegex are not the type, and are everywhere.
    [Arguments("    public void X(string p) => Upstream.Compile(p, FuzzyRegexOptions.IgnoreCase);")]
    [Arguments("        compile.Should().Throw<FuzzyRegexParseException>();")]
    [Arguments("        Dictionary<string, IReadOnlyCollection<string>> named = new(StringComparer.Ordinal);")]
    // The second pass's two false positives: the type named inside a string, and inside a block
    // comment's tail. Neither compiles anything.
    [Arguments("        act.Should().Throw<Exception>().WithMessage(\"FuzzyRegex refused it\");")]
    [Arguments("        int n = 1; /* ported from tests/FuzzyRegex.Tests/Ported */")]
    public void FindDirectEngineUses_accepts_the_helper_and_prose(string line) =>
        PortedTestConventions.FindDirectEngineUses("Some.cs", [line]).Should().BeEmpty();

    /// <summary>
    /// The end-to-end guard for S50b's version pin: no ported test source compiles a pattern
    /// outside <c>Ported.Upstream</c>, so none of them silently runs under this port's
    /// <c>Version1</c> default instead of upstream's <c>VERSION0</c>.
    /// </summary>
    [Test]
    public void No_ported_test_source_compiles_outside_Upstream()
    {
        DirectoryInfo ported = new(Path.Combine(RepositoryRoot().FullName, "tests", "FuzzyRegex.Tests", "Ported"));
        ported.Exists.Should().BeTrue("the ported tree must be found for this guard to mean anything");

        List<string> violations = [];
        foreach (FileInfo source in ported.EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            if (source.Name.Equals("Upstream.cs", StringComparison.Ordinal))
            {
                continue;
            }

            violations.AddRange(
                PortedTestConventions.FindDirectEngineUses(source.Name, File.ReadLines(source.FullName))
            );
        }

        violations.Should().BeEmpty();
    }

    /// <summary>
    /// The repository root, found from the test assembly's own location rather than from the
    /// working directory, which a test runner does not promise.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FuzzyRegex.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("no FuzzyRegex.slnx above " + AppContext.BaseDirectory);
    }
}
