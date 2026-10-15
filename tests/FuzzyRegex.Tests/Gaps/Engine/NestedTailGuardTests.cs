using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D85: a repeat records "the rest of the match failed from here" in its tail guard, and the rest
/// of the match reads a group through a backreference or a group condition the guard's set-up did
/// not see, so the next arrival at the position, with a different group value, is refused.
/// </summary>
/// <remarks>
/// Upstream's <c>add_repeat_guards</c> (<c>_regex.c:23273</c>) switches a tail guard off when the
/// tail reads a group, but its walk stops at the enclosing repeat's END_GREEDY_REPEAT
/// (<c>:23334-23337</c>), so for <c>a*</c> in <c>(b?)(?:a*|b)*\1c</c> it never sees the <c>\1</c>;
/// and it does not walk a bounded repeat's body (<c>:23383-23386</c>), so it never sees the
/// <c>\1</c> in <c>(a*)a*(?:d\1)?c</c>.
/// Over 'bac' the attempt at 0 with group 1 = 'b' guards a*'s tail at 2; the path with group 1 = ''
/// then needs a* to take the 'a' and reach that tail at 2, and the guard refuses it. Every expected
/// value below is the leftmost-first answer that Python re 3.14.7, PCRE2 10.47 and 10.45, Java 26,
/// Node 24, Perl 5.42 and .NET 10's System.Text.RegularExpressions all give
/// (<c>.scratch/s1016/d85/survey-*.txt</c>, 2026-10-15); regex 2026.9.10 gives the later start
/// written beside each row.
/// </remarks>
public sealed class NestedTailGuardTests
{
    [Test]
    // The three witnesses from perf/memo-br's differential.
    [Arguments(@"(b*)(?:a*|c|(b))*\1c", "baaaac", 0, 6)] // regex (1, 6)
    [Arguments(@"(a*)(a)(?:ca|c|b?.)*\1a\b", "bcbaacabbba", 3, 11)] // regex (4, 11)
    [Arguments(@"(?i)(ab|a)(?:.b*)+\1\b", "bbbabaccbcba", 3, 12)] // regex (5, 12)
    // The smallest form, and the same cut twice in a row.
    [Arguments(@"(b?)(?:a*|b)*\1c", "bac", 0, 3)] // regex (1, 3)
    [Arguments(@"(b?)(?:a*|b)*\1c", "bbac", 0, 4)] // regex (2, 4)
    // The capture inside the outer repeat plays no part.
    [Arguments(@"(b*)(?:a*|c|b)*\1c", "baaaac", 0, 6)] // regex (1, 6)
    // A lazy outer repeat, a bounded inner one, and the reference further on.
    [Arguments(@"(b?)(?:a*|b)*?\1c", "bac", 0, 3)] // regex (1, 3)
    [Arguments(@"(b?)(?:a{0,9}|b)*\1c", "bac", 0, 3)] // regex (1, 3)
    [Arguments(@"(b?)(?:a*|b)*x?\1c", "bac", 0, 3)] // regex (1, 3)
    // Two levels of nesting: the walk stops at the innermost END_GREEDY_REPEAT.
    [Arguments(@"(b?)(?:(?:a*|b)*x)*\1c", "baxc", 0, 4)] // regex (1, 4)
    // A group condition reads the captures the way a backreference does.
    [Arguments(@"(?:(b)|)(?:a*|b)*(?(1)z|c)", "bac", 0, 3)] // regex (1, 3)
    // The group set inside the guarded loop itself, found by the option-E cost probe (2026-10-15,
    // main against .NET over every subject on {a,b,c,x} up to length 7).
    [Arguments(@"(?:(?:(x?)a)*a*)*\1c", "xaaaxc", 0, 6)] // regex None
    // The design note's witnesses W1-W6 (.scratch/s1016/d85/design/note.md section 6, checked
    // against regex 2026.9.10 and re): the read inside a lookahead, the reverse mirror, the read
    // reached only through a group call, branch reset, a variable-width group set inside the loop,
    // and a general repeat (not a one-character one) as the guarded one. Then the lookahead form
    // of the witness above and the same shape in a fuzzy section, where no repeat guard applies.
    [Arguments(@"(b?)(?:a*|b)*(?=\1c)", "bac", 0, 2)] // regex (1, 2)
    [Arguments(@"(?r)c\1(?:a*|b)*(b?)", "cab", 0, 3)] // regex (0, 2)
    [Arguments(@"(b?)(?:a*|b)*(?2)c(?(DEFINE)(\1))", "bac", 0, 3)] // regex (1, 3)
    [Arguments(@"(?|(b)|())(?:a*|b)*\1c", "bac", 0, 3)] // regex (1, 3)
    [Arguments(@"(?:(?:(x*)a)*a*)*\1c", "xxaaaxxc", 0, 8)] // regex None
    [Arguments(@"(?:(?:(x?)a)*(?:a|a)*)*\1c", "xaaaxc", 0, 6)] // regex None
    [Arguments(@"(?:(?:(x?)a)*a*)*(?=\1c)", "xaaaxc", 0, 4)] // regex None
    [Arguments(@"(?:(b?)(?:a*|b)*\1c){e<=0}", "bac", 0, 3)] // regex (1, 3)
    // The body-guard half: a reference inside a bounded repeat, whose body the walk does not enter
    // (:23383-23386), so the bounded repeat keeps its body guard, and so does a repeat enclosing
    // it. A body guard blocks a position the body never reached its end from, which depends on
    // the group the body reads. The unbounded form is walked and already right (the controls).
    // The reference earlier in the enclosing body, reached on the next iteration.
    [Arguments(@"(a?)(?:(?:\1d)?a*|b)*c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(a?)(?:(?:\1d)?a*|b)*?c", "adc", 0, 3)] // regex (1, 3)
    // No enclosing repeat at all.
    [Arguments(@"(a*)a*(?:d\1)?c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(a*)a*(?:\1d){0,2}c", "adc", 0, 3)] // regex (1, 3)
    // A lazy and a possessive bounded repeat, a case-blind reference, the reference in an
    // alternation, and a group condition in place of the reference.
    [Arguments(@"(a*)a*(?:d\1)??c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(a*)a*(?:d\1){0,1}+c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(?i)(A*)a*(?:d\1)?c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(a*)a*(?:d(?:\1|x))?c", "adc", 0, 3)] // regex (1, 3)
    [Arguments(@"(?:(a)|)a*(?:d(?(1)z|))?c", "adc", 0, 3)] // regex (1, 3)
    public void A_repeat_guard_does_not_cut_a_path_with_different_captures(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    [Test]
    // The smallest rows of the test bed's triage (.scratch/s1016/triage-bed/NOTES.md, checked
    // against regex 2026.9.10 and re with a guards-off control): the stale guard can change only
    // which captures the match holds, and a group condition or a lookaround can be the reader.
    // Group -1 means the row checks no group.
    [Arguments(@"()(c+)*\2", "ccc", 0, 3, 2, 1, 2)] // regex (0, 2)
    [Arguments(@"((d+))*\1", "dddd", 0, 4, 1, 2, 3)] // regex g1 (0, 2)
    [Arguments(@"(?:()|(.?))*(?(1)x)", "b", 0, 1, -1, 0, 0)] // regex (0, 0)
    [Arguments(@"(?:()|.?)+(?!\1)", "zb", 0, 2, -1, 0, 0)] // regex (0, 1)
    public void A_repeat_guard_does_not_change_the_captures_a_match_holds(
        string pattern,
        string subject,
        int start,
        int end,
        int group,
        int groupStart,
        int groupEnd
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        if (group > 0)
        {
            (m.Groups[group].Index, m.Groups[group].Index + m.Groups[group].Length).Should().Be((groupStart, groupEnd));
        }
    }

    [Test]
    // Controls: regex 2026.9.10 already gives these, and so must any fix. a+ writes no tail guard
    // at its minimum, a possessive or atomic a* never backtracks into one, a lazy b?? tries group
    // 1 = '' first, and an unbounded body is walked, so the reference in it is seen. Each matches
    // the whole subject with group 1 empty, the path the stale guard cuts in the rows above.
    [Arguments(@"(b?)(?:a+|b)*\1c", "bac")]
    [Arguments(@"(b?)(?:a*+|b)*\1c", "bac")]
    [Arguments(@"(b?)(?:(?>a*)|b)*\1c", "bac")]
    [Arguments(@"(b??)(?:a*|b)*\1c", "bac")]
    [Arguments(@"(a*)a*(?:d\1)*c", "adc")]
    public void The_same_shapes_without_the_stale_guard_already_match_with_group_1_empty(string pattern, string subject)
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Success, m.Index, m.Length, m.Groups[1].Value).Should().Be((true, 0, subject.Length, ""));
    }
}
