namespace FuzzyRegex.Tests.Ported.Anchors;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c>. Written skipped ahead of the engine,
/// per the parity-ratchet regimen (design spec section 5): the slice that delivers the capability
/// named in the skip reason un-skips it.
/// </summary>
public sealed class AnchorTests
{
    [Test]
    [Skip("needs:anchors - the VM has no anchor opcodes yet")]
    [Property("Upstream", "RegexTests.test_bigcharset")]
    public void Caret_matches_at_the_start_of_the_subject()
        => Assert.Fail("Enabled by the slice that delivers needs:anchors.");
}
