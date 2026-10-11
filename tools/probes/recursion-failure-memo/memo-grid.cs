#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: does the failed-call memo
// change any answer? Random small patterns that mix fuzzy sections with recursion and the
// constructs the note names (captures, backreferences, conditionals, lookaround, atomic groups,
// possessive and bounded repeats, \K, \G, (?&name), (?0), verbs, (?b), (?e), (?r), POSIX), every
// subject over {a, b, x} up to length 3 plus a few longer ones, four modes (search, match,
// fullmatch, partial search). Each answer, capture lists included, is compared with the memo off
// (PatternObject.SkipCallMemo) and on from the first call (PatternObject.EagerCallMemo).
//
//     dotnet run -c Release tools/probes/recursion-failure-memo/memo-grid.cs [seed] [patterns] [minutes] [unsafe|nonarrow]
//
// "unsafe" switches the memo on even where PatternObject.UseCallMemo is off, which is how the
// exclusions' witnesses were found. "nonarrow" turns PatternObject.NarrowExactDeletions off, so
// the exact-deletion narrowing is compared both ways. A row counts only if the memo-off answer
// came back inside 250 ms.

#pragma warning disable
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Fuzzy.Text.RegularExpressions;

int seed = args.Length > 0 ? int.Parse(args[0]) : 1;
int patternCount = args.Length > 1 ? int.Parse(args[1]) : 2000;
double minutes = args.Length > 2 ? double.Parse(args[2]) : 8;
string variant = args.Length > 3 ? args[3] : "";
var rng = new Random(seed);

const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
PropertyInfo patternObjectProperty = typeof(FuzzyRegex).GetProperty("PatternObject", Any)!;
Type patternType = patternObjectProperty.PropertyType;
FieldInfo skipField = patternType.GetField("SkipCallMemo", Any)!;
FieldInfo eagerField = patternType.GetField("EagerCallMemo", Any)!;
FieldInfo useField = patternType.GetField("UseCallMemo", Any)!;
FieldInfo narrowField = patternType.GetField("NarrowExactDeletions", Any)!;
FieldInfo cacheField = typeof(FuzzyRegex).GetField("StateCache", Any)!;
FieldInfo slotField = cacheField.FieldType.GetField("_state", Any)!;
FieldInfo hitsField = slotField.FieldType.GetField("CallMemoHits", Any)!;

string[] fuzzy = ["{e<=1}", "{1<=e<=2}", "{d<=1}", "{1<=d<=2}", "{s<=1,d<=1}", "{i<=1}", "{e<=2}", "{2<=e<=3}"];
string[] quants = ["", "", "+", "*", "?", "{2,}", "*?", "+?", "{1,2}", "{0,2}", "++", "*+"];

// Half the patterns are "restricted": no verbs, no \K, no POSIX, and lookaround and atomic bodies
// drawn from Plain, which holds no capture or call. Those are the patterns the memo runs on, since
// any of the others turns it off (PatternObject.UseCallMemo); the other half checks that it does.
bool restricted = false;

string Plain(int depth)
{
    var sb = new StringBuilder();
    int n = 1 + rng.Next(2);
    for (int i = 0; i < n; i++)
        sb.Append(
            rng.Next(6) switch
            {
                0 => "a",
                1 => "b",
                2 => ".",
                3 => @"\G",
                4 => "(?:a|b.)" + fuzzy[rng.Next(fuzzy.Length)],
                _ => "",
            }
        );
    return sb.ToString();
}

string Atom(int depth)
{
    int r = rng.Next(depth > 2 ? 6 : 30);
    if (restricted && r is 16 or 17 or 24)
        r = 19;
    return r switch
    {
        0 => "a",
        1 => "b",
        2 => ".",
        3 => "(?R)",
        4 => "(?1)",
        5 => "",
        6 or 7 => "(?:" + Expr(depth + 1) + ")" + fuzzy[rng.Next(fuzzy.Length)],
        8 or 9 => "(?:" + Expr(depth + 1) + ")" + quants[rng.Next(quants.Length)],
        10 => "(" + Expr(depth + 1) + ")",
        11 => "(?=" + (restricted ? Plain(depth + 1) : Expr(depth + 1)) + ")",
        12 => "(?!" + (restricted ? Plain(depth + 1) : Expr(depth + 1)) + ")",
        13 => "(?>" + (restricted ? Plain(depth + 1) : Expr(depth + 1)) + ")",
        14 => "\\1",
        15 => "(?(1)" + Expr(depth + 1) + "|" + Expr(depth + 1) + ")",
        16 => "(*PRUNE)",
        17 => "(*SKIP)",
        18 => "(?<=a)",
        19 => "(?:" + Expr(depth + 1) + "|" + Expr(depth + 1) + ")",
        20 => "(?:(?:" + Expr(depth + 1) + ")" + fuzzy[rng.Next(fuzzy.Length)] + ")" + quants[rng.Next(quants.Length)],
        21 => "\\G",
        22 => "(?&n)",
        23 => "(?0)",
        24 => "\\K",
        25 => "(?<=" + (restricted ? Plain(depth + 1) : Expr(depth + 1)) + ")",
        26 => "(?:(?1)|)",
        27 => "(?:.??(?1)|" + Expr(depth + 1) + ")",
        _ => "(?:(?R)|)",
    };
}

string Expr(int depth)
{
    var sb = new StringBuilder();
    int n = 1 + rng.Next(3);
    for (int i = 0; i < n; i++)
        sb.Append(Atom(depth));
    return sb.ToString();
}

string Pattern()
{
    restricted = rng.Next(2) == 0;
    string[] flags = ["", "", "", "(?b)", "(?b)", "(?e)", "(?r)", restricted ? "" : "(?p)"];
    string flag = flags[rng.Next(flags.Length)];
    // Group 1 first so \1, (?1) and (?(1)...) always compile, a fuzzy section with a call inside,
    // and a named group "n" for (?&n).
    if (rng.Next(4) != 0)
    {
        return flag
            + "("
            + Expr(1)
            + ")"
            + "(?:"
            + Expr(1)
            + "(?R)?"
            + Expr(1)
            + ")"
            + fuzzy[rng.Next(fuzzy.Length)]
            + "(?P<n>"
            + Expr(2)
            + ")?";
    }

    // The best-match second walk over several entries, offsets and error limits, with the called
    // group defined after the section, as in the search-anchor witness
    // (?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga)).
    string called = rng.Next(2) == 0 ? "\\G" + Expr(2) : Expr(2);
    string define =
        rng.Next(2) == 0
            ? "(?(DEFINE)(" + called + ")(?P<n>" + Expr(2) + "))"
            : "(?:|(" + called + ")(?P<n>" + Expr(2) + "))";
    return (rng.Next(3) == 0 ? "(?e)" : "(?b)")
        + "(?:"
        + Expr(1)
        + "(?1)|"
        + Expr(1)
        + ")"
        + "(?:"
        + Expr(1)
        + ")"
        + fuzzy[rng.Next(fuzzy.Length)]
        + define;
}

var subjects = new List<string> { "" };
for (int len = 1; len <= 3; len++)
{
    foreach (string s in subjects.Where(s => s.Length == len - 1).ToList())
    foreach (char c in "abx")
        subjects.Add(s + c);
}
subjects.AddRange(["abab", "baxba", "aabba", "xabax"]);

long lastHits = 0;

string Answer(FuzzyRegex regex, object po, string subject, int mode, bool memo)
{
    skipField.SetValue(po, !memo);
    eagerField.SetValue(po, memo);
    lastHits = 0;
    try
    {
        var t = TimeSpan.FromMilliseconds(250);
        Match m = mode switch
        {
            0 => regex.Match(subject, timeout: t),
            1 => regex.MatchAtStart(subject, timeout: t),
            2 => regex.FullMatch(subject, timeout: t),
            _ => regex.Match(subject, partial: true, timeout: t),
        };
        object? state = slotField.GetValue(cacheField.GetValue(regex));
        lastHits = state is null ? 0 : (long)hitsField.GetValue(state)!;
        if (!m.Success)
            return "none";
        var sb = new StringBuilder();
        for (int g = 0; g < m.Groups.Count; g++)
        {
            var grp = m.Groups[g];
            sb.Append(grp.Success ? $"({grp.Index},{grp.Index + grp.Length})" : "(-)");
        }
        sb.Append(
            $" {m.FuzzyCounts.Substitutions}s{m.FuzzyCounts.Insertions}i{m.FuzzyCounts.Deletions}d {m.PartialMatch} {string.Join(",", m.FuzzyChanges.Substitutions)}|{string.Join(",", m.FuzzyChanges.Insertions)}|{string.Join(",", m.FuzzyChanges.Deletions)}"
        );
        sb.Append(" ##");
        for (int g = 0; g < m.Groups.Count; g++)
        {
            foreach (var cap in m.Groups[g].Captures)
                sb.Append($"[{cap.Index},{cap.Length}]");
            sb.Append(';');
        }
        return sb.ToString();
    }
    catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
    {
        return "TIMEOUT";
    }
    catch (Exception e)
    {
        return "EXC " + e.GetType().Name;
    }
}

var watch = Stopwatch.StartNew();
long rows = 0,
    skipped = 0,
    hitRows = 0,
    memoPatterns = 0,
    narrowRows = 0,
    compileErrors = 0,
    core = 0,
    capturesOnly = 0,
    partialDiffs = 0,
    offTimeouts = 0,
    bothTimeouts = 0,
    onOnlyTimeouts = 0,
    slowWithMemo = 0;
var slowShapes = new List<string>();
var shown = new List<string>();
var compileErrorExamples = new List<string>();
int done = 0;
for (int p = 0; p < patternCount && watch.Elapsed.TotalMinutes < minutes; p++, done++)
{
    string pattern = Pattern();
    FuzzyRegex regex;
    try
    {
        regex = new FuzzyRegex(pattern);
    }
    catch (Exception e)
    {
        compileErrors++;
        if (compileErrorExamples.Count < 4)
            compileErrorExamples.Add($"{pattern}: {e.Message}");
        continue;
    }

    object po = patternObjectProperty.GetValue(regex)!;
    if (variant == "unsafe")
        useField.SetValue(po, true);
    if (variant == "nonarrow")
        narrowField.SetValue(po, false);
    bool uses = (bool)useField.GetValue(po)!;
    bool narrow = (bool)narrowField.GetValue(po)!;
    if (uses)
        memoPatterns++;

    foreach (string subject in subjects)
    {
        for (int mode = 0; mode < 4; mode++)
        {
            string off = Answer(regex, po, subject, mode, false);
            if (off == "TIMEOUT" || off.StartsWith("EXC"))
            {
                if (off == "TIMEOUT" && mode != 3)
                {
                    offTimeouts++;
                    if (Answer(regex, po, subject, mode, true) == "TIMEOUT")
                    {
                        bothTimeouts++;
                        if (uses)
                        {
                            slowWithMemo++;
                            if (
                                slowShapes.Count < 4
                                && !slowShapes.Any(x => x.StartsWith(pattern + " ", StringComparison.Ordinal))
                            )
                                slowShapes.Add($"{pattern} '{subject}' mode {mode}");
                        }
                    }
                }
                skipped++;
                continue;
            }
            rows++;
            if (narrow)
                narrowRows++;
            string on = Answer(regex, po, subject, mode, true);
            if (lastHits > 0)
                hitRows++;
            if (on == off)
                continue;
            if (on == "TIMEOUT")
            {
                // Slower with the memo from the first call, not a different answer.
                onOnlyTimeouts++;
                continue;
            }
            string kind;
            if (mode == 3)
            {
                partialDiffs++;
                kind = "partial";
            }
            else if (on.Split(" ##")[0] != off.Split(" ##")[0])
            {
                core++;
                kind = "core";
            }
            else
            {
                capturesOnly++;
                kind = "captures";
            }
            if (shown.Count < 12)
                shown.Add($"{kind} mode {mode} {pattern} '{subject}': off {off} | on {on}");
        }
    }
}

Console.WriteLine(
    $"seed {seed} {variant}: {done} patterns ({compileErrors} did not compile, {memoPatterns} with the memo on), {rows} rows, {skipped} skipped (timeout or exception with the memo off), {hitRows} rows where the memo fired, {narrowRows} rows with the exact-deletion narrowing on ({watch.Elapsed.TotalSeconds:F0} s)"
);
Console.WriteLine($"changed: span/group/count/edit {core}; capture lists only {capturesOnly}; partial {partialDiffs}");
Console.WriteLine(
    $"non-partial rows over 250 ms with the memo off {offTimeouts}, still over 250 ms with it on {bothTimeouts} ({slowWithMemo} where the memo runs); over 250 ms only with the memo on {onOnlyTimeouts}"
);
foreach (string x in slowShapes)
    Console.WriteLine("  still slow with the memo: " + x);
foreach (string x in compileErrorExamples)
    Console.WriteLine("  did not compile: " + (x.Length > 300 ? x[..300] : x));
foreach (string x in shown.OrderBy(x => x.Length))
    Console.WriteLine("  " + (x.Length > 400 ? x[..400] : x));
