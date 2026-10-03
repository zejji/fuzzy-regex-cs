#:property TreatWarningsAsErrors=false
#:property RunAnalyzers=false
#:property EnforceCodeStyleInBuild=false
#:property Nullable=disable
// The .NET 10 BCL Regex half of tools/matrix/survey.py (System.Text.RegularExpressions, backtracking).
//
//     dotnet run -c Release tools/matrix/survey_bcl.cs -- <rows.jsonl> <start-index>
//
// Reads translated rows from a FILE, prints READY and one JSON line per row. Each match has a 2 s
// matchTimeout; survey.py's watchdog is the outer bound. "match" is a search whose answer must start
// at 0 (forwards) or end at the subject's end (RightToLeft): the first start a backtracking search
// tries is that position, so its first match there is the anchored match. "fullmatch" arrives as
// \A(?:...)\z. Groups come back in upstream's numbering: survey.py passes each group's name, and an
// unnamed group is .NET's k-th unnamed group, since .NET numbers unnamed groups before named ones.
// Spans are UTF-16 code units; survey.py converts.
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var lines = File.ReadAllLines(args[0]).Where(l => l.Trim().Length > 0).ToList();
int start = int.Parse(args[1]);
var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
stdout.WriteLine("READY");
for (int k = start; k < lines.Count; k++)
{
    var row = JsonNode.Parse(lines[k])!.AsObject();
    var res = new JsonObject { ["i"] = row["i"]!.GetValue<int>(), ["unit"] = "utf16" };
    try
    {
        string flags = row["flags"]!.GetValue<string>();
        var options = RegexOptions.None;
        if (flags.Contains('i'))
            options |= RegexOptions.IgnoreCase;
        if (flags.Contains('m'))
            options |= RegexOptions.Multiline;
        if (flags.Contains('s'))
            options |= RegexOptions.Singleline;
        bool rtl = flags.Contains('r');
        if (rtl)
            options |= RegexOptions.RightToLeft;
        var re = new Regex(row["pattern"]!.GetValue<string>(), options, TimeSpan.FromSeconds(2));
        string subject = row["subject"]!.GetValue<string>();
        string op = row["op"]!.GetValue<string>();
        var names = row["groupnames"]!.AsArray();
        if (op == "finditer")
        {
            var arr = new JsonArray();
            foreach (Match m in re.Matches(subject))
            {
                arr.Add(new JsonObject { ["span"] = new JsonArray(m.Index, m.Index + m.Length), ["partial"] = false });
                if (arr.Count > 50)
                    break;
            }
            res["status"] = "matches";
            res["matches"] = arr;
        }
        else
        {
            var m = re.Match(subject);
            bool anchoredMiss =
                op == "match" && m.Success && (rtl ? m.Index + m.Length != subject.Length : m.Index != 0);
            if (!m.Success || anchoredMiss)
            {
                res["status"] = "none";
            }
            else
            {
                res["status"] = "match";
                res["span"] = new JsonArray(m.Index, m.Index + m.Length);
                var groups = new JsonArray();
                var caps = new JsonArray();
                int unnamed = 0;
                foreach (var name in names)
                {
                    Group g = name is null ? m.Groups[++unnamed] : m.Groups[name.GetValue<string>()];
                    groups.Add(g.Success ? new JsonArray(g.Index, g.Index + g.Length) : null);
                    var list = new JsonArray();
                    foreach (Capture c in g.Captures)
                        list.Add(new JsonArray(c.Index, c.Index + c.Length));
                    caps.Add(list);
                }
                res["groups"] = groups;
                res["captures"] = caps;
            }
        }
    }
    catch (RegexMatchTimeoutException)
    {
        res["status"] = "timeout";
    }
    catch (Exception e)
    {
        res["status"] = "error";
        string msg = e.GetType().Name + ": " + e.Message;
        res["error"] = msg.Length > 200 ? msg[..200] : msg;
    }
    stdout.WriteLine(res.ToJsonString());
}
