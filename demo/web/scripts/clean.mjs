// Removes this project's own build output from the .NET web root, and nothing else.
//
// `vite build` writes into a directory that is also a source directory - it holds worker.js,
// examples.json, checks.html and .nojekyll, which are hand-written and committed - so `emptyOutDir`
// is off. Without a clean step the hashed files under assets/ would accumulate, one set per build,
// and every one of them would be published, deployed and counted into the size baseline that
// tools/run-wasm-smoke.ps1 prints.

import { rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const webRoot = new URL('../../FuzzyRegex.Demo.Wasm/wwwroot/', import.meta.url);

// Exactly what `vite build` writes, listed rather than globbed: this script deletes things, and a
// glob over a directory that also holds hand-written files is one typo away from deleting them.
for (const generated of ['index.html', 'assets']) {
    rmSync(fileURLToPath(new URL(generated, webRoot)), { recursive: true, force: true });
}
