"""The complete matrix's Python half: upstream and reference answers for checks C1x, C4 and C6.

    python tools/matrix/pyworker.py TASK ROWS.jsonl OUT.jsonl

TASK is one of:
  c1x    upstream regex 2026.9.10's answer, in port-runner.cs's format, for the rows the oracle's
         recorder cannot ask (finditer with partial=True or with pos/endpos);
  c4up   tools/matrix/d11-brute-judge.py's verdict (upstream without partial matching, over every
         continuation of up to 3 characters), for partial rows;
  c6     tools/probes/fuzzy-reference-matcher.py's answer, for rows inside its subset.

A row the task does not apply to answers "n/a". Upstream can hang or crash on a row, so a child
process answers the rows and this supervisor restarts it past a row that kills it ("ERR Crash") or
takes over ROW_SECONDS ("ERR Hang"). Output is appended one line per row and flushed, so a run
that is stopped resumes where it stopped. Never reads stdin.
"""

from __future__ import annotations

import importlib.util
import json
import queue
import re
import subprocess
import sys
import threading
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROW_SECONDS = 15.0
INLINE = (("flag-b", 4096, "b"), ("flag-e", 32768, "e"), ("flag-r", 1024, "r"), ("flag-i", 2, "i"),
          ("flag-f", 16384, "f"), ("flag-w", 2048, "w"), ("flag-p", 65536, "p"))


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def describe(m, groups: int) -> str:
    """port-runner.cs's Describe, from an upstream match."""
    s, i, d = m.fuzzy_counts
    subs, ins, dels = m.fuzzy_changes
    out = f"({m.start()},{m.end()})" + ("P" if m.partial else "") + f" {s},{i},{d}"
    out += " [" + ",".join(map(str, subs)) + "|" + ",".join(map(str, ins)) + "|" + ",".join(map(str, dels)) + "]"
    out += " g" + "".join(f"({m.start(g)},{m.end(g)})" if m.start(g) >= 0 else "(-)" for g in range(1, groups + 1))
    out += " c" + "".join("[" + "".join(f"({a},{b})" for a, b in m.spans(g)) + "]" for g in range(1, groups + 1))
    return out


def c1x(row, regex) -> str:
    if row["operation"] != "finditer" or not (row.get("partial") or row.get("pos") is not None):
        return "n/a"
    compiled = regex.compile(row["pattern"], row["flags"], **(row.get("namedLists") or {}))
    pos = row.get("pos") or 0
    endpos = row.get("endpos")
    args = (row["subject"], pos) if endpos is None else (row["subject"], pos, endpos)
    try:
        found = list(compiled.finditer(*args, partial=bool(row.get("partial")), timeout=5))
    except TimeoutError:
        return "ERR Timeout"
    return "[" + " ; ".join(describe(m, compiled.groups) for m in found) + "]"


def inline_prefix(flags: int) -> str:
    letters = "".join(ch for _, bit, ch in INLINE if flags & bit)
    prefix = f"(?{letters})" if letters else ""
    if flags & 256:
        prefix += "(?V1)"
    return prefix


def c4up(row, judge_mod) -> str:
    if not row.get("partial") or row["operation"] == "finditer" or row.get("pos") is not None:
        return "n/a"
    if row.get("namedLists"):
        return "n/a"  # the judge compiles without keyword arguments
    verdict, _ = judge_mod.judge(row["operation"], inline_prefix(row["flags"]) + row["pattern"], row["subject"], max_len=3)
    return judge_mod.show(verdict)


def unnamed(pattern: str, regex) -> str | None:
    """The pattern with named groups and named references turned into numbered ones."""
    if re.search(r"\(\?(?:&|P>|R\)|0\)|\d+\))", pattern):
        return None
    index = regex.compile(pattern).groupindex
    out = re.sub(r"\(\?P<\w+>", "(", pattern)
    out = re.sub(r"\(\?P=(\w+)\)", lambda m: "\\" + str(index[m.group(1)]), out)
    out = re.sub(r"\\g<(\w+)>", lambda m: "\\" + str(index.get(m.group(1), m.group(1))), out)
    out = re.sub(r"\(\?\((g\d+)\)", lambda m: f"(?({index[m.group(1)]})", out)
    return out


def c6(row, ref, regex) -> str:
    if row["flags"] or row.get("namedLists") or row.get("partial") or row.get("pos") is not None:
        return "n/a"
    if row["operation"] not in ("search", "match", "fullmatch"):
        return "n/a"
    pattern = unnamed(row["pattern"], regex)
    if pattern is None:
        return "n/a"
    try:
        result = getattr(ref, row["operation"])(pattern, row["subject"])
    except RecursionError:
        return "n/a"
    except Exception:  # noqa: BLE001 - outside the reference's subset
        return "n/a"
    if result is None:
        return "None"
    s, i, d = result.fuzzy_counts
    groups = "".join(f"({a},{b})" if a >= 0 else "(-)" for a, b in result.groups)
    return f"({result.span[0]},{result.span[1]}) {s},{i},{d} g{groups}"


def child(task: str, rows_path: str, start: int) -> int:
    import regex
    judge_mod = _load(HERE / "d11-brute-judge.py", "d11_brute_judge") if task == "c4up" else None
    ref = _load(HERE.parent / "probes" / "fuzzy-reference-matcher.py", "fuzzy_reference") if task == "c6" else None
    sys.setrecursionlimit(20000)
    rows = [json.loads(line) for line in open(rows_path, encoding="utf-8") if line.strip()]
    for k in range(start, len(rows)):
        row = rows[k]
        try:
            if task == "c1x":
                a = c1x(row, regex)
            elif task == "c4up":
                a = c4up(row, judge_mod)
            else:
                a = c6(row, ref, regex)
        except Exception as e:  # noqa: BLE001
            a = "ERR " + type(e).__name__
        sys.stdout.write(f"{k}\t{json.dumps({'id': row.get('id', k), task: a})}\n")
        sys.stdout.flush()
    return 0


def supervise(task: str, rows_path: str, out_path: str) -> int:
    rows = [json.loads(line) for line in open(rows_path, encoding="utf-8") if line.strip()]
    done = sum(1 for line in open(out_path, encoding="utf-8") if line.strip()) if Path(out_path).exists() else 0
    with open(out_path, "a", encoding="utf-8", newline="\n") as out:
        start = done
        while start < len(rows):
            proc = subprocess.Popen([sys.executable, __file__, "--child", task, rows_path, str(start)],
                                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                    text=True, encoding="utf-8")
            q: queue.Queue = queue.Queue()
            threading.Thread(target=lambda: ([q.put(l) for l in proc.stdout], q.put(None)), daemon=True).start()
            while True:
                try:
                    line = q.get(timeout=ROW_SECONDS)
                except queue.Empty:
                    proc.kill()
                    out.write(json.dumps({"id": rows[start].get("id", start), task: "ERR Hang"}) + "\n")
                    start += 1
                    break
                if line is None:
                    proc.wait()
                    if start < len(rows):
                        out.write(json.dumps({"id": rows[start].get("id", start), task: "ERR Crash"}) + "\n")
                        start += 1
                    break
                k, payload = line.rstrip("\n").split("\t", 1)
                out.write(payload + "\n")
                out.flush()
                start = int(k) + 1
            proc.wait()
    return 0


if __name__ == "__main__":
    if sys.argv[1] == "--child":
        sys.exit(child(sys.argv[2], sys.argv[3], int(sys.argv[4])))
    sys.exit(supervise(sys.argv[1], sys.argv[2], sys.argv[3]))
