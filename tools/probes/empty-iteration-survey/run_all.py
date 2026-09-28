"""Empty-iteration survey (2026-09-26): may a repeat take an iteration that matches no text?

Runs one battery (battery.tsv) on every engine available on the owner's machine and a fuzzy
battery (fuzzy.tsv) on mrab regex and TRE. Write-up: docs/plan/2026-09-26-empty-iteration-survey.md.

    python tools/probes/empty-iteration-survey/run_all.py

Windows-side engines: Python re, regex, PCRE2 (pip `pcre2`), RE2 (google-re2, installed with
`pip install --target <this dir>/pylib google-re2`; skipped if absent), Perl, Node, Java, .NET.
WSL Ubuntu engines: glibc regexec, TRE (libtre-dev, tre-agrep), Ruby (Onigmo); skipped without WSL.
Each line printed is: engine <TAB> case <TAB> result.
"""
import os, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))

def run(cmd, **kw):
    try:
        r = subprocess.run(cmd, cwd=HERE, capture_output=True, text=True, timeout=600, **kw)
        sys.stdout.write(r.stdout)
        if r.returncode:
            print(f"# {cmd[0]} exited {r.returncode}: {r.stderr.strip()[:300]}")
    except FileNotFoundError:
        print(f"# {cmd[0]} not found, skipped")

run([sys.executable, "run_python.py"])
run([sys.executable, "run_mrab_fuzzy.py"])
run(["perl", "run_perl.pl"])
run(["node", "run_node.js"])
run(["java", "RunJava.java", "."])
env = dict(os.environ, MSBUILDDISABLENODEREUSE="1")
run(["dotnet", "run", "RunDotnet.cs", "-p:UseSharedCompilation=false", "--", "."], env=env)
if shutil.which("wsl"):
    win = HERE.replace("\\", "/")
    wsl_dir = subprocess.run(["wsl", "-d", "Ubuntu", "--", "wslpath", "-a", win],
                             capture_output=True, text=True).stdout.strip()
    script = (f"cd '{wsl_dir}' && gcc posix_glibc.c -o /tmp/eis_glibc && /tmp/eis_glibc && "
              "gcc tre_probe.c -ltre -o /tmp/eis_tre && /tmp/eis_tre && ruby run_ruby.rb && "
              "bash run_tre_agrep.sh")
    run(["wsl", "-d", "Ubuntu", "--", "bash", "-c", script], env=dict(os.environ, MSYS_NO_PATHCONV="1"))
else:
    print("# wsl not found: glibc, TRE and Ruby skipped")
