/// <reference types="vitest/config" />
import { existsSync, realpathSync, statSync } from 'node:fs';
import { createReadStream } from 'node:fs';
import { extname, join, normalize, relative as relativeTo } from 'node:path';
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
export const webRoot = here('../FuzzyRegex.Demo.Wasm/wwwroot');

/**
 * This project's directory, which is the Vite root and the directory this config lives in.
 *
 * Vite defaults its root to `process.cwd()`, so a dev server created without one serves whatever
 * directory the caller happened to start in. Measured 2026-09-20: started from the repository root,
 * the same server resolved its root there, found no `index.html` and answered every request with an
 * empty 404 - which is what `tests/dev-server.test.ts` was reading as five failures. Exported so
 * that test can pin it and stop depending on its caller. The config file needs no pin of its own:
 * Vite looks for it under the root it settled on, so pinning the root pins both (measured the same
 * day from the repository root - `configFile` came back as this file and `vite:vue` was loaded).
 */
export const projectRoot = here('.');

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

/**
 * This project's own build output, which a fallback root must never answer for.
 *
 * Every fallback root above holds a previous `vite build`: the web root IS the output directory, and
 * a publish copies it. Serving those in dev means `npm run dev` shows the last production bundle,
 * with no module graph and no hot reload - measured 2026-09-19, the dev server returned the 1,750
 * byte built page rather than the 1,651 byte source entry, with neither `@vite/client` nor
 * `/src/main.ts` in it. Declining them here hands the request back to Vite, which is the only one
 * that should serve the page and its modules.
 *
 * Case-insensitive, because the lookup it guards is: `existsSync` on NTFS answers for `Index.html`
 * and `Assets/`, so a case-sensitive guard declines a request the loop below would then serve.
 */
export const isOwnBuildOutput = (relative: string) =>
    /^(index\.html|assets[\\/])/i.test(relative);

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
 * Installed as a PRE hook - `server.middlewares.use` in the body, not the `return () => ...` post
 * hook - because Vite's own `htmlFallbackMiddleware` rewrites `req.url` to `/index.html` for any
 * request whose `Accept` header contains `text/html` or the wildcard catch-all, which is exactly
 * what `new Worker()` and `fetch()` send. A post hook therefore never sees `/worker.js` at all:
 * measured 2026-09-19, that request returned 1,706 bytes of HTML, so the dev server could not
 * boot the engine's worker. Running first costs nothing, because the only things in the fallback
 * roots that Vite also serves are this project's own build output, which `isOwnBuildOutput` declines.
 */
const publishedAssets = (): Plugin => ({
    name: 'fuzzy-regex-demo:published-assets',
    apply: 'serve',
    configureServer(server) {
        server.middlewares.use((request, response, next) => {
            const url = (request.url ?? '/').split('?')[0] ?? '/';
            let relative: string;
            try {
                relative = normalize(decodeURIComponent(url)).replace(/^[\\/]+/, '');
            } catch {
                // A malformed percent escape (`/%zz`) is not a path. Handing it back lets Vite
                // answer it the way it answers any unknown URL, with a 404; letting the URIError
                // out of here turned that into a 500.
                return next();
            }
            // A request that climbs out of the root is refused rather than resolved: this
            // middleware serves a publish directory, and `..` in a URL is never a real asset.
            if (relative === '' || relative.startsWith('..')) return next();
            if (isOwnBuildOutput(relative)) return next();

            for (const root of devFallbackRoots) {
                const file = join(root, relative);
                if (!existsSync(file) || !statSync(file).isFile()) continue;

                // The guard above tests the name in the URL; this tests the name the file system
                // actually has. NTFS answers `existsSync` for a file's 8.3 short name and for an
                // alternate data stream, so `/INDEX~1.HTM` and `/worker.js::$DATA` reach a file the
                // spelling in the URL does not name. Measured 2026-09-19: the first served the last
                // production build past the guard, the second served bytes Vite itself refuses.
                let canonical: string;
                try {
                    canonical = relativeTo(root, realpathSync.native(file));
                } catch {
                    continue;
                }
                if (canonical !== relative && isOwnBuildOutput(canonical)) return next();
                if (canonical.startsWith('..')) continue;

                response.setHeader(
                    'Content-Type',
                    CONTENT_TYPES[extname(file).toLowerCase()] ?? 'application/octet-stream',
                );
                createReadStream(file).pipe(response);
                return;
            }

            return next();
        });
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
