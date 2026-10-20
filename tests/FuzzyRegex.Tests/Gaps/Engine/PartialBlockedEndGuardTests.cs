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

    // D113: the dedup at a blocked lookahead end (Matcher.EndALookaroundInTheSecondPass) backtracks
    // in place of a continuation it has already run, so the failure it stands for is the
    // continuation's, and the repeats in the body must not record it. Without NoteABlockedEnd there,
    // `a?`'s tail guard at 2 written under the lookahead at 1 (where `b` then fails) refused the
    // lookahead at 2, and start 0 was lost. The search over 2.1 million rows
    // (`.scratch/s1019/d113/grid`) found main late on 951 rows of 12 patterns, all of this shape: a
    // positive lookahead nested first in a positive lookahead whose body ends in a repeat and an
    // optional item. Each expected answer is the earliest start that some continuation completes
    // (upstream without partial over every continuation of one to four characters); upstream with
    // partial answers the same, except as noted. Main answers as in the comment.
    [Test]
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aab", "search", "(0,3)p")] // (1,3)p
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aab", "match", "(0,3)p")] // None
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aab", "finditer", "(0,3)p")] // (1,3)p
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aaaaab", "search", "(3,6)p")] // (4,6)p; upstream (0,6)p
    [Arguments(@"a*?(?=(?=[ab]*c)(?:ab|a)*a?)b", "ab", "search", "(0,2)p")] // (1,2)p
    [Arguments(@"(?:a|aa)(?=(?=a*b*c)(?:a|aa)*a?)(?:b|ab)", "aaab", "search", "(0,4)p")] // (1,4)p
    [Arguments(@"(?:(?:a|aa)(?=(?=[ab]*c)(?:a|aa)+a?)a(?!a)|b)", "aaab", "match", "(0,4)p")] // None
    [Arguments(@"(?:a?a?(?=(?=[ab]*c)(?:a|aa){0,3}(?<=a)))+(?<!a)", "aaab", "match", "(0,4)p")] // None
    public void A_repeat_inside_a_lookahead_keeps_no_record_of_a_continuation_the_dedup_skipped(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation, partial: true).Should().Be(expected);
    }

    // D113, the second shape (found by the cost research, `.scratch/s1019/d113-cost/review`): a
    // lookaround touching the text end in the first alternative of a lookahead's body leaves that
    // path's end blocked, and main's dedup let the repeat record the continuation's failure there.
    // The second alternative reaches the same repeat at the same position with its end decided, and
    // the record cut it, so the inner lookahead stayed undecided and the negative one around it
    // reported a partial over the whole text. Those spans are impossible: two of the patterns are
    // zero-width and the other two read one character. The right answers, for the first two rows:
    // over 'a', the second alternative `a` succeeds for every continuation, so `(?!...)` always
    // fails; over 'aa', `aa` succeeds at 0 whatever follows, and at 1 the continuation 'c' fails
    // both alternatives, so `(?!...)a` completes as (1,2). The last two rows move the touch to an
    // end anchor and to after a character. Upstream regex 2026.9.10 and 2026.9.29 answer as main
    // does (comments; ledger entry 94).
    [Test]
    [Arguments(@"(?!(?=(?:(?=[ab]*c)|a)(?:ab|a)*a?))", "a", "match", "None")] // (0,1)p
    [Arguments(@"(?!(?=(?:(?![ab]*c)a|aa)(?:ab|a)*a?))a", "aa", "search", "(1,2)p")] // (0,2)p
    [Arguments(@"(?!(?=(?:(?=a*$)a|aa)(?:ab|a)*a?))a", "aaa", "search", "(2,3)p")] // (0,3)p
    [Arguments(@"(?!(?=(?:a(?=[ab]*c)|aa)(?:ab|a)*a?))", "aab", "match", "None")] // (0,3)p
    public void A_negative_lookahead_reports_no_partial_its_inner_lookahead_decides_for_every_continuation(
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
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aabc", "search", true, "(0,3)")]
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(?:a|aa)*a?)b", "aaaaab", "match", true, "None")]
    [Arguments(@"(?:a|aa)(?=(?:a|aa)*a?)b", "aab", "search", true, "(0,3)")]
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)b)b", "aab", "search", true, "(0,3)p")]
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
