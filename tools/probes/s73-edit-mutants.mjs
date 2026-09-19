// S73 chunk 5c. Does anything actually test the per-edit underlay?
//
// The tests for it were written alongside the code rather than watched failing first, so each line
// that carries a decision gets a plausible wrong version planted in it, the suite that claims to
// cover it is run, and the file is put back. A mutant that survives is a line nothing tests.
//
// Run from the repo root:  node tools/probes/s73-edit-mutants.mjs
// It restores every file it touches, including on a failure; `git diff --stat` afterwards should be
// empty. The two C# mutants each rebuild the test project, so the whole run takes a few minutes.

import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const repo = new URL('../../', import.meta.url);
const web = new URL('demo/web/', repo);

const HIGHLIGHT = 'demo/web/src/lib/highlight.ts';
const ENGINE = 'demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs';

/** [what the mutant claims, file, the line as it reads, the line made wrong, which suite should catch it] */
const MUTANTS = [
    [
        'a deletion at the very end of the match is dropped',
        HIGHLIGHT,
        'if (at < start || at > end) continue;',
        'if (at < start || at >= end) continue;',
        'web',
    ],
    [
        'two deletions in one place draw one mark',
        HIGHLIGHT,
        'for (let i = carets.get(at) ?? 0; i > 0; i--) {',
        'for (let i = Math.min(1, carets.get(at) ?? 0); i > 0; i--) {',
        'web',
    ],
    [
        'a position outside the match is drawn anyway',
        HIGHLIGHT,
        'if (at < start || at >= end) return;',
        'if (at < start - 1000 || at >= end + 1000) return;',
        'web',
    ],
    [
        'a position on the low half of a surrogate pair is sliced where it lands',
        HIGHLIGHT,
        'return low && high ? at - 1 : at;',
        'return low && high ? at : at;',
        'web',
    ],
    [
        'every character is one code unit wide',
        HIGHLIGHT,
        'return codepoint !== undefined && codepoint > 0xffff ? 2 : 1;',
        'return codepoint !== undefined && codepoint > 0xffffff ? 2 : 1;',
        'web',
    ],
    [
        "upstream's deletion positions are passed through unshifted",
        ENGINE,
        'deletions.Add(changes.Deletions[i] - i);',
        'deletions.Add(changes.Deletions[i]);',
        'dotnet',
    ],
    [
        // Deleting the guard outright does not compile - `counts` is its only use, and an unused
        // parameter is an analyzer error here - so the mutant is a guard that never fires.
        'an exact match carries an empty edits object',
        ENGINE,
        'if (counts.Total == 0)',
        'if (counts.Total < 0)',
        'dotnet',
    ],
];

const SUITES = {
    web: () =>
        execFileSync('npx', ['vitest', 'run', 'tests/highlight.test.ts', 'tests/page.test.ts'], {
            cwd: web,
            encoding: 'utf8',
            shell: true,
        }),
    dotnet: () =>
        execFileSync(
            'dotnet',
            ['test', 'tests/FuzzyRegex.Tests', '--', '--treenode-filter', '/*/*/DemoEngineContractTests/*'],
            { cwd: repo, encoding: 'utf8', shell: true },
        ),
};

/** The names a run reports as failing, from either runner's output. */
function failures(output) {
    const vitest = [...output.matchAll(/FAIL\s+tests\/[\w.]+\s+>\s+(.+)/g)].map((one) => one[1].trim());
    const mtp = [...output.matchAll(/failed\s+([A-Za-z_]\w*)/g)]
        .map((one) => one[1].trim())
        .filter((name) => name !== 'with');
    return [...new Set(vitest.length > 0 ? vitest : mtp)];
}

for (const [claim, relative, from, to, suite] of MUTANTS) {
    const file = fileURLToPath(new URL(relative, repo));
    const original = readFileSync(file, 'utf8');
    if (!original.includes(from)) {
        console.log(`SKIPPED   ${claim}: the line is not in ${relative}`);
        continue;
    }

    writeFileSync(file, original.replace(from, to));
    let verdict;
    try {
        SUITES[suite]();
        verdict = 'SURVIVED - nothing tests this line';
    } catch (error) {
        const named = failures(`${error.stdout ?? ''}${error.stderr ?? ''}`);
        verdict = `killed by ${named.length} test(s): ${named.slice(0, 4).join('; ') || 'unnamed'}`;
    } finally {
        writeFileSync(file, original);
    }
    console.log(`${claim} -> ${verdict}`);
}
