// Serves a published demo over HTTP, so the browser probes beside this file have something to drive.
//
// S73's probes all say "against a served build (`.scratch/serve` on port 8213)" and the script that
// did the serving was scratch, so none of them could be re-run from the repository alone. This is
// that missing half, written down: the demo needs a PUBLISH and not the source tree, because
// `demo/FuzzyRegex.Demo.Wasm/wwwroot` has no `_framework` and only a publish runs the engine.
//
// Run, from the repository root:
//
//   pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/s74-publish
//   node tools/probes/serve-demo-publish.mjs --root .scratch/s74-publish/wwwroot
//
// It exits on its own after 30 minutes. A probe server that outlives the sitting that started it is
// a file lock on the publish directory and a port nobody remembers taking (STATE.md has carried a
// line about stray servers since S71), so the timeout is the default rather than an option.

import { createReadStream, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';

const argument = (name, fallback) => {
    const at = process.argv.indexOf(`--${name}`);
    return at === -1 ? fallback : (process.argv[at + 1] ?? fallback);
};

const root = resolve(argument('root', '.scratch/s74-publish/wwwroot'));
const port = Number(argument('port', '8213'));
const minutes = Number(argument('minutes', '30'));

// `.wasm` is the one that matters: served as anything else, `WebAssembly.instantiateStreaming`
// refuses the response and the page boots to a blank frame rather than to a wrong answer.
const TYPES = {
    '.css': 'text/css',
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript',
    '.json': 'application/json',
    '.mjs': 'text/javascript',
    '.png': 'image/png',
    '.svg': 'image/svg+xml',
    '.wasm': 'application/wasm',
    '.webmanifest': 'application/manifest+json',
};

const server = createServer((request, response) => {
    const path = decodeURIComponent(new URL(request.url ?? '/', 'http://localhost').pathname);
    // Resolved and then checked against the root, so `..` in a request cannot reach outside the
    // publish. This serves a directory of build output to a browser on this machine and nothing
    // else, but a traversal is not the kind of hole worth leaving open because the stakes are low.
    const file = normalize(join(root, path === '/' ? 'index.html' : path));
    if (!file.startsWith(root)) {
        response.writeHead(403).end('outside the served root');
        return;
    }

    let size = 0;
    try {
        size = statSync(file).size;
    } catch {
        response.writeHead(404).end('not found');
        return;
    }

    response.writeHead(200, {
        'content-type': TYPES[extname(file)] ?? 'application/octet-stream',
        'content-length': size,
        // The probes defeat this with a `?v=` anyway; saying it here means a rebuild between two
        // probe runs cannot serve one of them the previous bundle.
        'cache-control': 'no-store',
    });
    createReadStream(file).pipe(response);
});

server.listen(port, () => {
    console.log(`serving ${root} on http://localhost:${port} for ${minutes} minutes`);
});

setTimeout(() => {
    server.close();
    process.exit(0);
}, minutes * 60_000);
