/// <reference types="vite/client" />

import type { Demo, spawnEngineWorker } from './src/demo';
import type { createPool } from './src/lib/pool';
import type { ShallowUnwrapRef } from 'vue';

declare global {
    interface Window {
        /**
         * The page's own state, for a machine to read.
         *
         * checks.html - the browser-leg harness - writes `pattern`/`flags`/`subject` and reads
         * `busy`, `view`, `matches` and the rest back, so this member is the page's contract and not
         * a debugging aid: renaming anything on it breaks the harness rather than a console session.
         * `ShallowUnwrapRef` is what `proxyRefs` returns, so the type here says the refs are already
         * unwrapped, which is how the harness uses them.
         */
        __demo?: ShallowUnwrapRef<Demo>;

        /**
         * What checks.html needs to build a pool of its own, for the respawn measurement. Part of
         * the same contract as `__demo` and for the same reason: the bundle is one hashed file per
         * build, so the harness cannot import a module of the page's.
         */
        __demoInternals?: {
            readonly createPool: typeof createPool;
            readonly spawnEngineWorker: typeof spawnEngineWorker;
        };
    }
}
