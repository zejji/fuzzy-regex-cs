using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Recursion;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_recursive</c> (lines 2805-2875).
/// </summary>
/// <remarks>
/// Every <c>(?r)</c> variant here is right-to-left, not a duplicate of its unflagged sibling, even
/// where the expected values happen to differ only in which group captured which text. They were
/// tagged <c>needs:right-to-left</c> until S23 delivered that direction; every one of them also
/// recurses, so they now carry <c>needs:recursion</c> like their unflagged siblings.
/// Assertion #29 (line 2869,
/// <c>#self.assertEqual(bool(rgx.search('&lt;foo/&gt;foo')), False)</c>) is commented out in
/// upstream with the note "The next regex should and does match. Perl 5.14 agrees.", so it is not
/// executed there either; it is not ported, and its index is left out of the map below on purpose
/// so the surrounding numbering keeps matching upstream by eye.
/// </remarks>
public sealed class RecursiveTests
{
    [Test]
    [Arguments("xx", "xx", "x", "")]
    [Arguments("aba", "aba", "a", "b")]
    [Arguments("abba", "abba", "a", null)]
    [Arguments("kayak", "kayak", "k", null)]
    [Arguments("paper", "pap", "p", "a")]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Property("Upstream", "RegexTests.test_recursive#1-5")]
    public void Recursive_backreference_matches_the_run_around_a_repeated_character(
        string subject,
        string expectedValue,
        string expectedGroup1,
        string? expectedGroup2
    )
    {
        Match m = FuzzyRegex.Match(subject, @"(\w)(?:(?R)|(\w?))\1");

        m.Value.Should().Be(expectedValue);
        m.Groups[1].Value.Should().Be(expectedGroup1);
        if (expectedGroup2 is null)
        {
            m.Groups[2].Success.Should().BeFalse();
        }
        else
        {
            m.Groups[2].Value.Should().Be(expectedGroup2);
        }
    }

    [Test]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Property("Upstream", "RegexTests.test_recursive#6")]
    public void Recursive_backreference_does_not_match_without_a_repeated_character() =>
        FuzzyRegex.Match("dontmatchme", @"(\w)(?:(?R)|(\w?))\1").Success.Should().BeFalse();

    [Test]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Arguments("xx", "xx", "", "x")]
    [Arguments("aba", "aba", "b", "a")]
    [Arguments("abba", "abba", null, "a")]
    [Arguments("kayak", "kayak", null, "k")]
    [Arguments("paper", "pap", "a", "p")]
    [Property("Upstream", "RegexTests.test_recursive#7-11")]
    public void Reversed_recursive_backreference_matches_the_run_around_a_repeated_character(
        string subject,
        string expectedValue,
        string? expectedGroup1,
        string expectedGroup2
    )
    {
        Match m = FuzzyRegex.Match(subject, @"(?r)\2(?:(\w?)|(?R))(\w)");

        m.Value.Should().Be(expectedValue);
        if (expectedGroup1 is null)
        {
            m.Groups[1].Success.Should().BeFalse();
        }
        else
        {
            m.Groups[1].Value.Should().Be(expectedGroup1);
        }
        m.Groups[2].Value.Should().Be(expectedGroup2);
    }

    [Test]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Property("Upstream", "RegexTests.test_recursive#12")]
    public void Reversed_recursive_backreference_does_not_match_without_a_repeated_character() =>
        FuzzyRegex.Match("dontmatchme", @"(?r)\2(?:(\w?)|(?R))(\w)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:recursion - needs (?R) recursion and atomic groups; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_recursive#13")]
    public void Recursive_atomic_alternation_matches_balanced_parens_and_captures_the_last_run()
    {
        Match m = FuzzyRegex.Match("(ab(cd)ef)", @"\(((?>[^()]+)|(?R))*\)");

        m.Value.Should().Be("(ab(cd)ef)");
        m.Groups[1].Value.Should().Be("ef");
    }

    [Test]
    [Skip("needs:recursion - needs (?R) recursion, atomic groups and capture lists; the engine has none of them yet")]
    [Property("Upstream", "RegexTests.test_recursive#14")]
    public void Recursive_atomic_alternation_captures_every_repetition_of_group_one() =>
        FuzzyRegex
            .Match("(ab(cd)ef)", @"\(((?>[^()]+)|(?R))*\)")
            .Groups[1]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("ab", "cd", "(cd)", "ef");

    [Test]
    [Skip("needs:recursion - needs (?R) recursion and atomic groups; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_recursive#15")]
    public void Reversed_recursive_atomic_alternation_matches_balanced_parens_and_captures_the_first_run()
    {
        Match m = FuzzyRegex.Match("(ab(cd)ef)", @"(?r)\(((?R)|(?>[^()]+))*\)");

        m.Value.Should().Be("(ab(cd)ef)");
        m.Groups[1].Value.Should().Be("ab");
    }

    [Test]
    [Skip("needs:recursion - needs (?R) recursion, atomic groups and capture lists; the engine has none of them yet")]
    [Property("Upstream", "RegexTests.test_recursive#16")]
    public void Reversed_recursive_atomic_alternation_captures_every_repetition_of_group_one_in_reverse() =>
        FuzzyRegex
            .Match("(ab(cd)ef)", @"(?r)\(((?R)|(?>[^()]+))*\)")
            .Groups[1]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("ef", "cd", "(cd)", "ab");

    [Test]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Property("Upstream", "RegexTests.test_recursive#17")]
    public void Recursive_alternation_matches_the_innermost_balanced_group_within_surrounding_text()
    {
        Match m = FuzzyRegex.Match("some text (a(b(c)d)e) more text", @"\(([^()]+|(?R))*\)");

        m.Value.Should().Be("(a(b(c)d)e)");
        m.Groups[1].Value.Should().Be("e");
    }

    [Test]
    [Skip("needs:recursion - (?R) whole-pattern recursion is not implemented yet")]
    [Property("Upstream", "RegexTests.test_recursive#18")]
    public void Reversed_recursive_alternation_matches_the_innermost_balanced_group_within_surrounding_text()
    {
        Match m = FuzzyRegex.Match("some text (a(b(c)d)e) more text", @"(?r)\(((?R)|[^()]+)*\)");

        m.Value.Should().Be("(a(b(c)d)e)");
        m.Groups[1].Value.Should().Be("a");
    }

    [Test]
    [Skip("needs:recursion - needs (?2) numbered-group recursion and atomic groups; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_recursive#19")]
    public void Recursive_numbered_group_reference_matches_nested_parens_in_a_function_call()
    {
        Match m = FuzzyRegex.Match("foo(bar(baz)+baz(bop))", @"(foo(\(((?:(?>[^()]+)|(?2))*)\)))");

        m.Value.Should().Be("foo(bar(baz)+baz(bop))");
        m.Groups[1].Value.Should().Be("foo(bar(baz)+baz(bop))");
        m.Groups[2].Value.Should().Be("(bar(baz)+baz(bop))");
        m.Groups[3].Value.Should().Be("bar(baz)+baz(bop)");
    }

    [Test]
    [Skip("needs:recursion - needs (?2) numbered-group recursion and atomic groups; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_recursive#20")]
    public void Reversed_recursive_numbered_group_reference_matches_nested_parens_in_a_function_call()
    {
        Match m = FuzzyRegex.Match("foo(bar(baz)+baz(bop))", @"(?r)(foo(\(((?:(?2)|(?>[^()]+))*)\)))");

        m.Value.Should().Be("foo(bar(baz)+baz(bop))");
        m.Groups[1].Value.Should().Be("foo(bar(baz)+baz(bop))");
        m.Groups[2].Value.Should().Be("(bar(baz)+baz(bop))");
        m.Groups[3].Value.Should().Be("bar(baz)+baz(bop)");
    }

    [Test]
    [Arguments("<foo><bar></bar></foo>", true)]
    [Arguments("<foo><bar></foo></bar>", false)]
    [Arguments("<foo><bar/></foo>", true)]
    [Arguments("<foo><bar></foo>", false)]
    [Arguments("<foo bar=baz/>", false)]
    [Arguments("<foo bar=\"baz\">", false)]
    [Arguments("<foo bar=\"baz\"/>", true)]
    [Arguments("<    fooo   /  >", true)]
    [Arguments("foo<foo/>", false)]
    [Arguments("<foo>foo</foo>", true)]
    [Arguments("<foo><bar/>foo</foo>", true)]
    [Arguments("<a><b><c></c></b></a>", true)]
    [Skip(
        "needs:recursion - needs (?1) numbered-group recursion, conditionals ((?(3)|...)) and a backreference; the engine has none of them yet"
    )]
    [Property("Upstream", "RegexTests.test_recursive#21-28,30-33")]
    public void Recursive_numbered_group_reference_checks_balanced_xml_like_tags(string subject, bool expected) =>
        FuzzyRegex.Match(subject, _htmlLikeTagPattern).Success.Should().Be(expected);

    private const string _htmlLikeTagPattern =
        @"^\s*(<\s*([a-zA-Z:]+)(?:\s*[a-zA-Z:]*\s*=\s*(?:'[^']*'|""[^""]*""))*\s*(/\s*)?>(?:[^<>]*|(?1))*(?(3)|<\s*/\s*\2\s*>))\s*$";
}
