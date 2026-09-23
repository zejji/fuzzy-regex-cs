using System.Text.RegularExpressions;

// Does the built-in Regex time a lazy walk per match, or across the walk?
// Each match is instant; the caller spins for 300 ms between steps; the budget is 100 ms.
// Run: dotnet run tools/probes/bcl-lazy-walk-timeout.cs
// 2026-09-22, .NET 10.0.12: all three walks finish five matches with no timeout, so each
// times one match at a time and not the walk. Cited by docs/DIVERGENCES.md (S61).
var regex = new Regex(@"\w+", RegexOptions.None, TimeSpan.FromMilliseconds(100));
string subject = "a b c d e";

int spanCount = 0;
foreach (ValueMatch _ in regex.EnumerateMatches(subject))
{
    spanCount++;
    Busy();
}
Console.WriteLine($"EnumerateMatches(span): {spanCount} matches, no timeout");

int nextCount = 0;
for (Match m = regex.Match(subject); m.Success; m = m.NextMatch())
{
    nextCount++;
    Busy();
}
Console.WriteLine($"Match.NextMatch walk: {nextCount} matches, no timeout");

int lazyCount = 0;
foreach (Match _ in regex.Matches(subject))
{
    lazyCount++;
    Busy();
}
Console.WriteLine($"Matches (lazy MatchCollection): {lazyCount} matches, no timeout");
Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

static void Busy()
{
    var clock = System.Diagnostics.Stopwatch.StartNew();
    while (clock.ElapsedMilliseconds < 300)
    {
        Thread.SpinWait(1000);
    }
}
