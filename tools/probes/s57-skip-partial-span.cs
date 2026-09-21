#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S57: the one red row of the Phase 6 exit gate's extra wave - `interactions` seed 99991 row 525,
// and the same shape at row 225 of the 6000-row wave.
//
//     (?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})
//     subject "\r\n\U0001F600", search, partial
//
// upstream regex 2026.9.10 answers a partial of (0,2); this port answers a partial of (0,1).
//
// The isolating probe is the verb: upstream's own answer drops to (0,1) the moment `(*SKIP)` is
// spelled `(*PRUNE)` - the same pruning with no bound moved - or removed altogether, so the second
// code unit of its span comes from the verb having moved the slice bound. Upstream's own
// `match(0, 1, partial=True)` answers (0,1) too, and is None at endpos 2 and 3, so the highest
// anchor a reversed search reaches names this port's answer as well. Measured 2026-09-20, the
// upstream half by tools/probes/upstream-partial-retry-reversed-anchored.py:
//
//     regex 2026.9.10   as drawn (0,2)   (*PRUNE) (0,1)   no SKIP (0,1)   no fuzzy (0,1)
//     this port         as drawn (0,1)   (*PRUNE) (0,1)   no SKIP (0,1)   no fuzzy (0,1)
//
// All partial. Spans are codepoints upstream and UTF-16 here; they coincide on this subject's
// prefix, whose astral character is past every span but the forward twin's.
//
// This port restores both slice bounds after a verb moves them (S40b, S48), where upstream restores
// only the text position, so a partial it reports cannot be measured against a moved bound.

using Fuzzy.Text.RegularExpressions;

string subject = "\r\n\U0001F600";

Show("as drawn", @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})");
Show("PRUNE", @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*PRUNE)\s|\p{Nd})");
Show("no SKIP", @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f]\s|\p{Nd})");
Show("no fuzzy", @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]]))(?:[a-f](*SKIP)\s|\p{Nd})");
Show("no anchor", @"(?r)(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})");
Show("forward", @"^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})");

void Show(string label, string pattern)
{
    FuzzyRegex re = new(pattern, FuzzyRegexOptions.Version0);
    Match m = re.Match(subject, partial: true);

    // PadRight rather than an interpolation alignment specifier: CSharpier writes those with a
    // space after the comma and IDE0055 rejects the space, so a line using one satisfies neither of
    // this repo's formatting gates (the same note is on tools/probes/aot-smoke-slow-patterns.cs).
    Console.WriteLine(
        label.PadRight(10)
            + (m.Success ? $" span=({m.Index},{m.Index + m.Length}) partial={m.PartialMatch}" : " no match")
    );
}
