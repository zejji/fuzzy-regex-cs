#:project ../src/FuzzyRegex/FuzzyRegex.csproj
#:property PublishAot=false
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property Nullable=enable
// The battery screen's port half (tools/battery/screen.py): each row's answer, and the oracle-only
// switches, singly and then in pairs, whose answer is upstream's exactly.
//
//   dotnet run -c Release <worktree>/.battery/abl.cs -- ROWS.jsonl UPSTREAM.up OUT.tsv
//
// screen.py copies this file into the worktree of the commit under test, beside probe.cs, so the
// #:project path above is relative to <worktree>/.battery/, not to tools/battery/.
//
// ROWS: JSON lines with id, pattern, flags, subject, operation, partial, pos, endpos, namedLists,
// template, count, and `plain` (true: answer the row only, no switches; the screen's twins).
// UPSTREAM: screen.py's upstream answers, tab-separated, the id first and the answer fifth, in the
// same descriptor this file writes (span, partial mark, counts, change lists, capture lists).
// OUT, one line per row: id, the port's answer, then the switch sets whose answer equals upstream's
// ("-" for none, "cut" appended when the pair search ran out of its time).
//
// A switch is set alone: nothing else is forced on with it, unlike OracleComparer's named
// ablations, which add the D52, D54 and D81 switches (and run 20261016-2's triage copy, which added
// D51's too). A pair is two switches named together. Each switch is a PatternObject field or a
// compile argument that turns one of this port's fixes off, as the ExpectedDivergences entry that
// keys on it describes; screen.py maps each to its entry.
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

var rows = File.ReadLines(args[0]).Where(static l => !string.IsNullOrWhiteSpace(l)).ToList();
var upAnswers = new Dictionary<string, string>();
foreach (string line in File.ReadLines(args[1]))
{
    string[] p = line.Split('\t');
    if (p.Length >= 5)
    {
        upAnswers[p[0]] = p[4];
    }
}

var timeout = TimeSpan.FromSeconds(2);
var pairBudget = TimeSpan.FromSeconds(double.Parse(Environment.GetEnvironmentVariable("ABL_PAIR_SECONDS") ?? "30"));

MethodInfo build = typeof(FuzzyRegex).GetMethod("WithDefaultVersion", BindingFlags.NonPublic | BindingFlags.Static)!;
PropertyInfo poProp = typeof(FuzzyRegex).GetProperty("PatternObject", BindingFlags.NonPublic | BindingFlags.Instance)!;
Type poType = poProp.PropertyType;
const BindingFlags Field = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

string[] fieldSwitches =
[
    "SkipMovesTheSliceWhenItRuns",
    "VerbsAreConfinedToTheInnermostGroup",
    "KeepOutlivesALookaround",
    "KeepOutlivesAnAtomicGroup",
    "SkipsALazyRunDespiteAVerb",
    "StepsOnFromTheKeptStart",
    "BansAnEmptyMatchAtEveryCandidate",
    "AnchorsBackslashGAtEveryCandidate",
    "KeepSectionOpenAfterAVerb",
    "MatchMayEndInsideTheWholePatternCall",
    "KeepDiscardedTotals",
    "DropNegativeTestCaptures",
    "SkipRetriedFoldSteps",
    "DoubleCountTrailingInsertions",
    "UpstreamDefaultBoundary",
    "SecondPassAsBeforeD11",
    "TailRunOutAsBeforeD67",
    "RunOutAsBeforeD81",
    "SkipExactDeletionRetry",
    "SkipLookaroundInsertion",
    "KeepCallCaptures",
    "PartialRestoresFrameByFrame",
    "CheckMinimumBeforeTrailingInsertions",
    "UpstreamEmptyIterations",
    "ChargeUntouchedFoldings",
    "SkipGroupFoldLeftovers",
    "SkipLeftoverTakeBack",
    "SkipWholeFoldedCharEdits",
    "CallGuardComparesAcrossSections",
];
string[] compileSwitches =
[
    "upstreamReverseGrapheme",
    "upstreamFoldedRuns",
    "upstreamWholePatternCall",
    "upstreamCallFeatures",
    "upstreamFuzzyConditionTest",
    "upstreamCallCopies",
];

// noLift: OracleComparer.RunWithoutTheAnchorPin (rule H' off). d85: RunWithTheUpstreamRepeatGuards.
string[] all = [.. fieldSwitches, .. compileSwitches, "noLift", "d85"];

// A switch the commit under test does not have yet (an older run's worktree) is left out.
fieldSwitches = [.. fieldSwitches.Where(s => poType.GetField(s, Field) is not null)];
all = [.. all.Where(s => !s.StartsWith(char.ToUpperInvariant(s[0])) || fieldSwitches.Contains(s))];

using var output = new StreamWriter(args[2], false, new UTF8Encoding(false)) { NewLine = "\n" };
foreach (string line in rows)
{
    using var doc = JsonDocument.Parse(line);
    JsonElement row = doc.RootElement;
    string id = row.GetProperty("id").GetString()!;
    string baseAnswer = Answer(row, []);
    bool plain = row.TryGetProperty("plain", out var pl) && pl.ValueKind == JsonValueKind.True;
    if (plain || !upAnswers.TryGetValue(id, out string? want) || baseAnswer == want)
    {
        output.WriteLine($"{id}\t{baseAnswer}\t-");
        output.Flush();
        continue;
    }

    // An exact hit is upstream's answer to the character. A "~" hit differs from it only in the
    // change positions, which upstream can report from an abandoned path (ledger entry 11); the
    // screen decides whether that is proven.
    string wantLoose = WithoutPositions(want);
    var hits = new List<string>();
    void Try(string name, string[] set)
    {
        string got = Answer(row, set);
        if (got == want)
        {
            hits.Add(name);
        }
        else if (WithoutPositions(got) == wantLoose)
        {
            hits.Add("~" + name);
        }
    }

    // Where the row's own answer is upstream's bar the change positions, a switch that changes
    // nothing would "explain" it too: report that as the base's hit and try no switch.
    if (WithoutPositions(baseAnswer) == wantLoose)
    {
        output.WriteLine($"{id}\t{baseAnswer}\t~base");
        output.Flush();
        continue;
    }

    foreach (string s in all)
    {
        Try(s, [s]);
    }

    bool cut = false;
    if (!hits.Any(static h => !h.StartsWith('~')))
    {
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < all.Length && !cut; i++)
        {
            for (int j = i + 1; j < all.Length; j++)
            {
                if (clock.Elapsed > pairBudget)
                {
                    cut = true;
                    break;
                }

                Try(all[i] + "+" + all[j], [all[i], all[j]]);
            }
        }
    }

    string found = hits.Count == 0 ? "-" : string.Join(",", hits);
    output.WriteLine($"{id}\t{baseAnswer}\t{found}{(cut ? ",cut" : "")}");
    output.Flush();
}

string Answer(JsonElement row, string[] switches)
{
    string pattern = row.GetProperty("pattern").GetString()!;
    string subject = row.GetProperty("subject").GetString()!;
    string op = row.GetProperty("operation").GetString()!;
    int flags = row.TryGetProperty("flags", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
    bool partial = row.TryGetProperty("partial", out var p) && p.ValueKind == JsonValueKind.True;
    int? posN = row.TryGetProperty("pos", out var ps) && ps.ValueKind == JsonValueKind.Number ? ps.GetInt32() : null;
    int? endN = row.TryGetProperty("endpos", out var es) && es.ValueKind == JsonValueKind.Number ? es.GetInt32() : null;
    int begin = posN ?? 0;
    int length = Math.Max(0, Math.Min(endN ?? subject.Length, subject.Length) - begin);
    int count = row.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
    int limit = count == 0 ? -1 : count;
    var lists = new Dictionary<string, IReadOnlyCollection<string>>();
    if (row.TryGetProperty("namedLists", out var nl) && nl.ValueKind == JsonValueKind.Object)
    {
        foreach (var entry in nl.EnumerateObject())
        {
            lists[entry.Name] = [.. entry.Value.EnumerateArray().Select(static v => v.GetString()!)];
        }
    }

    try
    {
        object?[] a = new object?[11];
        a[0] = pattern;
        a[1] = (FuzzyRegexOptions)flags;
        a[2] = Timeout.InfiniteTimeSpan;
        a[3] = lists;
        a[4] = 8192; // the recorder's default version, V0
        for (int k = 0; k < compileSwitches.Length; k++)
        {
            a[5 + k] = switches.Contains(compileSwitches[k]);
        }

        var regex = (FuzzyRegex)build.Invoke(null, a)!;
        object po = poProp.GetValue(regex)!;
        foreach (string s in switches)
        {
            if (s == "noLift")
            {
                poType.GetField("LiftsAnchorBan", Field)!.SetValue(po, false);
                poType.GetField("AnchorGuards", Field)!.SetValue(po, null);
            }
            else if (s == "d85")
            {
                var list = (System.Collections.IEnumerable)poType.GetField("RepeatInfoList", Field)!.GetValue(po)!;
                foreach (object info in list)
                {
                    Type t = info.GetType();
                    t.GetField("TailKey", Field)!.SetValue(info, null);
                    t.GetField("BodyHoldsKeyedTail", Field)!.SetValue(info, false);
                    if ((bool)t.GetField("BodyGuardDroppedForD85", Field)!.GetValue(info)!)
                    {
                        FieldInfo st = t.GetField("Status", Field)!;
                        st.SetValue(info, (uint)st.GetValue(info)! | 1u);
                    }
                }
            }
            else if (fieldSwitches.Contains(s))
            {
                poType.GetField(s, Field)!.SetValue(po, true);
            }
        }

        return op switch
        {
            "match" => Describe(regex.MatchAtStart(subject, begin, length, partial, timeout)),
            "fullmatch" => Describe(regex.FullMatch(subject, begin, length, partial, timeout)),
            "search" => Describe(regex.Match(subject, begin, length, partial, timeout)),
            "finditer" or "finditer-overlapped" => "["
                + string.Join(
                    " | ",
                    regex
                        .Matches(subject, begin, length, op == "finditer-overlapped", partial, timeout)
                        .Select(Describe)
                )
                + "]",
            "sub" or "subf" => "sub " + Replace(regex, row, subject, limit, begin, length, op == "subf"),
            "split" => "split "
                + string.Join(
                    " ",
                    regex.Split(subject, limit, timeout).Select(static s => s is null ? "<null>" : Quote(s))
                ),
            _ => "unknown op",
        };
    }
    catch (TargetInvocationException e) when (e.InnerException is not null)
    {
        return "EXC " + e.InnerException.GetType().Name;
    }
    catch (Exception e)
    {
        return "EXC " + e.GetType().Name;
    }

    string Replace(FuzzyRegex regex, JsonElement r, string s, int lim, int b, int l, bool format)
    {
        string template = r.GetProperty("template").GetString()!;
        string replaced = format
            ? regex.ReplaceFormat(s, template, lim, out int k, b, l, timeout)
            : regex.Replace(s, template, lim, out k, b, l, timeout);
        return $"{k} {Quote(replaced)}";
    }
}

static string WithoutPositions(string answer) =>
    System.Text.RegularExpressions.Regex.Replace(answer, @" \[[\d,]*;[\d,]*;[\d,]*\]", "");

// The descriptor screen.py's upstream half writes: P for a partial match, the span, the counts and
// the change lists when there are errors, then every group's capture list.
static string Describe(Match m)
{
    if (!m.Success)
    {
        return "None";
    }

    FuzzyCounts counts = m.FuzzyCounts;
    string fuzzy = "";
    if (counts.Substitutions + counts.Insertions + counts.Deletions != 0)
    {
        FuzzyChanges ch = m.FuzzyChanges;
        fuzzy =
            $" ({counts.Substitutions},{counts.Insertions},{counts.Deletions})"
            + " ["
            + string.Join(",", ch.Substitutions)
            + ";"
            + string.Join(",", ch.Insertions)
            + ";"
            + string.Join(",", ch.Deletions)
            + "]";
    }

    string groups = string.Concat(
        Enumerable
            .Range(1, m.Groups.Count - 1)
            .Select(i =>
                $" g{i}="
                + string.Join(",", m.Groups[i].Captures.Select(static c => $"({c.Index},{c.Index + c.Length})"))
            )
    );
    return $"{(m.PartialMatch ? "P" : "")}({m.Index},{m.Index + m.Length}){fuzzy}{groups}";
}

static string Quote(string s)
{
    var sb = new StringBuilder("\"");
    foreach (char ch in s)
    {
        switch (ch)
        {
            case '"':
                sb.Append("\\\"");
                break;
            case '\\':
                sb.Append("\\\\");
                break;
            case '\n':
                sb.Append("\\n");
                break;
            case '\r':
                sb.Append("\\r");
                break;
            case '\t':
                sb.Append("\\t");
                break;
            default:
                if (ch < 0x20 || ch > 0x7e)
                {
                    sb.Append("\\u").Append(((int)ch).ToString("x4"));
                }
                else
                {
                    sb.Append(ch);
                }

                break;
        }
    }

    return sb.Append('"').ToString();
}
