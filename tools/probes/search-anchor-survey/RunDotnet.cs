// System.Text.RegularExpressions side of the \G survey.
// Run: dotnet run RunDotnet.cs -p:UseSharedCompilation=false -- <this directory>
using System.Text.RegularExpressions;

var lines = await File.ReadAllLinesAsync(Path.Combine(args[0], "battery.tsv")).ConfigureAwait(false);
foreach (var line in lines)
{
    if (line.Length == 0 || line[0] == '#')
    {
        continue;
    }

    var f = line.Split('\t');
    var rtl = string.Equals(f[3], "-", StringComparison.Ordinal);
    var opts = rtl ? RegexOptions.RightToLeft : RegexOptions.None;
    var start = rtl ? f[2].Length : int.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
    var m = new Regex(f[1], opts, TimeSpan.FromSeconds(5)).Match(f[2], start);
    Console.WriteLine(
        $".NET {Environment.Version}\t{f[0]}\t{(m.Success ? $"({m.Index}, {m.Index + m.Length})" : "None")}"
    );
}
