using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Substitution;

/// <summary>
/// The substitution rules upstream's own suite does not assert, each one measured against
/// <c>regex</c> 2026.7.19 on Python 3.14.6 on 2026-09-01 and quoted beside the assertion.
/// </summary>
/// <remarks>
/// These are gap tests, not ported ones: they pin behaviour this port had to go and find out, so
/// they do not count towards the parity percentage. Every one of them was a decision S24 could
/// have got wrong silently - the shortcut that skips validation, the two different exceptions for
/// the same bad reference, and the count convention that is upstream's inverted.
/// </remarks>
public sealed class SubstitutionRulesTests
{
    [Test]
    public void A_count_of_zero_makes_no_replacements_where_a_negative_count_means_no_limit()
    {
        // This surface spells "no limit" as -1 (S01) where upstream spells it 0, so the two
        // conventions are swapped in Subx. Upstream's own answers, for the same two questions:
        // regex.subn('a', 'b', 'aaaaa', count=0)  is ('bbbbb', 5)
        // regex.subn('a', 'b', 'aaaaa', count=-1) is ('aaaaa', 0)
        var pattern = new FuzzyRegex("a");

        pattern.Replace("aaaaa", "b", -1, out int unlimited).Should().Be("bbbbb");
        unlimited.Should().Be(5);

        pattern.Replace("aaaaa", "b", 0, out int none).Should().Be("aaaaa");
        none.Should().Be(0);
    }

    [Test]
    public void A_subject_too_short_for_the_pattern_is_returned_before_the_template_is_even_compiled()
    {
        // pattern_subx's shortcut (upstream/src/_regex.c:21762) runs before the template compiler,
        // so the same malformed template is rejected or not depending on the subject's length:
        // regex.sub('xx', r'\g<bad', 'z') is 'z' and regex.sub('x', r'\g<bad', 'z') raises
        // `error: missing > at position 6`.
        FuzzyRegex.Replace("z", "xx", @"\g<bad").Should().Be("z");

        Action longEnough = static () => _ = FuzzyRegex.Replace("z", "x", @"\g<bad");
        longEnough.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    public void An_out_of_range_group_reference_is_a_parse_error_in_Replace_and_an_argument_error_in_Result()
    {
        // The same template, two answers, because upstream checks it in two places:
        // regex.sub('x', r'\1', 'x')             raises error: invalid group reference
        // regex.match('x', 'x').expand(r'\1')    raises IndexError: no such group
        Action replace = static () => _ = FuzzyRegex.Replace("x", "x", @"\1");
        replace.Should().Throw<FuzzyRegexParseException>().WithMessage("invalid group reference");

        Action result = static () => _ = FuzzyRegex.MatchAtStart("x", "x").Result(@"\1");
        result.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Only_Replace_short_circuits_a_literal_template_so_only_it_accepts_a_lone_brace()
    {
        // check_replacement_string is called by sub and by expand, but not by expandf:
        // regex.subf(r'(\w+)', '}', 'ab')                is '}'
        // regex.match(r'(\w+)', 'ab').expandf('}')       raises ValueError
        FuzzyRegex.ReplaceFormat("ab", @"(\w+)", "}").Should().Be("}");

        Action resultFormat = static () => _ = FuzzyRegex.MatchAtStart("ab", @"(\w+)").ResultFormat("}");
        resultFormat.Should().Throw<FormatException>();
    }

    [Test]
    [Arguments("{")]
    [Arguments("{0}{}")]
    [Arguments("{}{0}")]
    public void A_malformed_format_template_is_rejected(string format)
    {
        // Upstream's are ValueErrors out of str.format: "Single '{' encountered in format string"
        // and "cannot switch from automatic field numbering to manual field specification".
        Action act = () => _ = FuzzyRegex.ReplaceFormat("ab", @"(\w)(\w)", format);

        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Automatic_field_numbering_walks_the_arguments_from_the_whole_match_onwards()
    {
        // regex.subf(r'(\w)(\w)', '{}{}{}', 'ab') is 'abab': the arguments are the whole match,
        // then group 1, then group 2.
        FuzzyRegex.ReplaceFormat("ab", @"(\w)(\w)", "{}{}{}").Should().Be("abab");
    }

    [Test]
    [Arguments("{1:>10}")]
    [Arguments("{1!r}")]
    [Arguments("{1!a}")]
    public void A_format_spec_or_a_repr_conversion_is_refused_because_upstream_refuses_it_too(string format)
    {
        // Upstream formats a `_regex.Capture`, not a `str`, so a spec raises
        // `TypeError: unsupported format string passed to _regex.Capture.__format__` and !r gives
        // '<_regex.Capture object at 0x...>' - an interpreter address, which no port can reproduce.
        Action act = () => _ = FuzzyRegex.ReplaceFormat("ab", @"(\w+)", format);

        act.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void The_too_short_shortcut_counts_codepoints_not_utf16_code_units()
    {
        // U+1F600 is one codepoint and two UTF-16 code units, so a two-codepoint pattern does not
        // fit it and the shortcut fires before the template is compiled:
        // regex.subn('..', r'\g<bad', '\U0001F600') is ('\U0001f600', 0)
        // Comparing min_width against the code-unit count instead makes 2 > 2 false, the template
        // is compiled, and a subject upstream returns untouched raises here. Found by S24's review.
        FuzzyRegex.Replace("\U0001F600", "..", @"\g<bad").Should().Be("\U0001F600");

        // The BMP subject of the same code-unit length does not fit either pattern, so both engines
        // compile the template and reject it - which is what makes the line above about the
        // *arithmetic* rather than about the shortcut existing.
        Action bmp = static () => _ = FuzzyRegex.Replace("ab", "..", @"\g<bad");
        bmp.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    [Arguments("{1[0]}", "a")]
    [Arguments("{1[01]}", "b")]
    [Arguments("{1[-1]}", "c")]
    [Arguments("{1[+1]}", "b")]
    public void A_capture_subscript_is_a_decimal_index_and_an_unsigned_one_may_carry_leading_zeros(
        string format,
        string expected
    ) =>
        // regex.subf(r'(\w)+', '{1[01]}', 'abc') is 'b': CPython's field parser converts an
        // all-digit key to an int itself, so no leading-zero rule applies to it.
        FuzzyRegex.ReplaceFormat("abc", @"(\w)+", format).Should().Be(expected);

    [Test]
    [Arguments("{1[-01]}")]
    [Arguments("{1[ 0 ]}")]
    [Arguments("{1[0x1]}")]
    public void A_capture_subscript_this_port_does_not_read_as_a_decimal_is_refused(string format)
    {
        // Two different reasons, both pinned so a future widening has to decide about each.
        // '-01': upstream refuses it too, with TypeError - a *signed* key goes through
        // index_to_integer, which is int(text, 0), and base 0 forbids a redundant leading zero.
        // ' 0 ' and '0x1': upstream accepts both (they are 'a' and 'b'), and this port does not -
        // the marked ceiling at Substitution.TryParseSubscript. Refusing is the safe direction.
        Action act = () => _ = FuzzyRegex.ReplaceFormat("abc", @"(\w)+", format);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void A_subscript_past_the_end_of_a_capture_list_is_rejected()
    {
        // capture_getitem raises IndexError("list index out of range"); group 1 of '(.)' captures
        // once, so [1] is past the end and [-1] is the one capture there is.
        FuzzyRegex.ReplaceFormat("a", "(.)", "{1[-1]}").Should().Be("a");

        Action act = static () => _ = FuzzyRegex.ReplaceFormat("a", "(.)", "{1[1]}");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void A_reverse_substitution_puts_the_pieces_back_in_forward_order()
    {
        // `(?r)` reverses the join list as well as the scan, so the result reads left to right
        // even though the matches were found right to left:
        // regex.subn('(?r)(.)', r'x\1y', 'ab') is ('xayxby', 2)
        // and with count=1 only the *last* match is replaced: ('axby', 1).
        var pattern = new FuzzyRegex("(?r)(.)");

        pattern.Replace("ab", @"x\1y", -1, out int all).Should().Be("xayxby");
        all.Should().Be(2);

        pattern.Replace("ab", @"x\1y", 1, out int one).Should().Be("axby");
        one.Should().Be(1);
    }

    [Test]
    public void An_empty_match_advances_by_one_and_the_two_version_flags_agree_about_it()
    {
        // The V0/V1 split this slice was told to probe does not exist in this release: modern
        // pattern_subx sets must_advance from the match alone, with no version test anywhere in it.
        // regex.subn('(?V0)x*', '-', 'abxd') and regex.subn('(?V1)x*', '-', 'abxd') are both
        // ('-a-b--d-', 5), and so is regex.subn('x*', '-', 'abxd').
        foreach (string pattern in new[] { "x*", "(?V0)x*", "(?V1)x*" })
        {
            new FuzzyRegex(pattern).Replace("abxd", "-", -1, out int made).Should().Be("-a-b--d-");
            made.Should().Be(5);
        }
    }

    [Test]
    public void An_evaluators_return_value_is_never_expanded_and_a_null_one_contributes_nothing()
    {
        // Upstream adds nothing to the join list when the callable returns None; the .NET
        // delegate's equivalent is a null string, which the ported suite never exercises.
        FuzzyRegex.Replace("x", ".", static _ => "\\1").Should().Be("\\1");
        FuzzyRegex.Replace("aba", "a", static _ => null!).Should().Be("b");
    }

    [Test]
    public void Every_substitution_entry_point_rejects_a_null_argument()
    {
        var pattern = new FuzzyRegex("a");

        ((Action)(() => _ = pattern.Replace(null!, "z"))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = pattern.Replace("a", (string)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = pattern.Replace("a", (MatchEvaluator)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = pattern.ReplaceFormat("a", null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = pattern.Match("a").Result(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = pattern.Match("a").ResultFormat(null!))).Should().Throw<ArgumentNullException>();
    }
}
