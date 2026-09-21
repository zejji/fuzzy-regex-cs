#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S57b: this port's half of seed 20260921 row 72790, the row sitting 3 left open because neither
// engine's change list for it was obviously the right one.
//
//     dotnet run tools/probes/s57b-row72790-port-changes-in-a-scan.cs
//
// The upstream half is tools/probes/s57b-row72790-change-order.py, which un-shifts both engines'
// deletion lists back to the positions they were recorded at and shows upstream's own two other
// spellings of the minimised row answering what this port answers. Read the two together.
//
// WHAT SITTING 3 SUSPECTED, AND WHAT KILLED IT. Row 72790's second match answers [3, 5, 6] here
// and [4, 5, 5] upstream, and this port's list shares its last two positions with the PREVIOUS
// match of the same scan - the shape of a change list carried over from one match into the next.
// It is not one. The first block below asks the same match three ways: inside the whole scan,
// where it is the second match drawn, by `Matches`, which holds one engine state for the walk, and
// by `EnumerateMatches`, whose state restarts per match; then alone; then as the FIRST match of a
// fresh scan over the prefix that ends where it does. The scan and the fresh scan differ, but the
// fresh scan is a different question - truncating the subject starves the lookahead, which reads
// past the match - and `Matches` and `EnumerateMatches` agree, so no state crosses a match here.
// The other half of the same test is in the notes: removing this port's own change-list clear
// (Matcher.cs:5155) reproduces upstream's leaked answer on ledger entry 11's own row and does not
// move this one by a single position, so the leak family is not the mechanism either.
//
// WHAT IS LEFT is where a change made OUTSIDE a fuzzy lookahead is recorded. The second block is
// the minimised row and its two controls, where upstream contradicts itself and this port does
// not: all three spellings answer the same zero-width match at 0 with the same counts, the
// lookahead deleting `\s` at 1 and the body deleting `b` at 0, and upstream moves the body's
// deletion to 1 for the spelling that carries a minimum error count.
//
// Measured 2026-09-21.
using Fuzzy.Text.RegularExpressions;

// Row 72790 verbatim out of TestResults/oracle/wave-20260921.jsonl: flags=0x2 is IGNORECASE.
const string Pattern = @"(?r)(?=(?:[^\d]?\sß){1<=e<=2})\L<w1>{d<=1}";
const string Subject = "ßß\r\n";

Dictionary<string, IReadOnlyCollection<string>> lists = new() { ["w1"] = ["ß", "İﬁİ"] };

FuzzyRegex row = new(Pattern, (FuzzyRegexOptions)0x2, lists);

static string Show(Match m)
{
    if (!m.Success)
    {
        return "NO MATCH";
    }

    FuzzyChanges changes = m.FuzzyChanges;

    // The raw position each deletion was recorded at, undoing the running shift
    // `match_fuzzy_changes` adds (upstream/src/_regex.c:20504-20560, Match.SplitFuzzyChanges).
    // Printed because the judgement compares the two engines' RAW lists: on row 72790 they are the
    // same three deletions in a different order.
    IEnumerable<int> raw = changes.Deletions.Select(static (position, index) => position - index);

    return $"({m.Index},{m.Length}) fuzzy={m.FuzzyCounts} "
        + $"[s:{string.Join(',', changes.Substitutions)}]"
        + $"[i:{string.Join(',', changes.Insertions)}]"
        + $"[d:{string.Join(',', changes.Deletions)}]"
        + $" raw deletions [{string.Join(',', raw)}]";
}

static void Walk(string label, IEnumerable<Match> matches)
{
    int index = 0;
    foreach (Match m in matches)
    {
        Console.WriteLine($"    {label} match {index++, -2} {Show(m)}");
    }
}

Console.WriteLine("THE WHOLE SCAN, where (3, 3) is the second match");
Walk("Matches         ", row.Matches(Subject));
Walk("EnumerateMatches", row.EnumerateMatches(Subject));

Console.WriteLine();
Console.WriteLine("THE (3, 3) MATCH ALONE, over the prefix that ends where it does");
Console.WriteLine($"    Match(input, 0, 3)        {Show(row.Match(Subject, 0, 3))}");
Console.WriteLine($"    FullMatch(input, 3, 0)    {Show(row.FullMatch(Subject, 3, 0))}");

Console.WriteLine();
Console.WriteLine("A FRESH SCAN over that prefix, where the same match has no predecessor");
Walk("Matches(0,3)    ", row.Matches(Subject, 0, 3));

Console.WriteLine();
Console.WriteLine("THE MINIMISED ROW AND ITS TWO CONTROLS, all over 'a'");
foreach (string section in new[] { "{1<=e<=2}", "{d<=1}", "{e<=1}" })
{
    string pattern = $@"(?r)(?=(?:a\s){section})b{{d<=1}}";
    Walk($"{pattern, -32}", new FuzzyRegex(pattern).Matches("a"));
}
