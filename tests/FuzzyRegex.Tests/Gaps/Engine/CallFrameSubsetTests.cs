using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D68: a group call the inliner keeps (a recursion) saves, at every call, only the
/// groups and repeats its body can change, not every group and repeat in the pattern.
/// </summary>
/// <remarks>
/// <para>
/// Measured 2026-11-17 (<c>tools/probes/d68-call-perf/recursion.cs</c>): <c>(\((?:[^()]|(?1))*\))</c>
/// over 30 nested pairs took 17.3 us, and 1,638 us with 400 groups that never take part appended
/// to the pattern, because each of the 30 calls copied all 401 groups and back. Design:
/// <c>docs/plan/2026-11-17-d68-call-frame-subset-design.md</c>, option A.
/// </para>
/// <para>
/// The witnesses pin what a call restores, as this port answered before the change (main
/// ffd0279f): calls that write groups inside the called body, a backreference inside the body to a
/// group outside it, recursion to the whole pattern, a return backtracked into, partial results
/// inside open calls (D62), a lookahead and a lookbehind around a call, reversed and fuzzy calls.
/// <c>regex</c> 2026.9.29 gives the same spans and counts on every row it can run, and lists a
/// call's inner captures where this port drops them on return (D51, ledger entry 63); on the fuzzy
/// recursion row it raises <c>MemoryError</c>.
/// </para>
/// </remarks>
public sealed class CallFrameSubsetTests
{
    /// <summary>A group that never takes part, appended to a pattern to give it groups outside every called body.</summary>
    private const string _idleGroup = "(?:(x)y)?";

    [Test]
    [Arguments(
        "search",
        @"(\((?:(x)|[^()x]|(?1))*\))",
        "((x)a(bx))",
        "(0, 10) p=False 1:(0, 10)['((x)a(bx))'] 2:(-1, -1)[] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(z)(\((?:\1|[^()]|(?2))*\))",
        "z(z(az)z)",
        "(0, 9) p=False 1:(0, 1)['z'] 2:(1, 9)['(z(az)z)'] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(a)?\((?:[^()]|(?R))*\)",
        "a(b(c)d)",
        "(0, 8) p=False 1:(0, 1)['a'] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(\(([^()]*|(?1))*\))\2",
        "((ab)ab)",
        "(0, 8) p=False 1:(0, 8)['((ab)ab)'] 2:(7, 7)['', '(ab)', 'ab', '', ''] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(\((?:[^()]|(?1))*\))(y)?",
        "((a)b)y",
        "(0, 7) p=False 1:(0, 6)['((a)b)'] 2:(6, 7)['y'] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments("partial", @"(\((?:[^()]|(?1))*\))", "((a", "(0, 3) p=True 1:(-1, -1)[] f=(0, 0, 0)([], [], [])")]
    [Arguments("partial", @"(?=(\((?:[^()]|(?1))*\)))", "((a", "(0, 3) p=True 1:(-1, -1)[] f=(0, 0, 0)([], [], [])")]
    [Arguments(
        "partial",
        @"(\((?:(q)|[^()q]|(?1))*\))",
        "((q(a",
        "(0, 5) p=True 1:(-1, -1)[] 2:(-1, -1)[] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(\((?:[^()]|(?1))*\)){e<=1}",
        "((a)b",
        "(0, 4) p=False 1:(0, 4)['((a)'] f=(1, 0, 0)([1], [], [])"
    )]
    [Arguments(
        "search",
        @"(\((?:(x)|[^()x]|(?1))*\)){s<=1}",
        "((x)(y)",
        "(0, 4) p=False 1:(0, 4)['((x)'] 2:(2, 3)['(', 'x'] f=(1, 0, 0)([1], [], [])"
    )]
    [Arguments(
        "fullmatch",
        @"((?:\((?:[^()]|(?1))*\))+)",
        "(a)(b(c))()",
        "(0, 11) p=False 1:(0, 11)['(a)(b(c))()'] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(?r)(\((?:(x)|[^()x]|(?1))*\))",
        "a((x)b)c",
        "(1, 7) p=False 1:(1, 7)['((x)b)'] 2:(-1, -1)[] f=(0, 0, 0)([], [], [])"
    )]
    [Arguments(
        "search",
        @"(\((?:[^()]|(?1))*\))(?<=(\(b\)))",
        "((a)(b))",
        "(4, 7) p=False 1:(4, 7)['(b)'] 2:(4, 7)['(b)'] f=(0, 0, 0)([], [], [])"
    )]
    public void A_recursive_call_restores_what_it_did_before(
        string operation,
        string pattern,
        string text,
        string expected
    )
    {
        Describe(Run(operation, pattern, text), groups: null).Should().Be(expected);
    }

    [Test]
    [Arguments("search", @"(\((?:(x)|[^()x]|(?1))*\))", "((x)a(bx))")]
    [Arguments("search", @"(z)(\((?:\1|[^()]|(?2))*\))", "z(z(az)z)")]
    [Arguments("search", @"(a)?\((?:[^()]|(?R))*\)", "a(b(c)d)")]
    [Arguments("search", @"(\(([^()]*|(?1))*\))\2", "((ab)ab)")]
    [Arguments("partial", @"(\((?:(q)|[^()q]|(?1))*\))", "((q(a")]
    [Arguments("search", @"(\((?:(x)|[^()x]|(?1))*\)){s<=1}", "((x)(y)")]
    [Arguments("fullmatch", @"((?:\((?:[^()]|(?1))*\))+)", "(a)(b(c))()")]
    [Arguments("search", @"(?r)(\((?:(x)|[^()x]|(?1))*\))", "a((x)b)c")]
    [Arguments("search", @"(\((?:[^()]|(?1))*\))(?<=(\(b\)))", "((a)(b))")]
    public void Groups_outside_every_called_body_change_no_answer(string operation, string pattern, string text)
    {
        // The whole-pattern recursion's body is the pattern, so its extra groups are inside it.
        Match plain = Run(operation, pattern, text);
        Match padded = Run(operation, pattern + string.Concat(Enumerable.Repeat(_idleGroup, 50)), text);

        Describe(padded, plain.Groups.Count - 1).Should().Be(Describe(plain, groups: null));
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(\((?:[^()]|(?1))*\))")]
    [Arguments(@"(\((?:(x)|[^()x]|(?1))*\))")]
    [Arguments(@"((?:\((?:[^()]|(?1))*\))+)")]
    public void A_recursive_call_saves_nothing_for_groups_outside_its_body(string pattern)
    {
        EngineWork.SkipUnlessCounting();
        string subject = new string('(', 30) + "ab" + new string(')', 30);

        long plain = ItemsSaved(new FuzzyRegex(pattern), subject);
        long padded = ItemsSaved(new FuzzyRegex(pattern + string.Concat(Enumerable.Repeat(_idleGroup, 400))), subject);

        // Before D68: each of the 30 calls also saved the 400 groups outside the body (12,000 more).
        plain.Should().BeGreaterThan(0, "the subject makes 30 calls");
        padded.Should().Be(plain, "a group outside the called body is never written while the call is open");
    }

    private static long ItemsSaved(FuzzyRegex regex, string subject)
    {
        long before = WorkCounter.CallFrameItemsSaved;
        regex.Match(subject).Success.Should().BeTrue();
        return WorkCounter.CallFrameItemsSaved - before;
    }

    private static Match Run(string operation, string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);
        return operation switch
        {
            "partial" => regex.Match(text, partial: true),
            "fullmatch" => regex.FullMatch(text),
            _ => regex.Match(text),
        };
    }

    /// <summary>The span, partial flag, each group's span and captures, and the fuzzy changes.</summary>
    private static string Describe(Match m, int? groups)
    {
        if (!m.Success)
        {
            return "None";
        }

        int last = groups ?? (m.Groups.Count - 1);
        var parts = new List<string>();
        for (int g = 1; g <= last; g++)
        {
            Group group = m.Groups[g];
            string span = group.Success ? $"({group.Index}, {group.Index + group.Length})" : "(-1, -1)";
            parts.Add($"{g}:{span}[{string.Join(", ", group.Captures.Select(static c => $"'{c.Value}'"))}]");
        }

        FuzzyCounts c = m.FuzzyCounts;
        FuzzyChanges ch = m.FuzzyChanges;
        return $"({m.Index}, {m.Index + m.Length}) p={m.PartialMatch} {string.Join(' ', parts)} "
            + $"f=({c.Substitutions}, {c.Insertions}, {c.Deletions})"
            + $"([{string.Join(", ", ch.Substitutions)}], [{string.Join(", ", ch.Insertions)}], [{string.Join(", ", ch.Deletions)}])";
    }
}
