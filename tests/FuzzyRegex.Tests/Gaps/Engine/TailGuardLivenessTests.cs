using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D85: the liveness pass that decides what a tail guard is keyed on
/// (<see cref="TailGuardLiveness.FindTailKeys"/>). Each row gives every tail-guarded repeat as
/// <c>r&lt;index&gt;:</c> then <c>-</c> for a position-only guard, or the keyed groups as
/// <c>group:flags</c> (t text read, s set-or-unset read, p a new capture compared with the current
/// span, i the repeat inside the group's brackets), <c>|L</c> and the enclosing repeats whose
/// progress bit the key holds, and <c>|X</c> when every group is keyed on its exact span.
/// </summary>
public sealed class TailGuardLivenessTests
{
    [Test]
    // Read before the repeat only: the position alone is the key (TestedGroupRepeat's shape).
    [Arguments(@"(a)(?:\1|b)*c", "r0:-")]
    [Arguments(@"(x?)\1(?:a*a*)*c$", "r1:- r2:- r3:-")]
    // Read after the enclosing repeat's end, which upstream's walk stops at.
    [Arguments(@"(b?)(?:a*|b)*\1c", "r2:1:t")]
    [Arguments(@"(b?)(?:(?:a*|b)*x)*\1c", "r2:1:t r3:1:t")]
    // Every path from the second a* captures group 1 again before reading it, so its text is
    // dead there; only the capture's comparison with the current span is left.
    [Arguments(@"(?:(x?)a*\1a*)*c$", "r0:- r3:1:p|L0")]
    // A read inside a lookahead, a group condition, the reverse mirror.
    [Arguments(@"(b?)(?:a*|b)*(?=\1c)", "r2:1:t")]
    [Arguments(@"(?:(b)|)(?:a*|b)*(?(1)z|c)", "r1:1:s")]
    [Arguments(@"(?r)c\1(?:a*|b)*(b?)", "r2:1:t")]
    // A group call: every read group, exact spans.
    [Arguments(@"(b?)(?:a*|b)*(?2)c(?(DEFINE)(\1))", "r2:1:tpi|X")]
    // A loop in a lookbehind runs backwards while the lookahead capture in its body runs forwards,
    // so the span compare's "at or after the position" no longer holds: exact spans there, and
    // only there (2026-10-16 review gap 1).
    [Arguments(@"x?(?<=(?:(?=(a*))b*a*)*)\1c", "r2:1:tp|L1|X r3:1:tp|L1|X r4:1:tpi|L1")]
    // A group set inside the loop around the repeat: progress bits for the enclosing loops.
    [Arguments(@"(?:(?:(x?)a)*a*)*\1c", "r1:1:tp|L0 r2:1:pi|L0,1 r3:1:tp|L0")]
    public void The_tail_key_holds_what_the_rest_of_the_match_can_read(string pattern, string expected)
    {
        Describe(new FuzzyRegex(pattern).PatternObject).Should().Be(expected);
    }

    [Test]
    // MatchState.CollapsesInside leaves an inside group's last capture out of the key on the
    // premise that the close of the bracket around the repeat is the first capture of the group on
    // every path from the tail. A group that a second node can capture (a branch reset) breaks the
    // premise, so it never collapses (2026-10-16 review gap 2). Each repeat inside a group: its
    // index, the group and whether it may collapse.
    [Arguments(@"(?:(a*)a*)*\1c$", "r1:1:c")]
    [Arguments(@"(?:(?|(a*)|(b))a*)*\1c$", "r1:1:-")]
    public void Only_a_group_one_node_captures_may_leave_its_last_capture_out(string pattern, string expected)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;
        var parts = new List<string>();
        for (int i = 0; i < compiled.RepeatInfoList.Count; i++)
        {
            if (compiled.RepeatInfoList[i].TailKey is not TailGuardKeySpec key)
            {
                continue;
            }

            for (int j = 0; j < key.Groups.Length; j++)
            {
                if (key.InsideDepths[j] >= 0)
                {
                    parts.Add($"r{i}:{key.Groups[j] + 1}:{(key.Collapsible[j] ? "c" : "-")}");
                }
            }
        }

        string.Join(" ", parts).Should().Be(expected);
    }

    private static string Describe(PatternObject pattern)
    {
        var parts = new List<string>();
        for (int i = 0; i < pattern.RepeatInfoList.Count; i++)
        {
            RepeatInfo info = pattern.RepeatInfoList[i];
            if ((info.Status & NodeStatus.Tail) == 0)
            {
                continue;
            }

            if (info.TailKey is not TailGuardKeySpec key)
            {
                parts.Add($"r{i}:-");
                continue;
            }

            string groups = string.Join(",", key.Groups.Select((g, j) => $"{g + 1}:{Flags(key.Modes[j])}"));
            string around = key.ProgressRepeats.Length > 0 ? "|L" + string.Join(",", key.ProgressRepeats) : "";
            parts.Add($"r{i}:{groups}{around}{(key.ExactSpans ? "|X" : "")}");
        }

        return string.Join(" ", parts);
    }

    private static string Flags(byte mode) =>
        ((mode & TailGuardLiveness.Text) != 0 ? "t" : "")
        + ((mode & TailGuardLiveness.Set) != 0 ? "s" : "")
        + ((mode & TailGuardLiveness.Span) != 0 ? "p" : "")
        + ((mode & TailGuardLiveness.Inside) != 0 ? "i" : "");
}
