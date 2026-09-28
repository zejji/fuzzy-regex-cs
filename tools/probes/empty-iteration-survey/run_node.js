// ECMAScript battery runner (Node's V8 Irregexp).
const fs = require('fs'), path = require('path');
for (const line of fs.readFileSync(path.join(__dirname, 'battery.tsv'), 'utf8').split('\n')) {
  if (!line.trim() || line.startsWith('#')) continue;
  const [id, pat, subj] = line.replace(/\r$/, '').split('\t');
  let r;
  try {
    const m = new RegExp(pat).exec(subj);
    r = m === null ? 'nomatch' : `span=${m.index},${m.index + m[0].length} g1=${m[1] === undefined ? 'unset' : "'" + m[1] + "'"}`;
  } catch (e) { r = 'error: ' + e.message; }
  console.log(`node ${process.version} (V8 ${process.versions.v8})\t${id}\t${r}`);
}
