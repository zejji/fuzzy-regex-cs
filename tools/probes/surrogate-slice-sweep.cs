#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
using System.Text;
using Fuzzy.Text.RegularExpressions;
using Answer = (string Text, System.Collections.Generic.List<(int Start, int End)> Spans, bool Error);

// What does every call answer when `beginning` or `beginning + length` falls between the two halves
// of a surrogate pair? Every such slice of seven subjects, 92 patterns forwards and reversed, eleven
// calls each (search, anchored, full, their partial forms, finditer plain, overlapped and partial,
// Count, Replace). A row is flagged for a timeout, hang or exception; a span outside the slice; a
// FullMatch that is not the slice; an anchored match off its edge; or an answer that differs from
// the one for the same subject with the cut-off half replaced by U+0001, a BMP Control character
// that, like a lone surrogate, is not a word character. That last check is the rule that a cut pair
// leaves two lone surrogates (DECISIONS 2026-09-26).
//
// Run: dotnet run tools/probes/surrogate-slice-sweep.cs -c Release -- out.tsv [--all] [--atom N]
//      [--timeout-ms 2000] [--hang-s 8]
// --all writes every row, which tools/probes/surrogate-slice-oracle.py then answers with upstream.
// A hang that the match timeout cannot stop leaves a thread spinning, so the run gives up after 20.
//
// Measured 2026-09-26, regex 2026.9.10 as the oracle:
//   main de9e80c (91 patterns, 136,136 cases, 300 ms budget, one process per pattern): 14,756 rows
//     flagged - 860 timeouts, 12 hangs no timeout stopped, 4 IndexOutOfRangeExceptions, 5,178
//     spans outside the slice, and every pattern differing from its U+0001 twin somewhere.
//   the fix (137,632 cases, 2 s budget): 0 rows flagged. The oracle differs on 9,410 rows, every
//     one of them identical on the U+0001 text, so none is about the cut: ledger 4 (a reversed
//     full match on a slice) and the partial-match rules.
string[] atoms =
[
    @".",
    @"(?:.)*x",
    @".*x",
    @".*?x",
    @"(?:.)*?x",
    @"(?:.)+",
    @".+?",
    @"(?:.)*",
    @"(.)*x",
    @"(?:(.))*x",
    @"(?:..)*x",
    @"(?:.){0,5}x",
    @".{2}",
    @".{1,3}x",
    @"(?>.*)x",
    @"(?:.)*+x",
    @"(?:.|x)*x",
    @"\X",
    @"\X*x",
    @"(?:\X)*x",
    @"[^a]",
    @"[^a]*x",
    @"(?:[^\n])*x",
    @"\w*",
    @"\W",
    @"\W+x",
    @"\S+",
    @"\s*",
    @"\b",
    @"\B",
    @"\b.",
    @".\b",
    @"(?w)\b",
    @"(?w)\b.",
    @"\U0001F600",
    @"\U0001F600+",
    @"\U00010428",
    @"\U00010428+x",
    @"x",
    @"c",
    @"[xc]+",
    @"(?i)X",
    @"(?i).*C",
    @"(.)\1",
    @"(.)(?:.)*\1",
    @"(.)*\1",
    @"(?<=.).",
    @"(?<=\W).",
    @"(?<!a).",
    @"(?<=\U0001F600).",
    @"(?<=\U00010428).",
    @"(?<=^.)x",
    @"(?<=.)",
    @"(?=.)",
    @"(?:.(?=x))",
    @"(?=.*x).",
    @"(?:ab){e<=1}",
    @"(?:x){e<=1}",
    @"(?:xc){e<=2}",
    @"(?:.x){e<=1}",
    @"(?:xc){e<=1}x?",
    @"(?:\U0001F600b){e<=1}",
    @"(?b)(?:xc){e<=1}",
    @"(?e)(?:xc){e<=2}",
    @"(?:x){i<=1}",
    @"(?:.x){s<=1}",
    @"^.",
    @"\A.",
    @".$",
    @".\Z",
    @"\G.",
    @"\G(?:.)*x",
    @"^",
    @"$",
    @"\M",
    @"\m",
    @"\p{Cs}",
    @"\p{Cs}+",
    @"[\ud800-\udfff]",
    @"[\udc00-\udfff].",
    @"\udc28",
    @"\ud801",
    @"\ud83d",
    @"(?V1)[^\s]*x",
    @"(?f)x",
    @"(?:.)*(*SKIP)x",
    @"(?:.)*(*PRUNE)x|c",
    @"(?:.)*\Kx",
    @"(?|(.)|(x))*x",
    @"(?:.)*x(?<=\X)",
    @"(?:(?<=.).)*",
    @"(?<=(?=(.)).).",
];
string[] subjects =
[
    "\U00010428xc",
    "a\U0001F600b",
    "\U0001F600\U0001F601x\U0001F602\na",
    "x\U0001F600",
    "\U0001F600",
    "a\U0001F600\U0001F600b",
    "\U0001F600x\U0001F600",
];

string output = args.FirstOrDefault(a => a.EndsWith(".tsv", StringComparison.Ordinal)) ?? "surrogate-slice-sweep.tsv";
bool reportAll = args.Contains("--all");
int only = Option("--atom", -1);
var timeout = TimeSpan.FromMilliseconds(Option("--timeout-ms", 2000));
var hangBound = TimeSpan.FromSeconds(Option("--hang-s", 8));
int cases = 0,
    flagged = 0,
    hung = 0;
var rows = new StringBuilder();

for (int a = 0; a < atoms.Length; a++)
{
    if (only >= 0 && a != only)
    {
        continue;
    }

    foreach (string source in new[] { atoms[a], "(?r)" + atoms[a] })
    {
        var pattern = new FuzzyRegex(source, FuzzyRegexOptions.None, timeout);
        bool reversed = source.StartsWith("(?r)", StringComparison.Ordinal);

        // \K moves the reported start, so the edge checks do not apply to it.
        bool checkEdges = !source.Contains(@"\K", StringComparison.Ordinal);

        foreach (string subject in subjects)
        {
            for (int b = 0; b <= subject.Length; b++)
            {
                for (int e = b; e <= subject.Length; e++)
                {
                    if (!Splits(subject, b) && !Splits(subject, e))
                    {
                        continue;
                    }

                    string twin = Twin(subject, b, e);
                    foreach ((string api, Func<string, int, int, Answer> call) in Calls(pattern))
                    {
                        cases++;
                        Answer got = await Run(() => call(subject, b, e - b)).ConfigureAwait(false);
                        Answer want = await Run(() => call(twin, b, e - b)).ConfigureAwait(false);
                        var problems = new List<string>();
                        if (got.Error)
                        {
                            problems.Add("error");
                        }

                        if (!string.Equals(got.Text, want.Text, StringComparison.Ordinal))
                        {
                            problems.Add("twin-differs");
                        }

                        if (checkEdges)
                        {
                            problems.AddRange(EdgeProblems(api, got.Spans, b, e, reversed));
                        }

                        if (problems.Count > 0)
                        {
                            flagged++;
                        }

                        if (problems.Count > 0 || reportAll)
                        {
                            rows.Append(string.Join(",", problems.Distinct(StringComparer.Ordinal)))
                                .Append('\t')
                                .Append(Esc(source))
                                .Append('\t')
                                .Append(Esc(subject))
                                .Append('\t')
                                .Append(b)
                                .Append(',')
                                .Append(e - b)
                                .Append('\t')
                                .Append(api)
                                .Append("\tgot=")
                                .Append(Esc(got.Text))
                                .Append("\twant=")
                                .Append(Esc(want.Text))
                                .Append('\t')
                                .Append(Hex(source))
                                .Append('\t')
                                .Append(Hex(subject))
                                .Append('\n');
                        }
                    }
                }
            }
        }
    }
}

await File.WriteAllTextAsync(output, rows.ToString()).ConfigureAwait(false);
Console.WriteLine($"cases={cases} flagged={flagged} hung={hung}");

int Option(string name, int fallback)
{
    int at = Array.IndexOf(args, name);
    return at >= 0 ? int.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture) : fallback;
}

async Task<Answer> Run(Func<Answer> call)
{
    Task<Answer> task = Task.Run(() =>
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            string kind = ex is System.Text.RegularExpressions.RegexMatchTimeoutException ? "TIMEOUT" : "EXC";
            return ($"{kind}:{ex.GetType().Name}", [], true);
        }
    });
    if (await Task.WhenAny(task, Task.Delay(hangBound)).ConfigureAwait(false) != task)
    {
        Console.WriteLine($"hang number {++hung}");
        if (hung >= 20)
        {
            await File.WriteAllTextAsync(output, rows + "ABORTED: too many hung threads\n").ConfigureAwait(false);
            Environment.Exit(3);
        }

        return ("HANG", [], true);
    }

    return await task.ConfigureAwait(false);
}

static (string Api, Func<string, int, int, Answer> Call)[] Calls(FuzzyRegex re) =>
    [
        ("search", (t, b, n) => One(re.Match(t, b, n))),
        ("match", (t, b, n) => One(re.MatchAtStart(t, b, n))),
        ("full", (t, b, n) => One(re.FullMatch(t, b, n))),
        ("psearch", (t, b, n) => One(re.Match(t, b, n, partial: true))),
        ("pmatch", (t, b, n) => One(re.MatchAtStart(t, b, n, partial: true))),
        ("pfull", (t, b, n) => One(re.FullMatch(t, b, n, partial: true))),
        ("findall", (t, b, n) => Many(re.Matches(t, b, n))),
        ("overlap", (t, b, n) => Many(re.Matches(t, b, n, overlapped: true))),
        ("pfindall", (t, b, n) => Many(re.Matches(t, b, n, partial: true))),
        (
            "count",
            (t, b, n) => (re.Count(t, b, n).ToString(System.Globalization.CultureInfo.InvariantCulture), [], false)
        ),
        ("sub", (t, b, n) => (Norm(re.Replace(t, @"<\g<0>>", -1, b, n)), [], false)),
    ];

static IEnumerable<string> EdgeProblems(string api, List<(int Start, int End)> spans, int b, int e, bool reversed)
{
    foreach ((int start, int end) in spans)
    {
        if (start < b || end > e)
        {
            yield return "outside-slice";
        }

        if (string.Equals(api, "full", StringComparison.Ordinal) && (start != b || end != e))
        {
            yield return "full-not-slice";
        }

        if (string.Equals(api, "match", StringComparison.Ordinal) && (reversed ? end != e : start != b))
        {
            yield return "match-not-at-edge";
        }
    }
}

static bool Splits(string s, int i) =>
    i > 0 && i < s.Length && char.IsHighSurrogate(s[i - 1]) && char.IsLowSurrogate(s[i]);

// The same subject with each cut-off half replaced by U+0001.
static string Twin(string s, int b, int e)
{
    char[] units = s.ToCharArray();
    if (Splits(s, b))
    {
        units[b - 1] = '\u0001';
    }

    if (Splits(s, e))
    {
        units[e] = '\u0001';
    }

    return new string(units);
}

static string Hex(string s) =>
    string.Join(" ", s.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));

static string Esc(string s) => string.Concat(s.Select(c => c is < ' ' or > '~' ? $"\\u{(int)c:X4}" : c.ToString()));

// The placeholder and the halves it stands in for print the same.
static string Norm(string s) => string.Concat(s.Select(c => char.IsSurrogate(c) || c == '\u0001' ? '?' : c));

static Answer One(Match m) => (Describe(m), m.Success ? [(m.Index, m.Index + m.Length)] : [], false);

static Answer Many(IEnumerable<Match> matches)
{
    List<Match> all = [.. matches];
    return (string.Join(" ", all.Select(Describe)), [.. all.Select(m => (m.Index, m.Index + m.Length))], false);
}

static string Describe(Match m)
{
    if (!m.Success)
    {
        return "None";
    }

    var text = new StringBuilder($"({m.Index},{m.Index + m.Length}){(m.PartialMatch ? "p" : "")}");
    for (int g = 1; g < m.Groups.Count; g++)
    {
        Group group = m.Groups[g];
        text.Append(group.Success ? $"[{group.Index},{group.Index + group.Length}]" : "[-]");
    }

    return text.ToString();
}
