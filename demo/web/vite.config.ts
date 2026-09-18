/// <reference types="vitest/config" />
import { existsSync, statSync } from 'node:fs';
import { createReadStream } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';

const here = (relative: string) => fileURLToPath(new URL(relative, import.meta.url));

/**
 * The .NET project's web root, which is also this project's build output directory.
 *
 * One web root, not two: `worker.js` is the WebAssembly SDK's `WasmMainJSPath` and has to sit beside
 * `_framework/`, and the page has to sit beside the worker. Building into the same folder means the
 * .NET publish gathers the page, the worker and the runtime into one static-asset manifest, which is
 * what `tools/run-wasm-smoke.ps1` checks and what GitHub Pages uploads.
 */
const webRoot = here('../FuzzyRegex.Demo.Wasm/wwwroot');

/**
 * Where `npm run dev` finds the files this project does not build: the runtime under `_framework/`,
 * `worker.js`, `examples.json` and `checks.html`. The publish is preferred over the source web root
 * because `_framework/` only exists after a publish, and a dev session running an older runtime than
 * the page it serves is the confusion this ordering avoids.
 */
const devFallbackRoots = [
    here('../FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot'),
    here('../FuzzyRegex.Demo.Wasm/bin/Debug/net10.0/publish/wwwroot'),
    webRoot,
];

const CONTENT_TYPES: Record<string, string> = {
    '.js': 'text/javascript',
    '.mjs': 'text/javascript',
    '.json': 'application/json',
    '.wasm': 'application/wasm',
    '.html': 'text/html',
    '.css': 'text/css',
    '.map': 'application/json',
};

/**
 * Serves the published .NET assets in dev, for requests Vite itself cannot answer.
 *
 * Returned as a post hook (`return () => ...`) so it is installed *after* Vite's own middlewares:
 * Vite must keep winning for the module graph, and this only ever sees what it declined.
 */
const publishedAssets = (): Plugin => ({
    name: 'fuzzy-regex-demo:published-assets',
    apply: 'serve',
    configureServer(server) {
        return () => {
            server.middlewares.use((request, response, next) => {
                const url = (request.url ?? '/').split('?')[0] ?? '/';
                const relative = normalize(decodeURIComponent(url)).replace(/^[\\/]+/, '');
                // A request that climbs out of the root is refused rather than resolved: this
                // middleware serves a publish directory, and `..` in a URL is never a real asset.
                if (relative === '' || relative.startsWith('..')) return next();

                for (const root of devFallbackRoots) {
                    const file = join(root, relative);
                    if (!existsSync(file) || !statSync(file).isFile()) continue;

                    response.setHeader(
                        'Content-Type',
                        CONTENT_TYPES[extname(file).toLowerCase()] ?? 'application/octet-stream',
                    );
                    createReadStream(file).pipe(response);
                    return;
                }

                return next();
            });
        };
    },
});

export default defineConfig({
    // Relative, so one artefact boots at the repository subpath on GitHub Pages, at the root of a
    // local server and at a fork's preview path. This is the built form of S71's no-`<base href>`
    // decision, which sitting 2 settled by running checks.html green at both layouts.
    base: './',
    plugins: [vue(), tailwindcss(), publishedAssets()],
    // Everything static already lives in the web root and is committed there; a public/ directory
    // here would copy a second set of the same files over them at build time.
    publicDir: false,
    build: {
        outDir: webRoot,
        // The web root also holds hand-written files (`worker.js`, `examples.json`, `checks.html`,
        // `.nojekyll`) and, after a publish, the runtime. Emptying it would delete them. `npm run
        // clean` removes exactly this project's own output instead, which is what stops stale
        // hashed assets accumulating.
        emptyOutDir: false,
        assetsDir: 'assets',
        sourcemap: true,
    },
    test: {
        environment: 'jsdom',
        // The page writes the case into the address bar, and a test that asserts what it wrote has
        // to know what "the current page" is. Trailing slash: a demo served from a repository
        // subpath is the deployment that actually happens.
        environmentOptions: { jsdom: { url: 'http://localhost/demo/' } },
        include: ['tests/**/*.test.ts'],
    },
});
