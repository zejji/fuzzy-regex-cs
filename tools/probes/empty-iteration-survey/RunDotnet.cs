// System.Text.RegularExpressions battery runner for the empty-iteration survey.
// Run: dotnet run RunDotnet.cs -p:UseSharedCompilation=false -- <this directory>
using System.Text.RegularExpressions;

var lines = await File.ReadAllLinesAsync(Path.Combine(args[0], "battery.tsv")).ConfigureAwait(false);
foreach (var opts in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
{
    foreach (var line in lines)
    {
        if (line.Length == 0 || line[0] == '#')
        {
            continue;
        }

        var f = line.Split('\t');
        var m = new Regex(f[1], opts, TimeSpan.FromSeconds(5)).Match(f[2]);
        var g1 = m.Groups[1].Success ? $"'{m.Groups[1].Value}'" : "unset";
        var r = m.Success ? $"span={m.Index},{m.Index + m.Length} g1={g1}" : "nomatch";
        Console.WriteLine($".NET {Environment.Version} {opts}\t{f[0]}\t{r}");
    }
}
