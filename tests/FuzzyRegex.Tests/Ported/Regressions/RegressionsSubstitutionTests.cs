using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c> (lines 3084-4410),
/// the assertions about substitution.
/// </summary>
/// <remarks>
/// Assertion #58 calls <c>regex.sub(pattern, repl, subject, regex.WORD)</c>; <c>sub</c>'s fourth
/// positional parameter is <c>count</c>, not <c>flags</c>, so <c>regex.WORD</c> (0x800) is used
/// only as a (harmlessly large) replacement count and the WORD flag is never applied. Verified
/// against the local oracle 2026-08-30: the same call with no fourth argument, and with
/// <c>flags=regex.WORD</c>, both give the identical result. It is ported as a plain
/// <see cref="FuzzyRegex.Replace(string, string, string, FuzzyRegexOptions)"/> with no options, so
/// nobody "restores" a WORD flag this API does not have.
/// </remarks>
public sealed class RegressionsSubstitutionTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#58")]
    public void Sub_wraps_each_word_run_without_splitting_a_ZWJ_joined_cluster()
    {
        // Hg issue 85: Non-conformance to Unicode UAX#29 re: ZWJ / ZWNJ. Written as explicit
        // \u escapes, not pasted glyphs, because the subject contains U+200D ZERO WIDTH JOINER,
        // which is invisible in source.
        string subject = "\u0905\u0928\u094d\u200d\u0928 \u0d28\u0d4d\u200d \u0915\u093f\u0928";

        FuzzyRegex
            .Replace(subject, @"(\w+)", "[\\1]")
            .Should()
            .Be("[\u0905\u0928\u094d\u200d\u0928] [\u0d28\u0d4d\u200d] [\u0915\u093f\u0928]");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#61")]
    public void Sub_with_an_evaluator_that_calls_Result_replaces_every_match() =>
        // Hg issue 91: match.expand is extremely slow. Check that the replacement cache works.
        FuzzyRegex.Replace("a-b-c", "(-)", m => m.Result("x")).Should().Be("axbxc");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#82")]
    public void Sub_with_V0_dot_star_replaces_the_whole_match_and_the_trailing_empty_match() =>
        FuzzyRegex.Replace("test", "(?V0).*", "x").Should().Be("xx");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#84")]
    public void Sub_with_V1_dot_star_replaces_the_whole_match_and_the_trailing_empty_match() =>
        FuzzyRegex.Replace("test", "(?V1).*", "x").Should().Be("xx");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#85")]
    public void Sub_with_V0_lazy_dot_star_replaces_every_empty_position_between_characters() =>
        FuzzyRegex.Replace("test", "(?V0).*?", "|").Should().Be("|||||||||");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#87")]
    public void Sub_with_V1_lazy_dot_star_replaces_every_empty_position_between_characters() =>
        FuzzyRegex.Replace("test", "(?V1).*?", "|").Should().Be("|||||||||");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#88")]
    public void Sub_with_a_negative_lookahead_and_dotall_inserts_a_divider_after_the_at_sign() =>
        // Hg issue 112: re: OK, but regex: SystemError.
        FuzzyRegex
            .Replace("@\n", @"^(@)\n(?!.*?@)(.*)", @"\1\n==========\n\2", FuzzyRegexOptions.Singleline)
            .Should()
            .Be("@\n==========\n");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#105")]
    public void Sub_with_a_whole_match_backreference_in_the_template_reproduces_the_match() =>
        // Hg issue 125: Reference to entire match (\g<0>) in Pattern.sub() doesn't work as of
        // 2014.09.22 release.
        FuzzyRegex.Replace("x", "x", @"\g<0>").Should().Be("x");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#144")]
    public void Sub_wraps_every_character_with_x_and_y() =>
        // Hg issue 140: Replace with REVERSE and groups has unexpected behavior.
        FuzzyRegex.Replace("ab", "(.)", @"x\1y").Should().Be("xayxby");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#145")]
    public void Sub_wraps_every_character_with_x_and_y_when_matched_right_to_left() =>
        FuzzyRegex.Replace("ab", "(?r)(.)", @"x\1y").Should().Be("xayxby");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#146")]
    public void Subf_wraps_every_character_with_x_and_y() =>
        FuzzyRegex.ReplaceFormat("ab", "(.)", "x{1}y").Should().Be("xayxby");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#147")]
    public void Subf_wraps_every_character_with_x_and_y_when_matched_right_to_left() =>
        FuzzyRegex.ReplaceFormat("ab", "(?r)(.)", "x{1}y").Should().Be("xayxby");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#425")]
    public void Sub_with_an_alternation_expands_the_matched_branch_and_the_unmatched_branch_to_empty() =>
        // Git issue 439: Unmatched groups: sub vs subf.
        FuzzyRegex.Replace("test1", "(test1)|(test2)", @"matched: \1\2").Should().Be("matched: test1");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#426")]
    public void Subf_with_an_alternation_expands_the_matched_branch_and_the_unmatched_branch_to_empty() =>
        FuzzyRegex.ReplaceFormat("test1", "(test1)|(test2)", "matched: {1}{2}").Should().Be("matched: test1");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#427")]
    public void Result_with_an_alternation_expands_the_matched_branch_and_the_unmatched_branch_to_empty() =>
        FuzzyRegex.Match("matched: test1", "(test1)|(test2)").Result(@"matched: \1\2").Should().Be("matched: test1");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#428")]
    public void ResultFormat_with_an_alternation_expands_the_matched_branch_and_the_unmatched_branch_to_empty() =>
        FuzzyRegex
            .Match("matched: test1", "(test1)|(test2)")
            .ResultFormat("matched: {1}{2}")
            .Should()
            .Be("matched: test1");
}
