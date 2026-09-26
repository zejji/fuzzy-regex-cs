using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy constraint that allows no errors still limits the errors made inside it, however it is
/// spelled.
/// </summary>
/// <remarks>
/// <para>
/// Upstream's parser throws away a constraint whose limits are all written as zero - <c>{e&lt;=0}</c>,
/// <c>{e&lt;1}</c>, <c>{s&lt;=0,i&lt;=0,d&lt;=0}</c> - before it builds a node for it
/// (<c>is_actually_fuzzy</c>, <c>upstream/regex/_regex_core.py:548-556</c>, called at <c>:530</c>).
/// On its own that changes nothing, because a section with no errors to spend matches exactly. It
/// matters where another fuzzy section is involved: an outer zero constraint no longer caps an inner
/// section's errors, and an inner one no longer keeps an outer section's budget off its subpattern.
/// <c>{d&lt;=0}</c> and <c>{1s+1i+1d&lt;=0}</c> give the same zero budget, since naming any error
/// type sets the others to zero, but keep the node, and upstream answers those correctly. So its
/// answer depends on the spelling. Upstream issue 306 asked for exactly this scoping, and its
/// pattern still gets the wrong answer. Ledger entry 39.
/// </para>
/// <para>
/// The port always applies the constraint. Where no error could reach the section anyway - no
/// fuzzy section around it or inside it, and no group call that could carry one in - it compiles to
/// the subpattern alone, as upstream's does, so a zero constraint used on its own costs nothing.
/// Every expected value was measured on <c>regex</c> 2026.9.10 on 2026-09-26 and is quoted beside
/// its assertion.
/// </para>
/// </remarks>
public sealed class FuzzyZeroBudgetTests
{
    private static readonly string[] _droppedSpellings = ["{e<=0}", "{e<1}", "{s<=0,i<=0,d<=0}"];

    private static readonly string[] _keptSpellings = ["{d<=0}", "{1s+1i+1d<=0}"];

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: upstream's own {d<=0} twin.
    [Test]
    public void An_outer_zero_constraint_caps_the_errors_of_a_fuzzy_section_inside_it()
    {
        // match('(?:(?:ab){s<=1,d<=1}){e<=0}', 'x')            (0, 1) (1, 0, 1)
        // match('(?:(?:ab){s<=1,d<=1}){e<1}', 'x')             (0, 1) (1, 0, 1)
        // match('(?:(?:ab){s<=1,d<=1}){s<=0,i<=0,d<=0}', 'x')  (0, 1) (1, 0, 1)
        // match('(?:(?:ab){s<=1,d<=1}){d<=0}', 'x')            None
        // match('(?:(?:ab){s<=1,d<=1}){1s+1i+1d<=0}', 'x')     None
        // match('(?:(?:ab){s<=1,d<=1}){e<=1}', 'x')            None
        foreach (string zero in _droppedSpellings.Concat(_keptSpellings).Append("{e<=1}"))
        {
            string pattern = "(?:(?:ab){s<=1,d<=1})" + zero;

            new FuzzyRegex(pattern).MatchAtStart("x").Success.Should().BeFalse(pattern);
        }

        // The cap is on the errors, not on the match: with none needed, it matches.
        new FuzzyRegex("(?:(?:ab){s<=1,d<=1}){e<=0}")
            .MatchAtStart("ab")
            .Success.Should()
            .BeTrue();
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: upstream's own {d<=0} twin.
    [Test]
    public void An_inner_zero_constraint_keeps_an_outer_budget_off_its_subpattern()
    {
        // match('(?:c(?:ab){e<=0}){e<=1}', 'cax')            (0, 3) (1, 0, 0)
        // match('(?:c(?:ab){e<1}){e<=1}', 'cax')             (0, 3) (1, 0, 0)
        // match('(?:c(?:ab){s<=0,i<=0,d<=0}){e<=1}', 'cax')  (0, 3) (1, 0, 0)
        // match('(?:c(?:ab){d<=0}){e<=1}', 'cax')            None
        // match('(?:c(?:ab){1s+1i+1d<=0}){e<=1}', 'cax')     None
        foreach (string zero in _droppedSpellings.Concat(_keptSpellings))
        {
            string pattern = "(?:c(?:ab)" + zero + "){e<=1}";

            new FuzzyRegex(pattern).MatchAtStart("cax").Success.Should().BeFalse(pattern);
        }

        // The outer budget still pays for an error outside the zero section: 'x' for 'c'.
        Match m = new FuzzyRegex("(?:c(?:ab){e<=0}){e<=1}").MatchAtStart("xab");

        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: issue 306's intent.
    [Test]
    public void Upstream_issue_306s_pattern_keeps_its_inner_sections_exact()
    {
        // Issue 306 (2019): "Fuzzy match parameters not respecting quantifier scope". Closed as fixed,
        // but the fix only reaches spellings that survive is_actually_fuzzy:
        // search('(dogf(((oo){e<1})|((00){e<1}))d){e<2}', 'dogfxod')                (0, 7) (1, 0, 0)
        // search('(dogf(((oo){d<1})|((00){d<1}))d){e<2}', 'dogfxod')                None
        // search('(dogf(((oo){1s+1i+1d<1})|((00){1s+1i+1d<1}))d){e<2}', 'dogfxod')  None
        foreach (string zero in new[] { "{e<1}", "{d<1}", "{1s+1i+1d<1}" })
        {
            string pattern = $"(dogf(((oo){zero})|((00){zero}))d){{e<2}}";

            new FuzzyRegex(pattern).Match("dogfxod").Success.Should().BeFalse(pattern);
        }

        // The issue's own rows, which upstream's test suite pins, still hold (RegressionsFuzzyTests):
        // search('(?e)(dogf(((oo){e<1})|((00){e<1}))d){e<2}', 'dogfood')  (0, 7) (0, 0, 0)
        // search('(?e)(dogf(((oo){e<1})|((00){e<1}))d){e<2}', 'dogfoot')  (0, 7) (1, 0, 0)
        new FuzzyRegex(@"(?e)(dogf(((oo){e<1})|((00){e<1}))d){e<2}")
            .Match("dogfoot")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: upstream's own {d<=0} twin.
    [Test]
    public void A_zero_constraint_in_a_group_still_holds_when_a_fuzzy_section_calls_the_group()
    {
        // search('(ab){e<=0}x(?:(?1)){e<=1}', 'abxax')            (0, 5) (1, 0, 0)
        // search('(ab){e<1}x(?:(?1)){e<=1}', 'abxax')             (0, 5) (1, 0, 0)
        // search('(ab){s<=0,i<=0,d<=0}x(?:(?1)){e<=1}', 'abxax')  (0, 5) (1, 0, 0)
        // search('(ab){d<=0}x(?:(?1)){e<=1}', 'abxax')            None
        // search('(ab){1s+1i+1d<=0}x(?:(?1)){e<=1}', 'abxax')     None
        foreach (string zero in _droppedSpellings.Concat(_keptSpellings))
        {
            string pattern = "(ab)" + zero + "x(?:(?1)){e<=1}";

            new FuzzyRegex(pattern).Match("abxax").Success.Should().BeFalse(pattern);
        }

        // search('(ab)x(?:(?1)){e<=1}', 'abxax'): (0, 5) (1, 0, 0). Without the constraint the call
        // may spend the outer budget.
        new FuzzyRegex("(ab)x(?:(?1)){e<=1}")
            .Match("abxax")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_constraint_written_after_a_zero_constraint_applies_to_the_constrained_item()
    {
        // search('a{e<=0}{e<=1}', 'b'): (0, 1) (1, 0, 0) upstream, which drops the first constraint.
        // The other spellings of that zero budget refuse to parse, so they give no answer to follow:
        // search('a{d<=0}{e<=1}', 'b'): error "nothing for fuzzy constraint at position 7".
        // Upstream accepts the dropped spellings here, and so does the port; the second constraint
        // then applies to the first, as it does in (?:a{e<=0}){e<=1}, and upstream's own spelling
        // of that says no match: search('(?:a{d<=0}){e<=1}', 'b') None.
        new FuzzyRegex("a{e<=0}{e<=1}")
            .Match("b")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?:a{e<=0}){e<=1}").Match("b").Success.Should().BeFalse();
    }

    [Test]
    public void A_zero_constraint_that_no_error_can_reach_compiles_to_the_bare_pattern()
    {
        // Upstream drops each of these constraints, and the port compiles each pattern to exactly the
        // code upstream emits, which is the code of the pattern with the constraint left out.
        (string Constrained, string Bare)[] rows =
        [
            ("a{e<=0}bc", "abc"),
            ("(?:ab){e<1}c", "(?:ab)c"),
            ("(ab){s<=0,i<=0,d<=0}", "(ab)"),
            ("a{e<=0}*", "a*"),
            ("(?:a*){e<=0}*", "(?:a*)*"),
            ("(?:(?:ab){e<=0}){e<=0}", "(?:ab)"),
            ("(?:ab){e<=1}(?:cd){e<=0}", "(?:ab){e<=1}(?:cd)"),
            ("(a)(?1){e<=0}", "(a)(?1)"),
        ];

        foreach ((string constrained, string bare) in rows)
        {
            CompiledPattern withConstraint = PatternCompiler.Compile(constrained);
            CompiledPattern without = PatternCompiler.Compile(bare);

            withConstraint.Code.Should().Equal(without.Code, constrained);
            (withConstraint.ReqOffset, withConstraint.ReqFlags).Should().Be((without.ReqOffset, without.ReqFlags));
            withConstraint.ReqChars.Should().Equal(without.ReqChars, constrained);
        }
    }

    [Test]
    public void The_parser_accepts_every_zero_constraint_upstream_accepts()
    {
        // Upstream ignores a dropped constraint before it looks for something to apply it to, so it
        // accepts these where {d<=0} would raise. Measured: each compiles, and
        // search('{e<=0}', 'x') is (0, 0), search('a{e<=1}{e<=0}', 'b') is (0, 1) (1, 0, 0).
        foreach (string pattern in new[] { "{e<=0}", "x|{e<=0}", "({e<=0})", "(?:){e<=0}", "a*{e<=0}" })
        {
            Action compile = () => _ = new FuzzyRegex(pattern);

            compile.Should().NotThrow(pattern);
        }

        new FuzzyRegex("{e<=0}").Match("x").Length.Should().Be(0);
        new FuzzyRegex("a{e<=1}{e<=0}").Match("b").FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void An_atomic_group_inside_a_fuzzy_section_can_lose_a_match_when_the_budget_grows()
    {
        // Agrees with upstream. README.rst:830 says an atomic group fails as a whole, and :837 that a
        // possessive repeat is the atomic group of the greedy one, so one more allowed error can let
        // it take a path it then cannot give back. The same happens without fuzzy matching, where the
        // atomic x{0,1} followed by x finds nothing in 'x' and the atomic x{0,0} followed by x matches it.
        // Measured over 'xy' with match: {e<=0} spans (0, 2) with no errors, {e<=1} finds nothing,
        // and {e<=2} spans (0, 2) with a substitution at 1 and a deletion at 2.
        Match none = new FuzzyRegex("(?:x++y){e<=0}").MatchAtStart("xy");
        Match two = new FuzzyRegex("(?:x++y){e<=2}").MatchAtStart("xy");

        (none.Index, none.Length).Should().Be((0, 2));
        none.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        new FuzzyRegex("(?:x++y){e<=1}").MatchAtStart("xy").Success.Should().BeFalse();
        (two.Index, two.Length).Should().Be((0, 2));
        two.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 1));
        two.FuzzyChanges.Substitutions.Should().Equal(1);
        two.FuzzyChanges.Deletions.Should().Equal(2);
        new FuzzyRegex("(?>x{0,1})x").MatchAtStart("x").Success.Should().BeFalse();
    }
}
