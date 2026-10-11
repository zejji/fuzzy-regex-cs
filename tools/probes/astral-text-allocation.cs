#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// Does a non-fuzzy search allocate in proportion to the text when the text holds a character
// outside the BMP? S61's second review pass measured 416 B for `\w+`, `cat`, `x` and `(\w)\1` over
// "cat 😀 " x 200 against 0 B over ASCII, and left it for an optimisation slice (S61 notes).
// Run: dotnet run tools/probes/astral-text-allocation.cs -c Release
//
// Each row is warmed first, then measured with GC.GetAllocatedBytesForCurrentThread over one call.
//
// Measured 2026-09-25 on maint/state-findings (Release, .NET 10), one Count call:
//
//   pattern            ascii x200     astral x200    astral x2000
//   \w+                       0 B           416 B          3344 B
//   cat                       0 B           416 B          3344 B
//   x                         0 B           416 B          3344 B
//   (\w)\1                    0 B           416 B          3344 B
//   (?p)\w+                9600 B          5216 B         51344 B
//   (?:cat){e<=1}             0 B             0 B             0 B
//
// So every non-fuzzy pattern pays about 1.6 B per repetition once the text holds an astral
// character, whatever the pattern, and a fuzzy pattern pays nothing: the cost is in the non-fuzzy
// search path, not in matching. `(?p)` allocates on ASCII text too, a separate cost.
using Fuzzy.Text.RegularExpressions;

string[] patterns = [@"\w+", "cat", "x", @"(\w)\1", "(?p)\\w+", "(?:cat){e<=1}"];
(string Label, string Text)[] texts =
[
    ("ascii x200", string.Concat(Enumerable.Repeat("cat a ", 200))),
    ("astral x200", string.Concat(Enumerable.Repeat("cat \U0001F600 ", 200))),
    ("astral x2000", string.Concat(Enumerable.Repeat("cat \U0001F600 ", 2000))),
];

Console.WriteLine("pattern".PadRight(16) + string.Concat(texts.Select(static t => t.Label.PadLeft(16))));
foreach (string pattern in patterns)
{
    var re = new FuzzyRegex(pattern);
    var line = new System.Text.StringBuilder(pattern.PadRight(16));
    foreach ((string _, string text) in texts)
    {
        for (int i = 0; i < 20; i++)
        {
            _ = re.Count(text);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = re.Count(text);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        line.Append((bytes + " B").PadLeft(16));
    }

    Console.WriteLine(line);
}
