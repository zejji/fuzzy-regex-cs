using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// C6 option C: the leading screen (<c>PatternObject.LeadingStrings</c> or <c>LeadingUnits</c>)
/// looks through a zero-width test in front of the literals, so <c>\b(?:cat|dog)\b</c> searches
/// for <c>cat</c> and <c>dog</c> the way <c>cat|dog</c> does. A word boundary or ^ anchor tests
/// the position an attempt stands on and moves nothing, so a match starting there still opens
/// with one of the literals, and so does a lookaround, which runs its body and goes on from the
/// position it started at. Design: <c>docs/plan/2026-10-14-multi-string-search-design.md</c>,
/// section 3.
/// </summary>
public sealed class LeadingZeroWidthTests
{
    private static readonly Dictionary<string, IReadOnlyCollection<string>> _words = new()
    {
        ["w"] = ["amber", "copper", "violet"],
    };

    /// <summary>
    /// One row per zero-width opcode the walk looks through, each the shape the compiler builds
    /// for it: the first-set check, then the test, then the branch (<c>.scratch/c6/ops.txt</c>).
    /// The second column is the opcode the row is the witness for, checked so that a row cannot
    /// pass by compiling to something else.
    /// </summary>
    /// <param name="pattern">A pattern opening with a zero-width test and then literals.</param>
    /// <param name="test">The opcode of the zero-width test.</param>
    [Test]
    [Arguments(@"\b(?:cat|dog)\b", nameof(Opcode.Boundary))]
    [Arguments(@"\B(?:cat|dog)", nameof(Opcode.Boundary))]
    [Arguments(@"(?w)\b(?:cat|dog)\b", nameof(Opcode.DefaultBoundary))]
    [Arguments(@"(?w)\B(?:cat|dog)", nameof(Opcode.DefaultBoundary))]
    [Arguments(@"\m(?:cat|dog)", nameof(Opcode.StartOfWord))]
    [Arguments(@"\M(?:cat|dog)", nameof(Opcode.EndOfWord))]
    [Arguments(@"(?w)\m(?:cat|dog)", nameof(Opcode.DefaultStartOfWord))]
    [Arguments(@"(?w)\M(?:cat|dog)", nameof(Opcode.DefaultEndOfWord))]
    [Arguments(@"(?m)^(?:cat|dog)", nameof(Opcode.StartOfLine))]
    [Arguments(@"(?mw)^(?:cat|dog)", nameof(Opcode.StartOfLineU))]
    [Arguments(@"\bcat|\bdog", nameof(Opcode.Boundary))]
    [Arguments(@"\b\b(?:cat|dog)", nameof(Opcode.Boundary))]
    [Arguments(@"(?L)\b(?:cat|dog)\b", nameof(Opcode.Boundary))]
    [Arguments(@"\b\L<w>\b", nameof(Opcode.Boundary))]
    public void A_zero_width_test_in_front_of_the_literals_is_looked_through(string pattern, string test)
    {
        PatternObject compiled = Compile(pattern, FuzzyRegexOptions.None);

        using (new AssertionScope())
        {
            compiled.StartNode!.Next1.Node!.Op.ToString().Should().Be(test);
            compiled.LeadingStrings.Should().NotBeNull();
        }
    }

    /// <summary>
    /// The same under IgnoreCase: the ignore-case first-set check, then the test, then
    /// case-insensitive literals, which give code units rather than strings.
    /// </summary>
    /// <param name="pattern">A pattern opening with a zero-width test and then literals.</param>
    [Test]
    [Arguments(@"\b(?:cat|dog)\b")]
    [Arguments(@"(?m)^(?:cat|dog)")]
    [Arguments(@"\b\L<w>\b")]
    public void A_zero_width_test_in_front_of_case_blind_literals_is_looked_through(string pattern)
    {
        PatternObject compiled = Compile(pattern, FuzzyRegexOptions.IgnoreCase);

        using (new AssertionScope())
        {
            compiled.StartNode!.Op.Should().Be(Opcode.SetUnionIgn);
            compiled.LeadingUnits.Should().NotBeNull();
        }
    }

    /// <summary>
    /// The zero-width shapes the walk leaves alone. <c>\G</c> holds at one position only, so a
    /// screen has nothing to skip, and the design refuses it. <c>^</c> and <c>\A</c> (both
    /// <c>StartOfString</c>) anchor the search, which then never consults a screen
    /// (<c>Matcher.BasicMatchCore</c>'s anchor switch). <c>\Z</c> holds only at the end, where no
    /// literal can start. An end-of-line anchor can only be followed by a newline, which the
    /// compiler factors out in front of the branch (<c>(?m)$\n(?:cat|dog)</c>), so it is not looked
    /// through: only a lone arm such as <c>(?m)$\ncat|dog</c> would reach it. <c>\K</c> moves the
    /// start the match reports. A conditional chooses what follows, and <c>\X</c> reads a grapheme.
    /// </summary>
    /// <param name="pattern">A pattern the screen must leave alone.</param>
    [Test]
    [Arguments(@"\G(?:cat|dog)")]
    [Arguments(@"^(?:cat|dog)")]
    [Arguments(@"\A(?:cat|dog)")]
    [Arguments(@"\Z(?:cat|dog)")]
    [Arguments(@"(?m)$\ncat|dog")]
    [Arguments(@"$\ncat|dog")]
    [Arguments(@"\K(?:cat|dog)")]
    [Arguments(@"(?(?=c)cat|dog)")]
    [Arguments(@"\X(?:cat|dog)")]
    [Arguments(@"\b(?:cat|dog){e<=1}")]
    [Arguments(@"(?r)\b(?:cat|dog)")]
    public void The_walk_refuses_a_zero_width_shape_it_cannot_screen(string pattern)
    {
        PatternObject compiled = Compile(pattern, FuzzyRegexOptions.None);

        using (new AssertionScope())
        {
            compiled.LeadingStrings.Should().BeNull();
            compiled.LeadingUnits.Should().BeNull();
        }
    }

    /// <summary>
    /// Every match, in UTF-16 units. Each subject puts a literal where the zero-width test fails
    /// (<c>xcat</c>, <c>catx</c>) beside one where it holds, so the screen's hit and the engine's
    /// verdict differ. The <c>K</c> in the IgnoreCase rows is U+212A KELVIN SIGN, which an ASCII
    /// word boundary does not count as a word character.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="options">The options.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Every match's span, <c>start-end</c>, space-separated.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer, 2026-10-14 (.scratch/c6/up1.py)")]
    [Arguments(@"\b(?:cat|dog)\b", FuzzyRegexOptions.None, "xcat cat catx dog. dogs", "5-8 14-17")]
    [Arguments(@"\b(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat catx dog. dogs", "5-8 9-12 14-17 19-22")]
    [Arguments(@"\B(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat catx dog. xdogs", "1-4 20-23")]
    [Arguments(@"(?w)\b(?:cat|dog)\b", FuzzyRegexOptions.None, "xcat cat's catx dog.dog", "")]
    [Arguments(@"(?w)\B(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat's catx dog.dog", "1-4 20-23")]
    [Arguments(@"\m(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat catx dog. dogs", "5-8 9-12 14-17 19-22")]
    [Arguments(@"\M(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat catx dog. dogs", "")]
    [Arguments(@"(?w)\m(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat's catx dog.dog", "5-8 11-14 16-19")]
    [Arguments(@"(?w)\M(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat's catx dog.dog", "")]
    [Arguments(@"(?m)^(?:cat|dog)", FuzzyRegexOptions.None, "xcat\ncat catx\ndog\n dog", "5-8 14-17")]
    [Arguments(@"(?mw)^(?:cat|dog)", FuzzyRegexOptions.None, "xcat\u2028cat catx\rdog\n dog", "5-8 14-17")]
    [Arguments(@"(?m)$(?:\ncat|\ndog)", FuzzyRegexOptions.None, "xcat\ncat\n\ndog\ndog", "4-8 9-13 13-17")]
    [Arguments(@"(?mw)$(?:\ncat|\ndog)", FuzzyRegexOptions.None, "xcat\ncat\n\ndog\ndog", "4-8 9-13 13-17")]
    [Arguments(@"$(?:\ncat|\ndog)", FuzzyRegexOptions.None, "cat\ncat\ndog", "")]
    [Arguments(@"(?w)$(?:\ncat|\ndog)", FuzzyRegexOptions.None, "cat\ncat\ndog", "")]
    [Arguments(@"\bcat|\bdog", FuzzyRegexOptions.None, "xcat cat xdog dog", "5-8 14-17")]
    [Arguments(@"\b\b(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat", "5-8")]
    [Arguments(@"(?L)\b(?:cat|dog)\b", FuzzyRegexOptions.None, "xcat cat", "5-8")]
    [Arguments(@"\b(?:cats?|dogs?)\b", FuzzyRegexOptions.None, "xcats cats dogsx dog", "6-10 17-20")]
    [Arguments(@"\b(?:cat|dog)\b", FuzzyRegexOptions.IgnoreCase, "xcat CAT xdog Dog", "5-8 14-17")]
    [Arguments(@"\b(?:kit|sit)\b", FuzzyRegexOptions.IgnoreCase, "a\u212AIT \u212AIT \u017Fit", "5-8 9-12")]
    [Arguments(@"(?a)\b(?:kit|sit)\b", FuzzyRegexOptions.IgnoreCase, "a\u212AIT \u212AIT KIT", "9-12")]
    public void A_zero_width_test_in_front_still_finds_every_match(
        string pattern,
        FuzzyRegexOptions options,
        string subject,
        string expected
    )
    {
        Spans(pattern, options, subject).Should().Be(expected);
    }

    [Test]
    [Property(
        "Upstream",
        @"regex 2026.9.10, 2026-10-14: finditer(r'\b\L<w>\b', 'amberx amber copper xviolet violet', w=[amber, copper, violet]) -> (7, 12), (13, 19), (28, 34); the same with I over 'amberx AMBER Copper xviolet violet'"
    )]
    public void A_whole_word_named_list_finds_every_match()
    {
        using (new AssertionScope())
        {
            Spans(@"\b\L<w>\b", FuzzyRegexOptions.None, "amberx amber copper xviolet violet")
                .Should()
                .Be("7-12 13-19 28-34");
            Spans(@"\b\L<w>\b", FuzzyRegexOptions.IgnoreCase, "amberx AMBER Copper xviolet violet")
                .Should()
                .Be("7-12 13-19 28-34");
        }
    }

    /// <summary>
    /// The slice edges. A word boundary at the start position looks at the character before it,
    /// which the screen's hit at that position does not see; one at the end of the slice holds
    /// even inside a word. A partial search is not screened, and an overlapped one starts each
    /// search one past the last match's start.
    /// </summary>
    [Test]
    [Property(
        "Upstream",
        @"regex 2026.9.10, 2026-10-14: search(r'\b(?:cat|dog)', 'xcat', pos=1) -> None; 'x cat', pos=2 -> (2, 5); "
            + @"search(r'\b(?:cat|dog)\b', 'cats', endpos=3) -> (0, 3); search(r'(?m)^(?:cat|dog)', 'xcat\ncat', pos=1) -> (5, 8); "
            + @"search(r'\b(?:cat|dog)', 'xx ca', partial=True) -> (3, 5) partial; finditer(r'\b(?:aba|ab)', 'aba ab', overlapped=True) -> (0, 3), (4, 6)"
    )]
    public void A_zero_width_test_in_front_keeps_its_answers_at_the_slice_edges()
    {
        var front = new FuzzyRegex(@"\b(?:cat|dog)");
        Match partial = front.Match("xx ca", partial: true);

        using (new AssertionScope())
        {
            front.Match("xcat", 1).Success.Should().BeFalse();
            front.Match("x cat", 2).Index.Should().Be(2);
            new FuzzyRegex(@"\b(?:cat|dog)\b").Match("cats", 0, 3).Length.Should().Be(3);
            new FuzzyRegex(@"(?m)^(?:cat|dog)").Match("xcat\ncat", 1).Index.Should().Be(5);
            (partial.Index, partial.Length, partial.PartialMatch).Should().Be((3, 2, true));
            new FuzzyRegex(@"\b(?:aba|ab)")
                .Matches("aba ab", overlapped: true)
                .Select(static m => (m.Index, m.Index + m.Length))
                .Should()
                .Equal((0, 3), (4, 6));
        }
    }

    /// <summary>
    /// A lookaround in front, positive or negative, ahead or behind, alone or beside a word
    /// boundary: the walk goes on from the lookaround's continuation (<c>Next2</c>).
    /// </summary>
    /// <param name="pattern">A pattern opening with a lookaround and then literals.</param>
    [Test]
    [Arguments(@"(?<![a-z])(?:cat|dog)")]
    [Arguments(@"(?<=\s)(?:cat|dog)")]
    [Arguments(@"(?=[cd])(?:cat|dog)")]
    [Arguments(@"(?!x)(?:cat|dog)")]
    [Arguments(@"(?<!\w)(?!\d)(?:cat|dog)")]
    [Arguments(@"\b(?<!-)(?:cat|dog)\b")]
    [Arguments(@"(?<![a-z])\L<w>(?![a-z])")]
    [Arguments("(?fi)(?<=a)bc|de")]
    public void A_lookaround_in_front_of_the_literals_is_looked_through(string pattern)
    {
        PatternObject compiled = Compile(pattern, FuzzyRegexOptions.None);

        (compiled.LeadingStrings ?? (object?)compiled.LeadingUnits).Should().NotBeNull();
    }

    /// <summary>
    /// The lookarounds the walk refuses. A <c>\K</c> in the body could move the next attempt's
    /// start (<c>PatternObject.AnAttemptCanMoveTheNextStart</c>): upstream's attempt at 0 over
    /// <c>xcat</c> sets the match start to 2 and fails, so its next attempt is at 3 and the one at
    /// 1, where <c>cat</c> starts, never runs. Since D88 (ledger entry 86) the failed attempt puts
    /// the start back and the search finds (3, 4), the answer a screen would give, so the guard is
    /// now only conservative for this row. A verb or a group call in the body is refused as
    /// unproved.
    /// </summary>
    /// <param name="pattern">A pattern the screen must leave alone.</param>
    [Test]
    [Arguments(@"(?=[a-z][a-z]\K)(?:cat|dog)")]
    [Arguments(@"(?=c(*PRUNE)a|d)(?:cat|dog)")]
    [Arguments(@"(?=(?&w))(?:cat|dog)(?<w>c)?")]
    public void The_walk_refuses_a_lookaround_that_can_move_the_next_start_or_is_unproved(string pattern)
    {
        PatternObject compiled = Compile(pattern, FuzzyRegexOptions.None);

        compiled.LeadingStrings.Should().BeNull();
    }

    /// <summary>
    /// Every match behind a lookaround, in UTF-16 units. The <c>\K</c> rows answered <c>""</c> and
    /// <c>7-8</c> before D88, as upstream 2026.9.10 does, which skips the start where <c>cat</c>
    /// begins; they now find it.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="options">The options.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Every match's span, <c>start-end</c>, space-separated.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer, 2026-10-14 (.scratch/c6/up2.py)")]
    [Arguments(@"(?<![a-z])(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat -dog dogs", "5-8 10-13 14-17")]
    [Arguments(@"(?<=\s)(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat -dog dogs", "5-8 14-17")]
    [Arguments(@"(?=[cd])(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat -dog", "1-4 5-8 10-13")]
    [Arguments(@"(?!cats)(?:cat|dog)", FuzzyRegexOptions.None, "cats cat dogs", "5-8 9-12")]
    [Arguments(@"(?<!\w)(?!\d)(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat 1dog dog", "5-8 14-17")]
    [Arguments(@"\b(?<!-)(?:cat|dog)\b", FuzzyRegexOptions.None, "xcat cat -dog dog", "5-8 14-17")]
    [Arguments(@"(?<![a-z])(?:cat|dog)", FuzzyRegexOptions.IgnoreCase, "xCAT Cat -dOg", "5-8 10-13")]
    [Arguments(@"(?=[a-z][a-z]\K)(?:cat|dog)", FuzzyRegexOptions.None, "xcat", "3-4")]
    [Arguments(@"(?=[a-z][a-z]\K)(?:cat|dog)", FuzzyRegexOptions.None, "xcat dog", "3-4 7-8")]
    [Arguments(@"(?=c(*PRUNE)a|d)(?:cat|dog)", FuzzyRegexOptions.None, "xcat cot dog", "1-4 9-12")]
    [Arguments(@"(?<=(\w))(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat", "1-4")]
    [Arguments(@"(?=(\w))(?:cat|dog)", FuzzyRegexOptions.None, "xcat cat", "1-4 5-8")]
    public void A_lookaround_in_front_still_finds_every_match(
        string pattern,
        FuzzyRegexOptions options,
        string subject,
        string expected
    )
    {
        Spans(pattern, options, subject).Should().Be(expected);
    }

    [Test]
    [Property(
        "Upstream",
        @"regex 2026.9.10, 2026-10-14: finditer(r'(?<![a-z])\L<w>(?![a-z])', 'amberx amber copper xviolet violet', w=[amber, copper, violet]) -> (7, 12), (13, 19), (28, 34); "
            + @"search(r'(?<=x)(?:cat|dog)', 'xcat', pos=1) -> (1, 4), pos=2 -> None; search(r'(?=cat\b)(?:cat|dog)', 'cats', endpos=3) -> (0, 3)"
    )]
    public void A_lookaround_in_front_keeps_its_answers_at_the_slice_edges()
    {
        var behind = new FuzzyRegex(@"(?<=x)(?:cat|dog)");

        using (new AssertionScope())
        {
            Spans(@"(?<![a-z])\L<w>(?![a-z])", FuzzyRegexOptions.None, "amberx amber copper xviolet violet")
                .Should()
                .Be("7-12 13-19 28-34");
            behind.Match("xcat", 1).Index.Should().Be(1);
            behind.Match("xcat", 2).Success.Should().BeFalse();
            new FuzzyRegex(@"(?=cat\b)(?:cat|dog)").Match("cats", 0, 3).Length.Should().Be(3);
        }
    }

    /// <summary>
    /// A search the leading screen moves from literal to literal does not also run the first-set
    /// prefilter (<c>Matcher.SearchStart</c>). Every literal starts with a character of the first
    /// set, so the prefilter could only stop where the screen already has; and once its first-set
    /// scan had passed a literal it never came back to the screen: <c>(?m)^(?:cat|dog)</c> over a
    /// megabyte with no newline made one attempt, in which the prefilter tried <c>^</c> at all
    /// 50,094 <c>c</c> and <c>d</c> (<c>.scratch/c6/ss/trace</c>). The control is the same search
    /// spelt with a class, which gets no screen and keeps the prefilter, so the counter is live;
    /// both find the same matches.
    /// </summary>
    /// <param name="pattern">A pattern the leading screen applies to.</param>
    /// <param name="control">The same matches, spelt so that no screen applies.</param>
    /// <param name="options">The options.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?m)^(?:cat|dog)", @"(?m)^[cd](?:at|og)", FuzzyRegexOptions.None)]
    [Arguments(@"\b(?:cat|dog)\b", @"\b[cd](?:at|og)\b", FuzzyRegexOptions.None)]
    [Arguments(@"(?<![a-z])(?:cat|dog)", @"(?<![a-z])[cd](?:at|og)", FuzzyRegexOptions.None)]
    [Arguments(@"(?:cat|dog)", @"[cd](?:at|og)", FuzzyRegexOptions.None)]
    [Arguments(@"\b(?:cat|dog)\b", @"\b[cd](?:at|og)\b", FuzzyRegexOptions.IgnoreCase)]
    [Arguments(@"(?m)^(?:cat|dog)", @"(?m)^[cd](?:at|og)", FuzzyRegexOptions.IgnoreCase)]
    public void A_screened_search_does_not_also_run_the_first_set_prefilter(
        string pattern,
        string control,
        FuzzyRegexOptions options
    )
    {
        EngineWork.SkipUnlessCounting();
        const string subject = "xcat\ncat catx\ndog\n dog Dog.cAT dcx";
        var screened = new FuzzyRegex(pattern, options, EngineWork.HangGuard);
        var unscreened = new FuzzyRegex(control, options, EngineWork.HangGuard);

        using (new AssertionScope())
        {
            unscreened.PatternObject.LeadingStrings.Should().BeNull();
            unscreened.PatternObject.LeadingUnits.Should().BeNull();
            (screened.PatternObject.LeadingStrings ?? (object?)screened.PatternObject.LeadingUnits)
                .Should()
                .NotBeNull();
            SearchStartCallsBy(() => unscreened.Count(subject)).Should().BePositive("the control keeps the prefilter");
            SearchStartCallsBy(() => screened.Count(subject)).Should().Be(0, "the screen has done its work");
            Spans(pattern, options, subject).Should().Be(Spans(control, options, subject));
        }
    }

    private static long SearchStartCallsBy(Action action)
    {
        long before = WorkCounter.SearchStartCalls;
        action();
        return WorkCounter.SearchStartCalls - before;
    }

    private static PatternObject Compile(string pattern, FuzzyRegexOptions options) =>
        pattern.Contains(@"\L<w>", StringComparison.Ordinal)
            ? new FuzzyRegex(pattern, options, _words).PatternObject
            : new FuzzyRegex(pattern, options).PatternObject;

    private static string Spans(string pattern, FuzzyRegexOptions options, string subject)
    {
        FuzzyRegex regex = pattern.Contains(@"\L<w>", StringComparison.Ordinal)
            ? new FuzzyRegex(pattern, options, _words)
            : new FuzzyRegex(pattern, options);

        return string.Join(' ', regex.Matches(subject).Select(static m => $"{m.Index}-{m.Index + m.Length}"));
    }
}
