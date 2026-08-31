using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// The three fuzzy-section behaviours the compile-parity corpus does not reach, all found by the
/// S13 blind review. Every expected value here was measured against <c>regex</c> 2026.7.19 on
/// 2026-08-31.
/// </summary>
public sealed class FuzzySectionTests
{
    /// <summary>
    /// A call to the pattern as a whole, from inside a pattern that <i>is</i> a fuzzy section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>_check_group_features</c> (<c>upstream/regex/_regex_core.py:4436</c>) computes
    /// <c>fuz = isinstance(parsed, Fuzzy)</c> for a call to group 0 and appends an extra copy of
    /// the whole pattern only when the features do not match. This is the second of the two places
    /// upstream asks that question - <c>_main.py:577</c> is the other - and S13's first draft wired
    /// only the <c>_main</c> one, leaving this at S08's <c>false</c> placeholder. The result was a
    /// spurious 26-word <c>CALL_REF</c> copy of the pattern appended to the code.
    /// </para>
    /// <para>
    /// No corpus row covers it: upstream's own suite never writes a group call inside a fuzzy
    /// section. Captured by intercepting <c>regex._regex.compile</c>:
    /// </para>
    /// <code>
    /// '(?:a(?0)?){e&lt;=1}'
    ///    code = [27, 0, 0, 4294967295, 0, 4294967295, 0, 4294967295, 0, 1, 1, 1, 1, 1,
    ///            12, 5, 97, 29, 0, 1, 31, 0, 20, 20, 1]
    /// </code>
    /// </remarks>
    [Test]
    public void A_call_to_group_zero_inside_a_fuzzy_pattern_adds_no_extra_copy() =>
        PatternCompiler
            .Compile("(?:a(?0)?){e<=1}")
            .Code.Should()
            .Equal(
                27u,
                0,
                0,
                4294967295,
                0,
                4294967295,
                0,
                4294967295,
                0,
                1,
                1,
                1,
                1,
                1,
                12,
                5,
                97,
                29,
                0,
                1,
                31,
                0,
                20,
                20,
                1
            );

    /// <summary>
    /// The control: the same call with no fuzzy section at all, which pins the shape the test
    /// above is measured against. Without it, that test would still pass if group calls stopped
    /// working altogether.
    /// </summary>
    /// <remarks>
    /// Captured the same way on 2026-08-31: <c>'(?:a(?0)?)'</c> compiles to
    /// <c>[11, 0, 12, 1, 97, 29, 0, 1, 31, 0, 20, 20, 1]</c>. The leading <c>CALL_REF 0</c> is the
    /// whole-pattern wrapper <c>_main._compile</c> adds (<c>upstream/regex/_main.py:627-634</c>);
    /// what neither row carries is the <i>additional</i> copy appended after <c>SUCCESS</c>, which
    /// is what the placeholder <c>fuz</c> produced.
    /// </remarks>
    [Test]
    public void A_call_to_group_zero_with_no_fuzzy_section_compiles_to_upstreams_bytecode() =>
        PatternCompiler.Compile("(?:a(?0)?)").Code.Should().Equal(11u, 0, 12, 1, 97, 29, 0, 1, 31, 0, 20, 20, 1);

    /// <summary>
    /// A backreference inside a fuzzy test. <c>Fuzzy.fix_groups</c>
    /// (<c>upstream/regex/_regex_core.py:2822</c>) descends into <c>subpattern</c> and not into
    /// <c>constraints["test"]</c>, so the reference is never resolved and upstream puts the
    /// <i>text</i> in the code list, which its own compiler rejects.
    /// </summary>
    /// <remarks>
    /// <code>
    /// &gt;&gt;&gt; regex.compile(r'(a)(?:abc){e&lt;=1:\1}')
    /// RuntimeError: invalid RE code
    /// &gt;&gt;&gt; regex.compile(r'(a)(?:abc){e&lt;=1:\g&lt;1&gt;}')
    /// RuntimeError: invalid RE code
    /// </code>
    /// <para>
    /// What matters is that the pattern is <b>rejected rather than quietly compiled</b>, which is
    /// the rule already set by <c>Gaps/Parsing/UpstreamInternalErrorTests</c>. It very nearly was
    /// not: this port's <c>RefGroup.GroupNumber</c> defaults to 0, a perfectly valid code word, so
    /// before the guard the pattern compiled to a reference to group 0 and would have matched the
    /// whole subject once the engine landed.
    /// </para>
    /// </remarks>
    [Test]
    [Arguments(@"(a)(?:abc){e<=1:\1}")]
    [Arguments(@"(a)(?:abc){e<=1:\g<1>}")]
    public void A_backreference_inside_a_fuzzy_test_is_rejected(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern);

        compile.Should().Throw<NotSupportedException>().WithMessage("*invalid RE code*");
    }

    /// <summary>
    /// The control: the same backreference in the fuzzy section's <i>subpattern</i>, which
    /// <c>fix_groups</c> does reach, so it resolves and compiles.
    /// </summary>
    /// <remarks>
    /// <c>'(a)(?:\1){e&lt;=1}'</c> compiles to
    /// <c>[30, 1, 1, 1, 12, 1, 97, 20, 27, 0, 0, 4294967295, 0, 4294967295, 0, 4294967295, 0, 1,
    /// 1, 1, 1, 1, 46, 4, 1, 20, 1]</c>, captured on 2026-08-31.
    /// </remarks>
    [Test]
    public void A_backreference_inside_a_fuzzy_subpattern_resolves_normally() =>
        PatternCompiler
            .Compile(@"(a)(?:\1){e<=1}")
            .Code.Should()
            .Equal(
                30u,
                1,
                1,
                1,
                12,
                1,
                97,
                20,
                27,
                0,
                0,
                4294967295,
                0,
                4294967295,
                0,
                4294967295,
                0,
                1,
                1,
                1,
                1,
                1,
                46,
                4,
                1,
                20,
                1
            );

    /// <summary>
    /// A cost equation with no digits after its comparator. Upstream's <c>parse_cost_equation</c>
    /// (<c>upstream/regex/_regex_core.py:796</c>) calls a bare <c>int(parse_count(source))</c>
    /// here rather than <c>parse_cost_limit</c>, so an empty count escapes <c>regex.compile</c> as
    /// a plain <c>ValueError</c> and not as its own <c>error</c> type.
    /// </summary>
    /// <remarks>
    /// <code>
    /// &gt;&gt;&gt; regex.compile('a{1i&lt;=}')
    /// ValueError: invalid literal for int() with base 10: ''
    /// </code>
    /// <para>
    /// This port raises the <c>"bad fuzzy cost limit"</c> that upstream itself would have raised
    /// one line later, at the same offset, rather than reproducing a Python <c>ValueError</c>
    /// message. Unlike the <c>UpstreamInternalErrorTests</c> cases, this is not upstream calling a
    /// method that does not exist - it is a malformed pattern that upstream simply forgot to route
    /// through its own error function - so a <see cref="FuzzyRegexParseException"/> is what a
    /// caller catching parse errors should get. Recorded in PORTMAP's "Where we diverge".
    /// </para>
    /// </remarks>
    [Test]
    [Arguments("a{1i<=}", 6)]
    [Arguments("a{1i<}", 5)]
    [Arguments("a{2i+3d<}", 8)]
    [Arguments("(?:abc){1i<=}", 12)]
    public void A_cost_equation_with_no_limit_is_rejected_as_a_bad_fuzzy_cost_limit(string pattern, int offset)
    {
        Action compile = () => PatternCompiler.Compile(pattern);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("bad fuzzy cost limit");
        error.Offset.Should().Be(offset);
    }
}
