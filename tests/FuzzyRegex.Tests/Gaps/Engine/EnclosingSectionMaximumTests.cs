using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// An error is made only if it fits the maximums of every fuzzy section around the one it is made
/// in, not only that section's own, so a committing construct inside a section cannot commit to a
/// path the section around it must fail.
/// </summary>
/// <remarks>
/// <para>
/// Known defect D133, shared with upstream (<c>regex</c> 2026.9.29 answers as this port did). An item
/// asks only the innermost section whether one more error fits (<c>this_error_permitted</c>,
/// <c>upstream/src/_regex.c</c>:9676); an enclosing section checks its own maximums only at its
/// END_FUZZY. In <c>(?:(?&gt;(?:ab){e&lt;=1})){d&lt;=1}</c> over 'bb' the inner section substitutes the
/// 'a' first, the atomic group commits to that, and the outer <c>{d&lt;=1}</c>, which allows no
/// substitution, then fails the only path left. Deleting the 'a' is within both sections' limits, and
/// without the atomic group the same pattern finds it.
/// </para>
/// <para>
/// Model E: when an error is made, every enclosing section's maximums (per kind, total, cost, and the
/// match's own limits) must hold for the counts it will have once the inner sections close. Counts
/// only grow along a path and no cost is negative, so a path this prunes is one END_FUZZY fails on
/// every continuation, and nothing changes where no construct commits. The walk passes atomic
/// groups and positive lookaround bodies, whose errors count outward (D130), and stops at a negative
/// lookaround body, whose errors are dropped, and at a condition's test, which is not monotone.
/// </para>
/// <para>
/// Expected answers: <c>tools/probes/fuzzy-reference-matcher.py</c> with <c>EAGER_MAXIMA</c> on, for
/// the rows it supports; the group call and lookaround rows by the argument written beside them
/// (<c>.scratch/s1101/d133/DESIGN.md</c>, blind refutation review 2026-11-02).
/// </para>
/// </remarks>
public sealed class EnclosingSectionMaximumTests
{
    /// <summary>
    /// A committing construct inside an outer section that forbids the inner section's first choice
    /// of error: the inner section takes the deletion the outer one allows. Upstream and the port
    /// before D133: None on every row.
    /// </summary>
    /// <remarks>
    /// The group call row: the called group's section is entered from the outer section through the
    /// run-time section link, so the outer limit reaches it as it reaches an inline section. The
    /// positive lookahead row: the lookahead's body is atomic and its errors count against the outer
    /// section, so its substitution is refused, the body deletes the 'a' instead, and 'b' then matches
    /// at the start.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The subject.</param>
    /// <param name="end">The end of the expected match, which starts at 0.</param>
    // DIVERGES FROM UPSTREAM 2026.9.29, and this test pins OUR answer. Upstream: None on every row.
    [Test]
    [Arguments(@"(?:(?>(?:ab){e<=1})){d<=1}", "bb", 1)]
    [Arguments(@"(?:(?:ab){e<=1}(*PRUNE)){d<=1}", "bb", 1)]
    [Arguments(@"(?:(?:(?:ab){e<=1})++){d<=1}", "bb", 1)]
    [Arguments(@"(?:x(?>(?:ab){e<=1})){d<=1}", "xbb", 2)]
    [Arguments(@"(?:(?>(?:ab){e<=1})){1d+2s<=1}", "bb", 1)]
    [Arguments(@"(?:(?>(?&g))){d<=1}(?(DEFINE)(?P<g>(?:ab){e<=1}))", "bb", 1)]
    [Arguments(@"(?:(?=(?:ab){e<=1})b){d<=1}", "bb", 1)]
    public void A_committed_inner_error_fits_the_enclosing_section(string pattern, string text, int end)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    /// <summary>
    /// A trailing insertion, which END_FUZZY's backtrack arm offers after a section has closed, also
    /// counts against the sections around it. The inner section first ends after the 'a' (or 'ab'),
    /// the zero-error <c>c</c> fails on 'x', and the trailing insertion of the 'x' fits the inner
    /// section but not the outer one, which allows no insertion. Refused, the inner section's own
    /// next choice consumes the 'x' with no error. The port before D133 and the reference
    /// matcher's model L: None. Upstream 2026.9.29 answers (0, 2) with one substitution and
    /// (0, 2) with one deletion, neither of which the reference matcher finds under either model;
    /// not judged here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The last two rows are an insertion after a fuzzy string that matched exactly (upstream's
    /// <c>fuzzy_insert</c> and <c>retry_fuzzy_insert</c>): the inner section's <c>ab</c> matches,
    /// the zero-error <c>c</c> fails on 'x', and the insertions of 'x' (and 'y') after the string fit
    /// the inner section but not the outer one; refused, the second alternative matches the whole
    /// subject with no error. Upstream and the port before D133: None on both.
    /// </para>
    /// <para>
    /// The first of them needs the check on the frame's first insertion (the push checks it and the
    /// retry checks it again: either one alone holds it). The second needs the retry's own check:
    /// the outer section allows one insertion, so the frame is pushed and its first insertion
    /// allowed, and only the second is refused.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The subject, which the match covers.</param>
    // DIVERGES FROM UPSTREAM 2026.9.29, and this test pins OUR answer.
    [Test]
    [Arguments(@"(?:(?>(?:ax??){e<=1}(?:c){e<=0})){s<=1}", "axc")]
    [Arguments(@"(?:(?>(?:ab|[a]bx){e<=1}(?:c){e<=0})){d<=1}", "abxc")]
    [Arguments(@"(?:(?>(?:ab(?:c){e<=0}|abxc){e<=1})){d<=1}", "abxc")]
    [Arguments(@"(?:(?>(?:ab(?:c){e<=0}|abxyc){e<=2})){i<=1}", "abxyc")]
    public void An_insertion_after_a_section_or_string_fits_the_enclosing_section(string pattern, string text)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, text.Length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    /// <summary>
    /// The failed-call memo keys the room left under the enclosing sections' maximums. The first
    /// call is made at 2 after the outer section has spent its one substitution on the 'a' of
    /// <c>ab</c>, so the called group's section can take no error and the call fails; the second,
    /// through <c>b[b]</c>, is made at 2 with the same inner counts and the substitution still free,
    /// and succeeds by substituting the called group's 'c'. A key without the room skips it.
    /// </summary>
    /// <param name="eager">Whether the memo is on from the first call.</param>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void The_failed_call_memo_tells_apart_calls_with_different_room_outside(bool eager)
    {
        FuzzyRegex regex = GroupCallTests.WithCallsKept(
            @"(?:(?:ab|b[b])(?:(?1)){e<=2}){s<=1}(?(DEFINE)((?:cd){e<=1}))",
            EngineWork.HangGuard
        );
        regex.PatternObject.EagerCallMemo = eager;
        regex.PatternObject.SkipCallMemo = !eager;

        Match m = regex.MatchAtStart("bbxd");

        regex.PatternObject.UseCallMemo.Should().BeTrue();
        (m.Success, m.Index, m.Length).Should().Be((true, 0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    /// <summary>
    /// The search finds the match at the first start where one exists, rather than at the next start
    /// where the inner section's first choice happens to suit the outer one. Upstream and the port
    /// before D133: (2, 3).
    /// </summary>
    // DIVERGES FROM UPSTREAM 2026.9.29, and this test pins OUR answer.
    [Test]
    public void A_search_finds_the_earliest_start()
    {
        Match m = new FuzzyRegex(@"(?:(?>(?:ab){e<=1})){d<=1}").Match("zbb");

        (m.Success, m.Index, m.Length).Should().Be((true, 1, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    /// <summary>
    /// Where nothing commits, the enclosing section's END_FUZZY already rejected the path and the
    /// search went on to the deletion, so the answer is the same as before, in both engines.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [Test]
    [Arguments(@"(?:(?:ab){e<=1}){d<=1}")]
    [Arguments(@"(?>(?:(?:ab){e<=1}){d<=1})")]
    public void Without_a_commit_inside_the_outer_section_nothing_changes(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("bb");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    /// <summary>
    /// A negative lookaround's body drops its errors when it ends, so the sections around it do not
    /// limit them, and the body's substitution still makes the lookaround fail. Asking the outer
    /// section would refuse that substitution and turn the lookaround true: <c>bb</c> would then
    /// match, and <c>c</c> would match after the second row's lookahead.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The subject.</param>
    [Test]
    [Arguments(@"(?:(?!(?:ab){s<=1})bb){d<=1}", "bb")]
    [Arguments(@"(?:(?!(?:b){e<=2}x)c){i<=1}", "cx")]
    public void A_negative_lookaround_body_is_not_limited_by_the_sections_around_it(string pattern, string text) =>
        new FuzzyRegex(pattern).MatchAtStart(text).Success.Should().BeFalse();

    /// <summary>
    /// A condition's test stops the walk (deferred; a test is not monotone, so pruning inside it lets
    /// the no-branch run). The test's substitution is made, the yes-branch runs, and the outer
    /// <c>{d&lt;=1}</c> fails it at its end, as in both engines before D133. Walking past the test
    /// would refuse the substitution, let the test delete the 'a' instead, and answer (0, 1).
    /// </summary>
    [Test]
    public void A_condition_test_is_not_limited_by_the_sections_around_it() =>
        new FuzzyRegex(@"(?:(?(?=(?:ab){s<=1})b|bb)){d<=1}").MatchAtStart("bb").Success.Should().BeFalse();
}
