"""Open question (survey finding 3): does an empty iteration that only changed a capture group count
as progress, so the loop goes round again? Upstream says yes (_regex.c:12552). Runs a few patterns
on every engine available here; each line is: engine <TAB> pattern over subject <TAB> result.

    python tools/probes/empty-iteration-survey/capture_progress.py
"""
import json, os, re, shutil, subprocess, sys, tempfile

# (id, pattern, subject). k = tested by a conditional, b = tested by a backreference, u = untested
# group (a control: every engine should agree). The {2,} and {0,5} rows check the law that X{2,}
# and X{0,5} match nothing X* cannot.
CASES = [
    ("k01", r"^(?:(?(1)c|z)|())*$", "c"),
    ("k02", r"(?:(?(1)c|z)|())*$", "c"),
    ("k03", r"^(?:(?(1)c|z)|())+$", "c"),
    ("k04", r"^(?:(?(1)c|z)|()){2,}$", "c"),
    ("k05", r"^(?:(?(1)c|z)|()){0,5}$", "c"),
    ("k06", r"^(?:(?(1)c|z)|())*$", "cc"),
    ("k07", r"^(?:(?(2)c|z)|(a)|())*$", "c"),
    ("k08", r"^(?:()|(?(1)c|z))*$", "c"),
    ("k09", r"^(?:(?(1)c|z)|())*?$", "c"),
    ("k10", r"^(?:(?(1)c|z)|())+?$", "c"),
    ("k11", r"^(?:(?(1)c|z)|())*+$", "c"),
    ("k12", r"^(?>(?:(?(1)c|z)|())*)$", "c"),
    ("k13", r"^(?:(?:(?(1)c|z)|())*)*$", "c"),
    ("k14", r"^(?:(?:(?(1)c|z)|())+)*$", "c"),
    ("k15", r"^(?:(?(1)c|z)|(()))*$", "c"),
    ("k16", r"^(?:(?(1)c|z)|())*$", ""),
    ("b01", r"^(?:\1c|())*$", "c"),
    ("b02", r"^(?:(?=(c))|\1)*$", "c"),
    ("b03", r"^(?:\1|(?=(c)))*$", "c"),
    ("b04", r"^(?:\1|(?=(c)))*?$", "c"),
    ("b05", r"^(?:\1|(?=(c)))*+$", "c"),
    ("b06", r"^(?:(?:\1|(?=(c)))*)*$", "c"),
    ("b07", r"^(?:\1|(?=(c))){2,}$", "c"),
    ("b08", r"^(?:\1|(?=(c)))+$", "c"),
    ("u01", r"^(?:(c)|())*$", "c"),
    ("u02", r"(?:(b|))*", "bba"),
    ("u03", r"^(?:(?=(c))|c)*$", "c"),
]
PAIRS = [(p, s) for _, p, s in CASES]


def show(span, g1):
    return "nomatch" if span is None else f"span={span[0]},{span[1]} g1={g1}"


def py_engines():
    import regex
    for name, mod in (("re " + sys.version.split()[0], re), ("regex " + regex.__version__, regex)):
        for p, s in PAIRS:
            try:
                m = mod.search(p, s)
                r = show(m and m.span(), m and (repr(m.group(1)) if m.group(1) is not None else "unset"))
            except Exception as e:
                r = "error: " + str(e)[:60]
            print(f"{name}\t{p} over {s!r}\t{r}")
    try:
        import pcre2
        for p, s in PAIRS:
            try:
                m = pcre2.compile(p).search(s)
                r = show(m and m.span(), m and (repr(m.group(1)) if m.group(1) is not None else "unset"))
            except Exception as e:
                r = "error: " + str(e)[:60]
            print(f"PCRE2 (pip pcre2)\t{p} over {s!r}\t{r}")
    except ImportError:
        print("# pcre2 not installed")


def run(cmd, **kw):
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=300, **kw)
        sys.stdout.write(r.stdout)
        if r.returncode:
            print(f"# {cmd[0]} exited {r.returncode}: {r.stderr.strip()[:300]}")
    except FileNotFoundError:
        print(f"# {cmd[0]} not found, skipped")


def main():
    py_engines()
    tmp = tempfile.mkdtemp()
    data = os.path.join(tmp, "cases.json")
    with open(data, "w", encoding="utf-8") as f:
        json.dump(PAIRS, f)
    perl = r'''
use JSON::PP; local $/; open my $f, "<", $ARGV[0]; my $c = decode_json(<$f>);
for my $x (@$c) { my ($p, $s) = @$x; my $r;
  my $ok = eval { if ($s =~ /$p/) { $r = sprintf "span=%d,%d g1=%s", $-[0], $+[0], defined $1 ? "'$1'" : "unset" } else { $r = "nomatch" } 1 };
  $r = "error: $@" unless $ok; $r =~ s/\n.*//s; print "perl $^V\t$p over '$s'\t$r\n"; }'''
    run(["perl", "-e", perl, data])
    node = r'''
const c = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
for (const [p, s] of c) { let r;
  try { const m = new RegExp(p).exec(s);
        r = m === null ? "nomatch" : `span=${m.index},${m.index + m[0].length} g1=${m[1] === undefined ? "unset" : "'" + m[1] + "'"}`; }
  catch (e) { r = "error: " + e.message.slice(0, 60); }
  console.log(`node ${process.version}\t${p} over '${s}'\t${r}`); }'''
    run(["node", "-e", node, data])
    java = os.path.join(tmp, "Cp.java")
    with open(java, "w", encoding="utf-8") as f:
        f.write(r'''import java.nio.file.*; import java.util.regex.*;
public class Cp { public static void main(String[] a) throws Exception {
  String[][] c = { %s };
  for (String[] x : c) { String r;
    try { Matcher m = Pattern.compile(x[0]).matcher(x[1]);
          r = m.find() ? "span=" + m.start() + "," + m.end() + " g1=" + (m.group(1) == null ? "unset" : "'" + m.group(1) + "'") : "nomatch"; }
    catch (Exception e) { r = "error: " + e.getMessage().split("\\R")[0]; }
    System.out.println("java " + System.getProperty("java.version") + "\t" + x[0] + " over '" + x[1] + "'\t" + r); } } }
''' % ", ".join("{%s, %s}" % (json.dumps(p), json.dumps(s)) for p, s in PAIRS))
    run(["java", java])
    cs = os.path.join(tmp, "Cp.cs")
    with open(cs, "w", encoding="utf-8") as f:
        f.write('using System.Text.RegularExpressions;\nvar c = new (string, string)[] { %s };\n' %
                ", ".join("(%s, %s)" % (json.dumps(p), json.dumps(s)) for p, s in PAIRS) + r'''
foreach (var (p, s) in c) { string r;
  try { var m = new Regex(p).Match(s);
        r = m.Success ? $"span={m.Index},{m.Index + m.Length} g1={(m.Groups[1].Success ? "'" + m.Groups[1].Value + "'" : "unset")}" : "nomatch"; }
  catch (Exception e) { r = "error: " + e.Message.Split('\n')[0]; }
  Console.WriteLine($".NET {Environment.Version}\t{p} over '{s}'\t{r}"); }
''')
    run(["dotnet", "run", cs, "-p:UseSharedCompilation=false"], env=dict(os.environ, MSBUILDDISABLENODEREUSE="1"))
    if shutil.which("wsl"):
        rb = ('require "json"; JSON.parse(STDIN.read).each { |p, s| r = begin m = Regexp.new(p).match(s); '
              'm.nil? ? "nomatch" : "span=#{m.begin(0)},#{m.end(0)} g1=#{m[1].nil? ? "unset" : "\'" + m[1] + "\'"}"\n'
              'rescue => e; "error: #{e.message[0, 60]}" end; puts "ruby #{RUBY_VERSION} (Onigmo)\\t#{p} over \'#{s}\'\\t#{r}" }')
        run(["wsl", "-d", "Ubuntu", "--", "ruby", "-e", rb], input=json.dumps(PAIRS))

    probe = os.environ.get("PORT_PROBE")  # path to port-probe.cs (git show 8dda4d9:tools/probes/port-probe.cs)
    if probe:
        rows = os.path.join(tmp, "rows.jsonl")
        with open(rows, "w", encoding="utf-8") as f:
            for p, s in PAIRS:
                f.write(json.dumps({"pattern": p, "subject": s, "operation": "search"}) + "\n")
        r = subprocess.run(["dotnet", "run", probe, "--", rows], capture_output=True, text=True, timeout=600)
        for line in r.stdout.splitlines():
            op, p, s, ans = line.split("\t")[:4]
            p, s = json.loads(p), json.loads(s)
            m = re.match(r"\((\d+),(\d+)\)", ans)
            res = "nomatch" if ans == "None" else f"span={m.group(1)},{m.group(2)}" if m else "error: " + ans[:60]
            print(f"port FuzzyRegex\t{p} over {s!r}\t{res}")


def table(raw):
    """Pivots the raw lines into a markdown table: one row per case, one column per engine."""
    ids = {f"{p} over {s!r}": i for i, p, s in CASES}
    cols, cells = [], {}
    for line in raw.splitlines():
        parts = line.split("\t")
        if len(parts) != 3 or parts[1] not in ids:
            continue
        eng = parts[0].split()[0]
        if eng not in cols:
            cols.append(eng)
        r = parts[2]
        cells[ids[parts[1]], eng] = "none" if r == "nomatch" else "err" if r.startswith("error") else r.split()[0][5:]
    print("| id | pattern | subject | " + " | ".join(cols) + " |")
    print("|---" * (3 + len(cols)) + "|")
    for i, p, s in CASES:
        pe = p.replace("|", "\\|")
        print(f"| {i} | `{pe}` | `{s}` | " + " | ".join(cells.get((i, c), "?") for c in cols) + " |")


if __name__ == "__main__":
    import contextlib, io
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        main()
    sys.stdout.write(buf.getvalue())
    print()
    table(buf.getvalue())
