/**
 * The stylesheet as the browser receives it, not as it is written.
 *
 * `layout.test.ts` used to assert over `src/styles.css` read as a string, which made it fail on
 * edits that change nothing: merging
 *
 *     .input-pane { overflow-y: auto } .results-pane { overflow-y: auto }
 *
 * into one selector list - identical CSS, and what a minifier does anyway - failed two tests, and
 * reordering two rules inside the media block failed two more. Worse in the other direction: the
 * source says `@apply min-h-dvh`, and only the compiled output says whether that became
 * `min-height: 100vh`, which is the bug that shipped (chunk 2, finding 3).
 *
 * So the tests read the build. Vite's JavaScript API compiles the same entry the production build
 * does, with the same Tailwind plugin, in memory: `write: false` keeps it out of `wwwroot`, and
 * `configFile: false` keeps `vite.config.ts` - whose `outDir`, `publicDir` and dev middleware are
 * about shipping the page - out of a test's way.
 *
 * Verified again 2026-09-20 against `npm run build`: byte-identical output, 27,486 characters, md5
 * `3f31b6b17730c5af3d551096b4e09b13`, in about 130 ms. (First taken 2026-09-19 at 24,144 characters;
 * the stylesheet has grown since.) `tools/probes/demo-built-css-matches-production.test.ts` is that
 * check, with the commands to re-run it - the hash moves whenever the stylesheet does, and what it
 * is for is that the two routes agree on the same tree, not that either equals a number written
 * here.
 *
 * One thing it does NOT see: a `<style>` block inside a single-file component. Those reach the
 * bundle through the JavaScript graph that starts at `index.html`, and the entry here is the
 * stylesheet. `layout.test.ts > no component brings a stylesheet of its own` is what keeps that
 * from mattering.
 *
 * The cache below means one compile per test file, not one per test.
 */
import { join } from 'node:path';

import tailwindcss from '@tailwindcss/vite';
import { build, type Rollup } from 'vite';

const projectRoot = join(import.meta.dirname, '..');

let compiled: Promise<string> | null = null;

/** The compiled stylesheet, shared by every caller in a test file. */
export function builtCss(): Promise<string> {
    compiled ??= compile();
    return compiled;
}

async function compile(): Promise<string> {
    const result = await build({
        root: projectRoot,
        configFile: false,
        // Tailwind's `@source` directives in the stylesheet are resolved relative to it, so the
        // plugin scans the same files here as it does in the production build - including the
        // `@source not '../tests'` that keeps a class named in a test comment out of the bundle.
        plugins: [tailwindcss()],
        logLevel: 'silent',
        build: {
            write: false,
            rollupOptions: { input: join(projectRoot, 'src/styles.css') },
        },
    });

    const output: readonly (Rollup.OutputChunk | Rollup.OutputAsset)[] =
        (Array.isArray(result) ? result[0]?.output : (result as Rollup.RollupOutput).output) ?? [];

    for (const emitted of output) {
        if (emitted.type !== 'asset' || !emitted.fileName.endsWith('.css')) continue;
        return typeof emitted.source === 'string' ? emitted.source : new TextDecoder().decode(emitted.source);
    }

    throw new Error('the build produced no stylesheet');
}
