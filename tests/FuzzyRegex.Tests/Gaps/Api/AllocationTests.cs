using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S61. What a repeated call on one compiled pattern allocates once the pattern is warm. The
/// benchmarks measure the same thing over 100,000 calls (<c>ManyInputsBenchmarks</c>); these pin
/// the floor so a change that brings back a per-call allocation fails here, not in a report.
/// </summary>
/// <remarks>
/// The count is <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which counts only this thread's
/// allocations, so tests running in parallel on other threads do not disturb it. "Warm" means the
/// pattern has already run once on the same kind of subject: the first call builds the state the
/// pattern then keeps, as upstream does with its <c>groups_storage</c>, <c>repeats_storage</c> and
/// <c>stack_storage</c> (<c>upstream/src/_regex.c</c> :577-579).
/// </remarks>
[SkipUnderStryker]
public sealed class AllocationTests
{
    [Test]
    [Arguments(@"(\w+)@(\w+)\.com", "write to someone@example.com today", FuzzyRegexOptions.None)]
    [Arguments(@"(\w+)@(\w+)\.com", "no address in this one", FuzzyRegexOptions.None)]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut", FuzzyRegexOptions.IgnoreCase)]
    [Arguments("(?:amber lantern works){e<=2}", "nothing close to it here", FuzzyRegexOptions.IgnoreCase)]
    public void A_warm_IsMatch_allocates_nothing(string pattern, string subject, FuzzyRegexOptions options)
    {
        FuzzyRegex regex = new(pattern, options);
        bool expected = regex.IsMatch(subject);

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool actual = regex.IsMatch(subject);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        actual.Should().Be(expected);
        allocated.Should().Be(0, "a predicate on a warm pattern has nothing it needs to build");
    }

    [Test]
    [Arguments(@"\d+", "call 0123 456 7890 after six", FuzzyRegexOptions.None)]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut", FuzzyRegexOptions.IgnoreCase)]
    public void A_warm_Count_allocates_nothing(string pattern, string subject, FuzzyRegexOptions options)
    {
        // Count walks the subject with the scanner and builds no Match, so the walk's state is
        // the only thing it could allocate, and the pattern's cache already holds one.
        FuzzyRegex regex = new(pattern, options);
        int expected = regex.Count(subject);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int actual = regex.Count(subject);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        actual.Should().Be(expected);
        allocated.Should().Be(0, "a count on a warm pattern has nothing it needs to build");
    }
}
