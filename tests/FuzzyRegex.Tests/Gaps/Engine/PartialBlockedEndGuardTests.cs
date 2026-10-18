using System.Globalization;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D90: in a partial call's second pass a lookahead whose body ran out at the edge has its end
/// blocked, so matching carries on after it with the body's entries kept. When that continuation
/// fails, the repeats inside the body record their position as failed, but what failed was the
/// continuation, which depends on where the lookahead started and what came before. A later
/// lookahead at another start read the record and cut the path that matches.
/// </summary>
/// <remarks>
/// The expected answers are the same build's with every repeat guard off: guards only prune, so
/// that answer is the reference (rows from the 5.6 million-row search in
/// <c>.scratch/s1016/d90/grid</c>, 2026-10-18). The fault needs neither a capture nor a read of
/// one: the third group of rows has no group, or a group nothing reads. Each row's answer on main
/// 6f557571 is in the comment beside it. The answer is written as
/// <c>(start,end)</c>, a <c>p</c> for a partial match, then each group's captures.
/// </remarks>
public sealed class PartialBlockedEndGuardTests
{
    [Test]
    // The register's two witnesses, in every operation.
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "ab", "search", "(0,2)p g1=")] // main (2,2)p
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "ab", "match", "(0,2)p g1=")] // main None
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "ab", "fullmatch", "(0,2)p g1=")] // main None
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "ab", "finditer", "(0,2)p g1=")] // main (2,2)p
    [Arguments(@"a*(?:(?=(?:a|(b))*)a)*\1cb?", "ab", "search", "(0,2)p g1=")] // main g1=(1,2)
    // A read of the group, the lookahead in a counted repeat, or in no repeat at all.
    [Arguments(@"a*(?=(?:(a)|b)*)a\1", "ab", "search", "(0,2)p g1=")] // main (2,2)p
    [Arguments(@"a*(?=(?:a|(b))*)a\1", "aba", "fullmatch", "(0,3)p g1=")] // main None
    [Arguments(@"a*(?:(?=(?:(a)|b)*a?)a){2,}\1", "aab", "search", "(0,3)p g1=")] // main (3,3)p
    [Arguments(@"[ab]*?(?:(?=(?:a|(b))*)b)*?(?(1)c|d)", "ab", "search", "(0,2)p g1=")] // main g1=(1,2)
    // No group, or a group nothing reads; a tail guard as well as a body guard.
    [Arguments(@"a*(?=(?:ab|a)*b?y)aa", "aaba", "search", "(0,4)p")] // main (3,4)p
    [Arguments(@"a*(?=(?:ab|a)*b?y)aa", "aaba", "match", "(0,4)p")] // main None
    [Arguments(@"a*(?=(?:ab|a)*b?y)aa", "aaba", "finditer", "(0,4)p")] // main (3,4)p
    [Arguments(@"a*(?:(?=(?:ab|a)*b?y)a){2,}", "aaba", "search", "(0,4)p")] // main (3,4)p
    [Arguments(@"a*(?=(?:(a)|b)*y)a", "ab", "search", "(0,2)p g1=(0,1)")] // main (2,2)p
    [Arguments(@"a*(?=(a*)b?y)a", "ab", "match", "(0,2)p g1=(0,1)")] // main None
    [Arguments(@"[aby]*?(?:(?=(?:(a)|b)*y)a)+", "ba", "fullmatch", "(0,2)p g1=(1,2)")] // main g1=
    public void A_repeat_inside_a_blocked_lookahead_keeps_no_record_of_the_continuation_failing(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation, partial: true).Should().Be(expected);
    }

    // Controls, green before the fix: the same patterns without partial, and partial calls where
    // the body never runs out or the record is sound.
    [Test]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "ab", "search", false, "None")]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "aab", "search", false, "None")]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+\1ab?", "aaab", "search", true, "(0,4) g1=(0,1),(1,2),(2,3)")]
    [Arguments(@"a*(?=(?:ab|a)*b?y)aa", "aaba", "search", false, "None")]
    [Arguments(@"a*(?=(?:ab|a)*b?y)aa", "aabay", "search", false, "(0,2)")]
    [Arguments(@"(?=(a))a\1", "aa", "search", true, "(0,2) g1=(0,1)")]
    [Arguments(@"(?=(a))a\1", "a", "search", true, "(0,1)p g1=(0,1)")]
    [Arguments(@"(?:(?=(?:a|aa)*y)a)*z", "aaaa", "match", true, "(0,4)p")]
    [Arguments(@"(?>(?:a|aa)*y?)(?<!a)", "aaaa", "match", true, "(0,4)p")]
    public void Calls_the_fault_cannot_reach_answer_as_before(
        string pattern,
        string subject,
        string operation,
        bool partial,
        string expected
    )
    {
        Run(pattern, subject, operation, partial).Should().Be(expected);
    }

    private static string Run(string pattern, string subject, string operation, bool partial)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(subject, partial: partial),
            "fullmatch" => regex.FullMatch(subject, partial: partial),
            "finditer" => regex.Matches(subject, partial: partial)[0],
            _ => regex.Match(subject, partial: partial),
        };

        if (!m.Success)
        {
            return "None";
        }

        string groups = string.Concat(
            Enumerable
                .Range(1, m.Groups.Count - 1)
                .Select(i =>
                    " g"
                    + i.ToString(CultureInfo.InvariantCulture)
                    + "="
                    + string.Join(
                        ",",
                        m.Groups[i]
                            .Captures.Select(static c =>
                                string.Create(CultureInfo.InvariantCulture, $"({c.Index},{c.Index + c.Length})")
                            )
                    )
                )
        );
        return string.Create(
            CultureInfo.InvariantCulture,
            $"({m.Index},{m.Index + m.Length}){(m.PartialMatch ? "p" : "")}{groups}"
        );
    }
}
