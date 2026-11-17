using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D71 (cost per fork of a partial second pass): a continuation key is hashed once,
/// when it is built, not again for each lookup and write of the same key.
/// </summary>
/// <remarks>
/// The WPR profile of X1, <c>(?:(?=(.*)z|())a)*b\1\2</c> over 'a' * 300 + 'c' partial
/// (<c>docs/plan/2026-11-17-d71-fork-cost-profile.md</c>), put 14% of the time in hashing
/// <c>TailKeyValues</c>: the key is hashed to look it up at the lookahead's end and again when the
/// failed continuation's mark is popped and recorded. Before the fix, n = 100 made 11,095 lookups
/// and 21,992 hashes; the lookups hit about half the time, so the memo itself pays.
/// </remarks>
public sealed class ContinuationKeyHashTests
{
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(100)]
    [Arguments(200)]
    public void A_continuation_key_is_hashed_once_per_lookup_at_most(int n)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(@"(?:(?=(.*)z|())a)*b\1\2");
        string subject = new string('a', n) + "c";

        long checks = WorkCounter.ContinuationChecks;
        long hashes = WorkCounter.KeysHashed;
        Match m = regex.Match(subject, partial: true);
        checks = WorkCounter.ContinuationChecks - checks;
        hashes = WorkCounter.KeysHashed - hashes;

        // The answer the budget leaves at these sizes (D71 row).
        (m.Success, m.PartialMatch, m.Index, m.Length)
            .Should()
            .Be((true, true, n + 1, 0));
        checks.Should().BeGreaterThan(1_000, "the second pass forks at every position");
        hashes
            .Should()
            .BeLessThanOrEqualTo(checks, "each key is hashed when it is built, and its lookups reuse the hash");
    }
}
