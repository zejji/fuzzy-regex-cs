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

    // Round 2 (blind review): a lookahead inside an atomic group or a possessive repeat lets a run-out
    // in its body wait for the body's other paths (Matcher.RunOutCanWaitForTheBody), and backtracking
    // through them wrote a tail guard for a tail that had run out, not failed: `b?`'s at 2 here, so
    // the attempt from 0 lost the run-out. 'aby' matches (0, 1), so P(0, 2). Main and round 1 answer
    // the row as in the comment.
    [Test]
    [Arguments(@"a?(?>(?=a?b?y)a)", "ab", "search", "(0,2)p")] // (2,2)p
    [Arguments(@"a?(?>(?=a?b?y)a)", "ab", "match", "(0,2)p")] // None
    [Arguments(@"a?(?>(?=a?b?y)a)", "ab", "finditer", "(0,2)p")] // (2,2)p
    [Arguments(@"a?(?>(?=a?b?y)a)", "ab", "overlapped", "(0,2)p")] // (2,2)p
    [Arguments(@"a*(?>(?=a*b?y)a)a", "aab", "search", "(0,3)p")] // (3,3)p
    [Arguments(@"a?(?>(?=a?b?y))a", "ab", "search", "(0,2)p")] // (2,2)p
    [Arguments(@"a?(?>(?>(?=a?b?y))a)", "ab", "match", "(0,2)p")] // None
    [Arguments(@"a?(?>(?=a?b?y)a)++", "ab", "search", "(0,2)p")] // (2,2)p
    [Arguments(@"a?(?:(?=a?b?y)a)++", "ab", "match", "(0,2)p")] // None
    [Arguments(@"(?:a|(?>(?=a*+b?y)a)ab)*a", "aab", "fullmatch", "(0,3)p")] // None
    public void A_run_out_that_waits_for_the_body_leaves_no_failure_record(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation, partial: true).Should().Be(expected);
    }

    // Negative lookarounds whose undecided outcome another construct reads: an atomic group or
    // possessive repeat around it, or another negative. A record that turned the body's run-out into
    // a failure made a later instance pass decided, so the enclosing construct committed. These bound
    // the exemption below (MatchState.BlockedEndMark): a negative lookaround inside nothing that cuts
    // keeps its records. The search of 1.48 million rows (`.scratch/s1016/d90/grid/gen3.py`) found
    // main wrong only in these three wrappers. Main answers as in the comment.
    [Test]
    [Arguments(@"(?:(?!a(?!(?:(a)|b)*y))a)+", "aaaa", "fullmatch", "(0,4)p g1=")] // None
    [Arguments(@"a?(?>(?!(?:(a)|b)*y)a|ab)a", "ab", "search", "(0,2)p g1=")] // (2,2)p
    [Arguments(@"a*(?:(?!(?:(a)|b)*y)a)++a", "aab", "search", "(0,3)p g1=")] // (3,3)p
    [Arguments(@"(?:(?!a(?!(?:ab|a)*b?y))a)+b", "aaab", "search", "(0,4)p")] // (1,4)p
    public void A_negative_lookaround_whose_outcome_a_cut_reads_keeps_its_run_out(
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
    [Arguments(@"a?(?>(?!a?b?y)|a)", "ab", "search", true, "(0,1)")]
    [Arguments(@"(?:(?!(?:a|b)*(b)?y)a)*[ab]*\1c", "aaaa", "search", true, "(0,4)p g1=")]
    [Arguments(@"[aby]*?(?!(?:a|b)*(b)?y)aa", "abab", "search", true, "(0,4)p g1=")]
    [Arguments(@"a?(?=(?>a?b?y))a", "ab", "search", true, "(0,2)p")]
    [Arguments(@"a?(?>(?(?=a?b?y)a|c))", "ab", "search", true, "(0,2)p")]
    [Arguments(@"a?(?>a(?<=^a?b?y))", "ab", "search", true, "(2,2)p")]
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

    // D104: the records the rule above withholds are kept under the instance they were written in
    // (MatchState.CutBodyKeyOf: where the lookahead started, the enclosing loop's count, start and
    // progress, the groups the rest reads), so a second arrival in the same state is still cut.
    // Without them the second pass explored what the guards-off build explores until the fork
    // budget stopped it and answered its partial at the attempt start, (0, 10), on every row here
    // (red on perf/d103 608481da). The expected answers are the guards-off build's with no budget
    // (.scratch/s1016/d90/steps, 30 of the 40 rows where main before D90 agreed with it, and the
    // register's witness at n = 10; the other 10 are still open, in OpenDefectTests). Upstream
    // 2026.9.10 answers (0, 10) on all 40 grid rows: its lookahead never carries on past a blocked
    // end, so it has no such second pass.
    [Test]
    [Arguments(@"(?:(?=(?:a|(b))*a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"(?:(?=(?:a|(b))*a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"(?:(?=(?:a|(b))*a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*y)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*y)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:(a)|b)*y)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:ab|a)*(b?)y)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:ab|a)*(b?)y)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:ab|a)*(b?)y)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*y)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*y)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:a|(b))*y)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"b?(?:(?=(?:a|(b))*a?)a)*(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"b?(?:(?=(?:a|(b))*a?)a)+(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"b?(?:(?=(?:a|(b))*a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"y?(?:(?=(?:a|(b))*a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"y?(?:(?=(?:a|(b))*a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"y?(?:(?=(?:a|(b))*a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments("(?:(?=(?:a|(b))*y)a)+(?(1)c|d)", "aaaaaaaaaab", 11, 11)]
    public void A_record_kept_under_its_instance_keeps_the_exact_partial(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length)
            .Should()
            .Be((start, end), "the fork budget's partial at the attempt start is the D104 phantom");
    }

    // D104, blind review of the design (2026-10-18): `b?` captures an empty span, which the
    // capture-change counter and D17's empty-iteration states read, so a key holding only whether
    // the group is set would merge arrivals whose futures differ. The key holds what D85's does,
    // the exact span where a new capture could equal it. The expected answers are the guards-off
    // build's (.scratch/s1018/d104g/w-sw.txt); D90's rule and main before D90 give the same. Upstream
    // 2026.9.10 answers (0, 10) and (0, 2) partial, or raises MemoryError, for want of the
    // blocked-end second pass.
    [Test]
    [Arguments(@"(?:(?=(?:a|(b?))*y)a?)+(?(1)c|d)", "aaaaaaaaab", "search", "(10,10)p g1=")]
    [Arguments(@"(?:(?=(?:a|(b?))*y)a?)+(?(1)c|d)", "aaaaaaaaab", "match", "None")]
    [Arguments(@"(?:(?=(?:a|(b?))*y)a?)+(?(1)c|d)", "ab", "search", "(2,2)p g1=")]
    [Arguments(@"(?:(?=(?:a|(b?))*y)a?)+(?(1)c|d)", "aaba", "search", "(3,4)p g1=")]
    [Arguments(@"a*(?:(?=(?:a|(b?))*a?)a?)*(?(1)c|d)", "aaaaaaaaab", "search", "(10,10)p g1=")]
    [Arguments(@"a*(?:(?=(?:a|(b?))*a?)a?)*(?(1)c|d)", "aaba", "fullmatch", "None")]
    [Arguments(@"(?:(?=(?:(a?)|b)*y)a?){2,}(?(1)c|d)", "ab", "search", "(2,2)p g1=")]
    [Arguments(@"(?:(?=(?:(a?)|b)*y)a?){2,}(?(1)c|d)", "aaba", "search", "(3,4)p g1=")]
    public void A_record_kept_under_its_instance_tells_empty_captures_apart(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation, partial: true).Should().Be(expected);
    }

    private static string Run(string pattern, string subject, string operation, bool partial)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(subject, partial: partial),
            "fullmatch" => regex.FullMatch(subject, partial: partial),
            "finditer" => regex.Matches(subject, partial: partial)[0],
            "overlapped" => regex.Matches(subject, overlapped: true, partial: partial)[0],
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
