using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101, option B1: a run-out inside a group call that a negative lookaround or a negative
/// condition test opened. The text ran out inside the called group, so the body's success is
/// undetermined, and the construct is judged as it is when its body opened no call: the body's
/// other paths are tried, and the construct passes undetermined if none of them succeeds on the
/// held text. Main answered the partial at the attempt's start instead.
/// </summary>
/// <remarks>
/// The fix rests on one fact about the engine (design note E3,
/// <c>.scratch/s1021/d101/DESIGN.md</c>): a negative construct's blocked end never goes on past the
/// construct with the body's state. It goes back into the body
/// (<c>Matcher.EndALookaroundInTheSecondPass</c>), so the call's own backtracking arms close the
/// call and put the caller's captures back, and the construct's own arm restores the rest before
/// matching goes on after it.
/// </remarks>
public sealed class PartialNegativeCallRunOutTests
{
    /// <summary>
    /// E3's witness. The body's group captures 'x' before the text runs out at the body's 'y'
    /// (group 2, inside the called group; group 1 in the call-free twin and in the negative test).
    /// Whatever follows 'zx', the group holds nothing after the construct: if the next character is
    /// 'y' the body succeeds and the construct fails, and otherwise the body fails and a negative
    /// construct keeps no capture. 'zx' + 'b' completes the match, so the judge answers P(0, 2), and
    /// the group is unset in every completion (regex 2026.9.29 over 'zxb'). A continuation that ran
    /// with the body's state would report the group as (1, 2).
    /// </summary>
    /// <remarks>
    /// Mutation check (2026-10-21): with the negative branch of
    /// <c>EndALookaroundInTheSecondPass</c> planted out, so that a blocked negative end goes on past
    /// the construct as a positive one does, every row of this class goes red: the call-free rows
    /// report the group, and the call rows trip amendment 2's assert 4 (Debug).
    /// </remarks>
    [Test]
    [Arguments(@"z(?!(?&d))xb(?(DEFINE)(?<d>(x)y))", 2)]
    [Arguments(@"z(?!(x)y)xb", 1)]
    [Arguments(@"z(?(?!(x)y)xb|c)", 1)]
    public void A_negative_constructs_blocked_end_goes_on_without_the_bodys_captures(string pattern, int group)
    {
        var regex = new FuzzyRegex(pattern);

        Match m = regex.MatchAtStart("zx", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
        m.Groups[group].Success.Should().BeFalse();
        Match full = regex.MatchAtStart("zxb", partial: true);
        (full.Success, full.PartialMatch, full.Index, full.Length).Should().Be((true, false, 0, 3));
        full.Groups[group].Success.Should().BeFalse();
    }

    /// <summary>
    /// The negative constructs of D101 (was <c>OpenDefectTests</c>, and the first row was pinned as
    /// a partial in <c>PartialContractTests</c> until D101's B1). In each anchored row the group,
    /// or the leading 'x', is one character and is followed by <c>\Z</c> or by 'a' or 'b' and
    /// <c>\Z</c>, so the text must end at 1 or 2 with an 'a' or 'b' at 1; the row's text already
    /// rules that out, and no continuation completes a match from 0: the judge answers None. Each
    /// search's leftmost completable start is the text's end ('xxy' + 'x', or + 'xa'), P(3, 3). The
    /// same patterns with the call written out, <c>(?:x)</c> for the group (to depth three for the
    /// recursion), answer the same.
    /// </summary>
    /// <remarks>
    /// DIVERGES FROM UPSTREAM: regex 2026.9.29 answers every row with the partial at the attempt's
    /// start, P(0, 2) and P(1, 3), as main did, and PCRE2 10.47 gives the same partials for the
    /// first rows and their written-out forms (F97; <c>.scratch/s1016/refuse-partial/NOTES.md</c>,
    /// "PCRE2 10.47"). Both are allowed by the partial contract; the port answers as the judge, and as
    /// the written-out form, which upstream does not.
    /// </remarks>
    [Test]
    [Arguments("match", @"(x)(?!y?(?1))\Z", "xy", -1, -1)]
    [Arguments("search", @"(x)(?!y?(?1))\Z", "xxy", 3, 3)]
    [Arguments("match", @"(x)(?(?!y?(?1))a|b)\Z", "xy", -1, -1)]
    [Arguments("search", @"(x)(?(?!y?(?1))a|b)\Z", "xxy", 3, 3)]
    [Arguments("match", @"(x)(?!y?(?&d))\Z(?(DEFINE)(?<d>(?1)))", "xy", -1, -1)]
    [Arguments("match", @"x(?!(?R)?y)\Z", "xx", -1, -1)]
    public void A_run_out_inside_a_call_a_negative_construct_opened_answers_as_written_out(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "match", StringComparison.Ordinal)
            ? regex.MatchAtStart(text, partial: true)
            : regex.Match(text, partial: true);

        bool found = start >= 0;
        (m.Success, m.PartialMatch, m.Success ? m.Index : -1, m.Success ? m.Index + m.Length : -1)
            .Should()
            .Be((found, found, start, end));
    }

    /// <summary>
    /// B1's review witnesses (<c>.scratch/s1021/d101/b1-review.md</c>), each pinned to the answer
    /// and captures of the same pattern with the call written out (measured 2026-10-21): a choice
    /// point inside the call (the alternatives a negative body keeps), a group captured before the
    /// call in the body, a backreference after the call, a reversed lookbehind, a negative body
    /// nested in a negative, positive or atomic construct, a verb, a lazy repeat in the callee, and
    /// a branch join after two calls.
    /// </summary>
    /// <remarks>
    /// The written-out forms, in order: <c>a(?!(?:x|))</c>, <c>a(?!(?:x*))</c>,
    /// <c>z(?!(x)(?:y))xb</c>, <c>(x)(?!y?(?:x)\1)\Z</c>, <c>(?r)\A(?&lt;!(?:x)y?)(x)</c>,
    /// <c>(x)(?!(?!y?(?:x)))y\Z</c>, <c>(x)(?=(?!y?(?:x)))\Z</c>, <c>(x)(?&gt;(?!y?(?:x)))\Z</c>,
    /// <c>(x)(?!(?:y|))(*PRUNE)</c>, <c>(?!(?:x*?)a)</c> and <c>(x)(y)(?!(?:(?:x)|(?:y))\1)</c>.
    /// </remarks>
    [Test]
    [Arguments("match", @"a(?!(?&d))(?(DEFINE)(?<d>x|))", "a", 0, "None")]
    [Arguments("match", @"a(?!(?&d))(?(DEFINE)(?<d>x*))", "a", 0, "None")]
    [Arguments("match", @"z(?!(x)(?&d))xb(?(DEFINE)(?<d>y))", "zx", 1, "P(0, 2) g1=-")]
    [Arguments("match", @"(x)(?!y?(?1)\1)\Z", "xy", 1, "None")]
    [Arguments("search", @"(?r)\A(?<!(?1)y?)(x)", "yx", 1, "P(0, 0) g1=-")]
    [Arguments("match", @"(x)(?!(?!y?(?1)))y\Z", "xy", 1, "None")]
    [Arguments("match", @"(x)(?=(?!y?(?1)))\Z", "xy", 1, "None")]
    [Arguments("match", @"(x)(?>(?!y?(?1)))\Z", "xy", 1, "None")]
    // Since the always-matching fold (LookaroundFolder), None: the body `y|` matches empty at every
    // position, so the lookahead fails whatever follows and no text completes a match. It answered
    // P(0, 1), as its written-out form did: the 'y' branch ran out first and the verb made the empty
    // branch's success count as blocked.
    [Arguments("match", @"(x)(?!(?&d))(*PRUNE)(?(DEFINE)(?<d>y|))", "x", 1, "None")]
    [Arguments("match", @"(?!(?&d)a)(?(DEFINE)(?<d>x*?))", "xx", 0, "M(0, 0)")]
    [Arguments("match", @"(x)(y)(?!(?:(?1)|(?2))\1)", "xy", 2, "M(0, 2) g1=(0, 1) g2=(1, 2)")]
    public void A_call_a_negative_construct_opened_answers_as_its_written_out_form(
        string operation,
        string pattern,
        string text,
        int groups,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "match", StringComparison.Ordinal)
            ? regex.MatchAtStart(text, partial: true)
            : regex.Match(text, partial: true);

        Describe(m, groups).Should().Be(expected);
    }

    private static string Describe(Match m, int groups)
    {
        if (!m.Success)
        {
            return "None";
        }

        var text = new System.Text.StringBuilder();
        text.Append(m.PartialMatch ? 'P' : 'M')
            .Append('(')
            .Append(m.Index)
            .Append(", ")
            .Append(m.Index + m.Length)
            .Append(')');
        for (int g = 1; g <= groups; g++)
        {
            Group group = m.Groups[g];
            text.Append(" g").Append(g).Append('=');
            text.Append(group.Success ? $"({group.Index}, {group.Index + group.Length})" : "-");
        }

        return text.ToString();
    }
}
