// The dev server has to serve the page from source and everything else from the .NET web root.
// Neither is something a green production build can tell you, and both were broken until S71's
// review measured them:
//
//   - `build.outDir` IS the .NET web root, which is also the middleware's last fallback root, so a
//     previous `vite build` sitting in it answered for the page: `npm run dev` showed the last
//     production bundle (1,750 bytes, no `@vite/client`) with no module graph and no hot reload.
//   - Vite's own html fallback rewrites the URL to `/index.html` for any request whose `Accept`
//     header contains `text/html` or the wildcard catch-all, which is what `new Worker()` sends, so
//     a post-hook middleware never saw `/worker.js`: the dev server returned 1,706 bytes of HTML
//     where the engine's worker should have been, and the demo could not boot at all in dev.
//
// The built page is gitignored, so this test writes its own shadow copy rather than relying on one
// being there: on a fresh checkout there is nothing in the web root to shadow the source page, and
// a test that needs a previous build to go red is a test CI can never fail.
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';

import { afterAll, beforeAll, expect, test } from 'vitest';
import { createServer, type ViteDevServer } from 'vite';

// Both from the config, so the test cannot drift from the thing it guards: `webRoot` is the build
// output directory and the middleware's last fallback root at once, which is the whole problem.
import { isOwnBuildOutput, webRoot } from '../vite.config';

// Shaped like what `vite build` writes, because the assertions below tell the two pages apart by
// the hashed bundle a built page references and a source page does not.
const SHADOW = '<!doctype html><!-- a previous production build -->\n'
    + '<script type="module" crossorigin src="./assets/index-shadow.js"></script>\n';
const shadows = ['index.html', 'assets/index-shadow.js'].map((relative) => join(webRoot, relative));
const written: string[] = [];
const created: string[] = [];

let server: ViteDevServer;
let base: string;

beforeAll(async () => {
    for (const path of shadows) {
        if (existsSync(path)) continue;
        const directory = dirname(path);
        if (!existsSync(directory)) created.push(directory);
        mkdirSync(directory, { recursive: true });
        writeFileSync(path, SHADOW);
        written.push(path);
    }

    // Port 0: the suite must not fight a dev server the developer already has open.
    server = await createServer({ server: { port: 0 }, logLevel: 'error' });
    await server.listen();
    const local = server.resolvedUrls?.local[0];
    expect(local, 'the dev server reported no local URL').toBeDefined();
    base = local as string;
});

afterAll(async () => {
    await server?.close();
    // Only what this test made: a real build's output is not ours to delete, and an `assets/` left
    // behind is worse than untidy - tools/build-demo-web.ps1 checks for that directory to decide
    // whether the bundle landed.
    for (const path of written) rmSync(path, { force: true });
    for (const directory of created) rmSync(directory, { force: true, recursive: true });
});

test.each([
    ['index.html', true],
    ['Index.html', true],
    ['assets/index-CxNDFCqg.js', true],
    ['Assets\\index-CxNDFCqg.css', true],
    ['worker.js', false],
    ['examples.json', false],
    ['checks.html', false],
    ['_framework/dotnet.js', false],
    ['assets.json', false],
])('%s is this project\'s own build output: %s', (relative, own) => {
    expect(isOwnBuildOutput(relative)).toBe(own);
});

test.each(['', 'index.html', 'Index.html'])(
    'GET /%s is the page from source, not the last build',
    async (path) => {
        const html = await (await fetch(base + path, { headers: { Accept: 'text/html' } })).text();
        expect(html, 'did not come from the module graph').toContain('/src/main.ts');
        expect(html, 'was not transformed by Vite').toContain('@vite/client');
    },
);

test.each([
    // The spellings NTFS resolves to the same file behind the middleware's back. Either answer is
    // acceptable - Vite's own 404, or the page from source - as long as it is not the last build.
    'INDEX~1.HTM',
    'Index.html%20',
    './index.html',
])('GET /%s cannot reach the last build by another spelling', async (path) => {
    const body = await (await fetch(base + path, { headers: { Accept: 'text/html' } })).text();
    expect(body).not.toContain('a previous production build');
    expect(body, 'a hashed bundle means this came from a build, not from source').not.toMatch(
        /assets\/index-/,
    );
});

test('a malformed percent escape is Vite\'s 404, not a 500 out of the middleware', async () => {
    // decodeURIComponent throws URIError on `%zz`, and an exception here becomes an internal error.
    const response = await fetch(base + '%zz');
    expect(response.status).toBe(404);
});

test.each([
    ['worker.js', "self.addEventListener('message'"],
    ['examples.json', '"pattern"'],
])('GET /%s comes from the web root even for a wildcard Accept', async (path, marker) => {
    // The header `new Worker()` and `fetch()` send, which is the one that used to be answered with
    // the HTML page.
    const response = await fetch(base + path, { headers: { Accept: '*/*' } });
    const body = await response.text();
    expect(response.headers.get('content-type')).not.toContain('text/html');
    expect(body).toContain(marker);
});
