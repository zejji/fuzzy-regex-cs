using System.Globalization;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// No call does work in proportion to the whole subject when its match is short: the surrogate
/// check behind the character-count conversions looks only as far as the queries reach, and
/// <c>Replace</c>'s width shortcut counts codepoints only as far as it needs to (D83).
/// </summary>
/// <remarks>
/// Counts, not timings: <see cref="Fuzzy.Text.RegularExpressions.Engine.WorkCounter.SubjectUnitsScanned"/>
/// is the number of code units examined for either reason. Before D83 every match state scanned the
/// whole subject for a high surrogate when it was set up, so a <c>Match</c> that found a word at
/// position 0 of a megabyte cost a megabyte, and <c>Replace</c> counted every codepoint of its input.
/// </remarks>
public sealed class SubjectScanTests
{
    /// <summary>The bound for a call whose matches sit in the first few dozen units.</summary>
    private const long _local = 2_048;

    private static readonly string _long = Repeat("a needle in a haystack. ", 1 << 20);

    private static string Repeat(string unit, int length)
    {
        var builder = new System.Text.StringBuilder(length + unit.Length);
        while (builder.Length < length)
        {
            _ = builder.Append(unit);
        }

        return builder.ToString(0, length);
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_match_near_the_start_of_a_long_subject_scans_only_around_it()
    {
        var words = new FuzzyRegex(@"\w+");

        EngineWork.ShouldScanAtMostUnits(
            () => words.Match(_long).Value.Should().Be("a"),
            _local,
            "a one-letter match at position 0 needs nothing of the megabyte after it"
        );
        EngineWork.ShouldScanAtMostUnits(
            () => words.IsMatch(_long).Should().BeTrue(),
            _local,
            "IsMatch is the same search"
        );
        EngineWork.ShouldScanAtMostUnits(
            () => words.EnumerateMatches(_long).Take(2).Last().Value.Should().Be("needle"),
            _local,
            "the first two matches of a lazy walk are a dozen units in"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_reverse_match_near_the_end_of_a_long_subject_scans_only_around_it()
    {
        var words = new FuzzyRegex(@"(?r)\w+");

        EngineWork.ShouldScanAtMostUnits(
            () => words.Match(_long).Index.Should().BeGreaterThan(_long.Length - 30),
            _local,
            "a reverse search's queries start at the end of the slice and stay there"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_lazy_walk_to_the_end_scans_each_unit_about_once()
    {
        // Already true before D83 for a walk on one state; this keeps it true now that the knowledge
        // grows with the walk instead of being complete from the start. NextMatch builds a state per
        // step and must hand on what the last one learnt, or the walk scans the subject per match.
        string subject = Repeat("ab cd ", 6 * 10_000);
        var words = new FuzzyRegex(@"\w+");

        EngineWork.ShouldScanAtMostUnits(
            () => words.EnumerateMatches(subject).Count().Should().Be(subject.Length / 3),
            subject.Length + _local,
            "one state's knowledge only grows"
        );
        EngineWork.ShouldScanAtMostUnits(
            () =>
            {
                int count = 0;
                for (Match match = words.Match(subject); match.Success; match = match.NextMatch())
                {
                    count++;
                }

                count.Should().Be(subject.Length / 3);
            },
            subject.Length + _local,
            "NextMatch hands the knowledge on to the next step's state"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_lazy_repeat_bounded_by_a_count_scans_only_its_reach()
    {
        // A bounded lazy repeat compares its count with the characters left in the slice, capped at
        // its maximum: the cap makes the question local.
        var lazy = new FuzzyRegex(@"a.{0,5}?d");

        EngineWork.ShouldScanAtMostUnits(
            () => lazy.Match(_long).Value.Should().Be("a need"),
            _local,
            "at most five characters are ever asked about"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    public void A_replace_of_the_first_match_does_no_whole_input_codepoint_count()
    {
        var needle = new FuzzyRegex("needle");

        EngineWork.ShouldScanAtMostUnits(
            () => needle.Replace(_long, "pin", count: 1).Should().StartWith("a pin in a haystack. a needle"),
            _local,
            "the width shortcut is decided by the input's length in code units alone"
        );
    }

    /// <summary>
    /// Builds a subject from a row's spec: <c>|</c>-separated pieces, each either text or
    /// <c>count*text</c>, with <c>~P</c> for the pair U+1F600 and <c>~H</c>/<c>~L</c> for its two
    /// halves standing alone (an attribute cannot carry a lone surrogate through its metadata).
    /// </summary>
    private static string Expand(string spec) =>
        string.Concat(
            spec.Split('|')
                .Select(static piece =>
                {
                    int star = piece.IndexOf('*', StringComparison.Ordinal);
                    string text = Tokens(star < 0 ? piece : piece[(star + 1)..]);
                    return star < 0
                        ? text
                        : string.Concat(
                            Enumerable.Repeat(text, int.Parse(piece[..star], CultureInfo.InvariantCulture))
                        );
                })
        );

    private static string Tokens(string text) =>
        text.Replace("~P", "\U0001F600", StringComparison.Ordinal)
            .Replace("~H", "\uD83D", StringComparison.Ordinal)
            .Replace("~L", "\uDE00", StringComparison.Ordinal);

    private static string Span(Match match) =>
        $"{match.Index},{match.Index + match.Length}" + (match.PartialMatch ? ",partial" : "");

    [Test]
    [Arguments("\\w+", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "0,602")]
    [Arguments("\\w+", "ab|600*c|~P|x|10*d", "findall", 0, -1, false, "0,602;604,615")]
    [Arguments("c.+x", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "2,605")]
    [Arguments("c.*?x", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "2,605")]
    [Arguments("c.{600}x", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "2,605")]
    [Arguments("c.{599}x", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "3,605")]
    [Arguments("(?r)\\w+", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "604,615")]
    [Arguments("(?r)c.+", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "2,615")]
    [Arguments(
        "(?r).{3}",
        "ab|600*c|~P|x|10*d",
        "findall",
        590,
        25,
        false,
        "612,615;609,612;606,609;602,606;599,602;596,599;593,596;590,593"
    )]
    [Arguments(".+x", "255*a|~P|x", "match", 0, -1, false, "0,258")]
    [Arguments(".+x", "256*a|~P|x", "match", 0, -1, false, "0,259")]
    [Arguments(".+x", "257*a|~P|x", "match", 0, -1, false, "0,260")]
    [Arguments(".+?x", "256*a|~P|x", "match", 0, -1, false, "0,259")]
    [Arguments(".{256}", "255*a|~P|x", "match", 0, -1, false, "0,257")]
    [Arguments(".{257}x", "255*a|~P|x", "match", 0, -1, false, "none")]
    [Arguments("a{1,600}?~P", "511*a|~P", "match", 0, -1, false, "0,513")]
    [Arguments("a{1,600}?~P", "512*a|~P", "match", 0, -1, false, "0,514")]
    [Arguments("a{1,600}?~P", "513*a|~P", "match", 0, -1, false, "0,515")]
    [Arguments("(?r).+a", "a|~P|256*b", "search", 0, -1, false, "none")]
    [Arguments("(?r).{257}", "a|~P|256*b", "search", 0, -1, false, "1,259")]
    [Arguments(".+x", "300*a|~H|bx", "match", 0, -1, false, "0,303")]
    [Arguments(".{301}x", "300*a|~H|bx", "match", 0, -1, false, "none")]
    [Arguments(".+x", "300*a|~L|bx", "match", 0, -1, false, "0,303")]
    [Arguments("(?r).+", "300*a|~L|b", "search", 0, -1, false, "0,302")]
    [Arguments(".{2}", "a|~H|~H|~P|b", "findall", 0, -1, false, "0,2;2,5")]
    [Arguments(".+", "10*a|~P|10*b", "search", 11, -1, false, "11,22")]
    [Arguments(".+", "10*a|~P|10*b", "search", 0, 11, false, "0,11")]
    [Arguments("(?r).+", "10*a|~P|10*b", "search", 11, -1, false, "11,22")]
    [Arguments("(?r).+", "10*a|~P|10*b", "search", 0, 11, false, "0,11")]
    [Arguments(".{2,}?b", "10*a|~P|10*b", "search", 11, -1, false, "11,14")]
    [Arguments(".", "3*a|~P|3*b", "findall", 4, 3, false, "4,5;5,6;6,7")]
    [Arguments(".{5,}x", "aa|~P", "search", 0, -1, true, "0,4,partial")]
    [Arguments("a.*?z", "300*a|~P|b", "search", 0, -1, true, "0,303,partial")]
    [Arguments("(?r)z.{3}", "~P|aa", "search", 0, -1, true, "0,4,partial")]
    [Arguments(".{300}", "290*a|~P|3*b", "match", 0, -1, true, "0,295,partial")]
    [Arguments("(?:cx){e<=1}", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "2,4")]
    [Arguments("(?:c~Px){e<=1}", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "600,605")]
    [Arguments("(?b)(?:c.x){e<=2}", "ab|600*c|~P|x|10*d", "search", 0, -1, false, "601,605")]
    [Arguments("(?e)(?:d{3}){e<=1}", "~P|dd|~P|ddd", "search", 0, -1, false, "0,4")]
    [Arguments("(?:\\w{3}){s<=1}", "a|~P|bc", "findall", 0, -1, false, "0,4")]
    [Arguments("a+.", "300*a|~P|~P", "match", 0, -1, false, "0,302")]
    [Arguments("[a~P]+", "300*a|~P|~P|b", "match", 0, -1, false, "0,304")]
    [Arguments("[a~P]+?b", "300*a|~P|~P|b", "match", 0, -1, false, "0,305")]
    [Arguments(".{298,302}?b", "300*a|~P|~P|b", "match", 0, -1, false, "0,305")]
    [Arguments("(?r)[a~P]+", "b|~P|~P|300*a", "search", 0, -1, false, "1,305")]
    [Arguments("\\W*?~Pb", "300* |~P|~P|b", "search", 0, -1, false, "0,305")]
    public void A_conversion_answers_what_upstream_answers_wherever_the_surrogates_sit(
        string pattern,
        string spec,
        string operation,
        int beginning,
        int length,
        bool partial,
        string expected
    )
    {
        // Rows chosen to put the first surrogate far after the first match, at the edges of the
        // 256-unit scan chunk, as lone halves, cut by a slice bound, under reverse, partial and
        // fuzzy matching, and in the path of a single-character repeat. Every expected value is
        // upstream's (regex 2026.9.10, measured 2026-10-11) over the Python str the UTF-16 subject
        // and its slice denote, a pair cut by a bound being two lone surrogates there, with the
        // codepoint spans mapped back to UTF-16 indices.
        var regex = new FuzzyRegex(Tokens(pattern), FuzzyRegexOptions.None, TimeSpan.FromSeconds(10));
        string subject = Expand(spec);

        string actual = operation switch
        {
            "search" => regex.Match(subject, beginning, length, partial) is { Success: true } found
                ? Span(found)
                : "none",
            "match" => regex.MatchAtStart(subject, beginning, length, partial) is { Success: true } found
                ? Span(found)
                : "none",
            _ => string.Join(';', regex.EnumerateMatches(subject, beginning, length, partial: partial).Select(Span))
                is { Length: > 0 } all
                ? all
                : "none",
        };

        actual.Should().Be(expected);
    }

    [Test]
    [Arguments("~P~P", false)]
    [Arguments("a~P", false)]
    [Arguments("~P~P~P", true)]
    [Arguments("~Paa", true)]
    [Arguments("aaa", true)]
    public void The_width_shortcut_still_counts_codepoints_where_the_length_does_not_settle_it(
        string spec,
        bool compiles
    )
    {
        // Upstream, regex 2026.9.10, measured 2026-10-11: regex.subn('...', r'\g<bad', s) is
        // (s, 0) for the first two - fewer than three codepoints, so the template is never compiled
        // - and raises for the rest. Three to five units is the band a width of three leaves
        // undecided; six units settle it from the length alone.
        string subject = Expand(spec);
        Func<string> replace = () => new FuzzyRegex("...").Replace(subject, @"\g<bad");

        if (compiles)
        {
            replace.Should().Throw<FuzzyRegexParseException>();
        }
        else
        {
            replace().Should().Be(subject);
        }
    }
}
