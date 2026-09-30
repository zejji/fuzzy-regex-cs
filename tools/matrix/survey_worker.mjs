// JavaScript's half of tools/matrix/survey.py (node 24, V8's Irregexp).
//
//     node tools/matrix/survey_worker.mjs <rows.jsonl> <start-index>
//
// Reads translated rows from a FILE, prints READY and then one JSON line per row. JavaScript has
// no regex timeout, so survey.py's per-row watchdog is the only bound: it kills this process and
// restarts it after the slow row. "match" is the sticky flag (anchored at lastIndex 0); "fullmatch"
// arrives from survey.py already written as (?:...)$ with the sticky flag, and without the m flag
// JavaScript's $ is the end of the input. Spans are UTF-16 code units; survey.py converts.
import { readFileSync, writeSync } from "node:fs";

const [path, startText] = process.argv.slice(2);
const rows = readFileSync(path, "utf8").split("\n").filter((l) => l.trim()).map((l) => JSON.parse(l));
const out = (o) => writeSync(1, JSON.stringify(o) + "\n");
out("READY");
for (const row of rows.slice(Number(startText))) {
  const res = { i: row.i, unit: "utf16" };
  try {
    const base = row.flags + "d";
    const op = row.op;
    if (op === "finditer") {
      const re = new RegExp(row.pattern, base + "g");
      res.status = "matches";
      res.matches = [];
      for (const m of row.subject.matchAll(re)) {
        res.matches.push({ span: [m.index, m.index + m[0].length], partial: false });
        if (res.matches.length > 50) break;
      }
    } else {
      const re = new RegExp(row.pattern, base + (op === "search" ? "" : "y"));
      const m = re.exec(row.subject);
      if (m === null) {
        res.status = "none";
      } else {
        res.status = "match";
        res.span = m.indices[0];
        res.groups = [];
        for (let g = 1; g <= row.ngroups; g++) res.groups.push(m.indices[g] ?? null);
      }
    }
  } catch (e) {
    res.status = "error";
    res.error = String(e).slice(0, 200);
  }
  out(res);
}
