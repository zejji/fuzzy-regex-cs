// Does the snippet panel's own trim strip exactly what `String.Trim()` strips, and does it do it in
// linear time?
//
//     node tools/probes/demo-trim-matches-dotnet.mjs            (from the repository root)
//
// S73 chunk 3. `snippet.ts` has a `trimmed()` of its own because JavaScript's `trim()` is not
// .NET's: the two disagree about U+0085 and U+FEFF, and `DemoEngine` trims the mode, each flag
// token, a list's name and each of its words. A snippet that trimmed differently would print a walk
// under a partial answer.
//
// Two things are settled here, both by running rather than by reasoning:
//
//  1. THE SET. A C# program prints every BMP code point `char.IsWhiteSpace` is true for; the page's
//     generator is then driven over all 65,536 of them, one per named-list name, and the two answers
//     compared. The .NET side is measured, not copied from a table.
//  2. THE COST. `trimmed()` is an index walk from each end. Written as one regex - `^[ws]+|[ws]+$` -
//     the trailing alternative restarts inside every interior run of whitespace, which is quadratic,
//     and the panel regenerates the snippet on every keystroke into a flag box nothing caps. Both
//     forms are timed below over the same inputs. The absolute milliseconds are machine-dependent;
//     the shape (four times per doubling, against flat) is the finding.
//
// The C# project is made in the system temporary directory and not in the repository, as
// `demo-snippet-compiles.mjs` does and for the same reason.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
const webRoot = join(repoRoot, 'demo/web');
const project = join(tmpdir(), 'fuzzyregex-trim-probe');

const { createServer } = await import(
    pathToFileURL(createRequire(join(webRoot, 'package.json')).resolve('vite')).href
);
const server = await createServer({ root: webRoot, configFile: false, logLevel: 'error' });
const { toCSharp } = await server.ssrLoadModule('/src/lib/snippet.ts');
await server.close();

function run(command, args, cwd = repoRoot) {
    const result = spawnSync(command, args, { cwd, encoding: 'utf8' });
    return `${result.stdout ?? ''}${result.stderr ?? ''}`.trim();
}

if (!existsSync(project)) {
    mkdirSync(project, { recursive: true });
    run('dotnet', ['new', 'console', '-o', project]);
}

// --- 1. the set -----------------------------------------------------------------------------

writeFileSync(
    join(project, 'Program.cs'),
    `for (int code = 0; code <= 0xFFFF; code++)
{
    if (char.IsWhiteSpace((char)code))
    {
        Console.Write($"{code:x4},");
    }
}
`,
    'utf8',
);

const printed = run('dotnet', ['run', '--project', project, '--nologo', '-v', 'quiet'], project);
const dotnet = new Set(
    printed
        .split(',')
        .filter((hex) => hex !== '')
        .map((hex) => Number.parseInt(hex, 16)),
);
console.log(`char.IsWhiteSpace over the BMP: ${dotnet.size} code points`);
console.log(printed);

let disagreements = 0;
let skipped = 0;
for (let code = 0; code <= 0xffff; code++) {
    const char = String.fromCharCode(code);

    // A list name cannot hold a colon or a line separator and a word cannot hold a word separator;
    // those characters are read by the block parser before the trim ever sees them.
    if (char === ':' || char === '\n' || char === '\r' || char === ',' || char === ';') {
        skipped += 1;
        continue;
    }

    const emitted = toCSharp({
        pattern: 'a',
        flags: '',
        subject: 's',
        mode: '',
        replacement: '',
        namedLists: `${char}f${char}: w`,
    });

    // A `"` is doubled inside the verbatim literal the name is printed as, so it is kept but not
    // spelt the way `kept` looks for. Checked by its stripped form only.
    const stripped = emitted.includes('[@"f"]');
    const kept = char === '"' ? !stripped : emitted.includes(`[@"${char}f${char}"]`);
    if (dotnet.has(code) ? !stripped : !kept) {
        disagreements += 1;
        console.log(`U+${code.toString(16).padStart(4, '0')}: expected ${dotnet.has(code) ? 'stripped' : 'kept'}`);
    }
}
console.log(`${0x10000 - skipped} code units driven through the generator, ${disagreements} disagreements`);

// --- 2. the cost ----------------------------------------------------------------------------

// The version `trimmed()` replaced, kept here as the thing being measured against. U+00A0 is a
// character the flag box's separators do not split on, so the whole run reaches one trim.
const CLASS = '\\t-\\r \\u0085\\u00a0\\u1680\\u2000-\\u200a\\u{2028}\\u{2029}\\u202f\\u205f\\u3000';
const OLD_TRIM = new RegExp(`^[${CLASS}]+|[${CLASS}]+$`, 'gu');

for (const size of [12500, 25000, 50000, 100000]) {
    const flags = `a${'\u{a0}'.repeat(size)}b`;

    let started = performance.now();
    flags.replace(OLD_TRIM, '');
    const regex = Math.round(performance.now() - started);

    started = performance.now();
    toCSharp({ pattern: 'a', flags, subject: 's', mode: '', replacement: '', namedLists: '' });
    const walk = Math.round(performance.now() - started);

    console.log(`${size} characters: the regex ${regex} ms, the whole snippet with the index walk ${walk} ms`);
}
