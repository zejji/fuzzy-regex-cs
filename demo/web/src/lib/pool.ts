// The page's side of the worker contract: one worker serving, one booted and idle behind it.
//
// terminate() is the PRIMARY control here and not an error path (S70's finding, recorded in
// DemoEngine.MatchTimeout's remarks): the engine's own MatchTimeout is a MATCHING budget, so a
// pattern whose repeat counts multiply out - `(((a{100}){100}){100}){100}`, well inside the pattern
// cap - wedges FuzzyRegex's CONSTRUCTOR, where no clock is running and no answer is ever produced.
// Killing the worker is the only thing that recovers that, which is why the Stop button below is a
// first-class control rather than a panic button.
//
// The spare exists because a terminate() then costs a fresh .NET WebAssembly boot before the next
// question can be answered. The ROADMAP notes there is no published figure for that; the spare makes
// the number stop mattering, and checks.html measures it both ways so the claim is not a guess.

import type { BootState, Inputs, Reply, WorkerLike } from '../types';

import { parseReply } from './shapes';

/** What a pool tells its owner whenever its shape changes. */
export interface PoolState {
    readonly generation: number;
    readonly hasSpare: boolean;
}

export interface Pool {
    readonly generation: number;
    readonly hasSpare: boolean;
    /** Settles when a worker has booted, with `{ok: true}` or `{ok: false, error}`. */
    readonly ready: Promise<BootState>;
    /** Asks the serving worker. The answer is the engine's JSON, already parsed. */
    ask(request: Inputs): Promise<Reply>;
    /**
     * Kills the serving worker mid-question and takes over with the spare. The in-flight promise
     * settles with `{aborted: true}` rather than hanging.
     */
    stop(why?: string): void;
    /** Shuts the pool down. Used by the checks page; the demo page itself never stops. */
    dispose(): void;
}

export interface PoolOptions {
    /**
     * Makes a new worker. Injected rather than hard-coded so the pool can be driven by a fake in the
     * unit tests, where there is no Worker and no runtime to boot.
     */
    spawn: () => WorkerLike;
    /**
     * Keep a warm spare. False is not a configuration anyone should run: it exists so checks.html
     * can measure what the spare is worth, and a measurement with no control is not a measurement.
     */
    spare?: boolean;
    /**
     * Told whenever the pool's state changes, so a UI can show "starting", "ready" or a boot failure
     * without polling.
     */
    onChange?: (state: PoolState) => void;
}

interface Client {
    readonly ready: Promise<BootState>;
    ask(request: Inputs): Promise<Reply>;
    terminate(why: string): void;
}

/**
 * Wraps one Worker in the request/reply contract worker.js speaks: every request carries a
 * requestId and every reply carries it back, so an answer to a question the user has moved on from
 * can be dropped rather than rendered as the answer to the new one.
 */
function createClient(worker: WorkerLike): Client {
    const waiting = new Map<number, (reply: Reply) => void>();
    let nextId = 1;
    let settleReady!: (state: BootState) => void;
    let failed: string | null = null;

    const ready = new Promise<BootState>((resolve) => {
        settleReady = resolve;
    });

    // A worker that dies - terminated, or a boot that threw - must settle every promise waiting on
    // it. Without this the page's "matching..." state is permanent and indistinguishable from a slow
    // match, which is the one failure mode a demo about responsiveness cannot have.
    // `aborted` separates "we killed this worker" from "this worker could not boot". They look
    // identical from here - both end with nobody getting an answer - but they are opposite claims
    // about the engine, and a page that shows the first as the second tells a visitor the demo is
    // broken while the promoted spare answers correctly behind the message.
    const failEveryone = (why: string, aborted = false) => {
        failed = why;
        settleReady({ ok: false, error: why, aborted });
        for (const [, settle] of waiting) settle({ error: why, aborted: true });
        waiting.clear();
    };

    worker.addEventListener('error', (event) => failEveryone(`worker error: ${event.message ?? event}`));

    worker.addEventListener('message', (event) => {
        const data = event.data;
        if (data === undefined) return;

        if ('ready' in data) {
            if (data.ready === false) {
                failEveryone(`the engine failed to load: ${data.error}`);
                return;
            }
            settleReady({ ok: true });
            return;
        }

        const settle = waiting.get(data.requestId);
        if (settle === undefined) return;
        waiting.delete(data.requestId);
        // Parsed and checked, never cast: a throw here escapes into the listener, where nobody
        // catches it and - because the waiting entry has already been taken - nobody is ever
        // settled either. `parseReply` returns the failure as an answer instead, so a reply the
        // page cannot read ends the question rather than hanging it.
        settle(parseReply(data.json));
    });

    return {
        ready,
        ask(request) {
            if (failed !== null) return Promise.resolve({ error: failed, aborted: true });

            const requestId = nextId++;
            return new Promise<Reply>((resolve) => {
                waiting.set(requestId, resolve);
                worker.postMessage({ requestId, ...request });
            });
        },
        terminate(why) {
            worker.terminate();
            failEveryone(why, true);
        },
    };
}

/** A worker and a warm spare behind it. */
export function createPool({ spawn, spare = true, onChange = () => {} }: PoolOptions): Pool {
    let current = createClient(spawn());
    let standby: Client | null = spare ? createClient(spawn()) : null;
    let generation = 0;

    const announce = () => onChange({ generation, hasSpare: standby !== null });

    void current.ready.then(announce);
    announce();

    /**
     * The engine's boot verdict, followed through replacements.
     *
     * A worker killed before it finished booting has told us nothing about whether the engine loads:
     * it was killed because the visitor typed again or pressed Stop, which can easily happen during
     * the second or two a WebAssembly runtime takes to come up. Reporting that as a boot failure
     * latches a permanent "engine failed to load" over a page whose promoted spare is answering
     * normally, so the verdict is taken from whichever worker survives to give one.
     *
     * The `current.ready === pending` test is the stop: once nothing replaces the killed worker -
     * after dispose() - the same settled promise would otherwise be awaited forever.
     */
    const ready = (async () => {
        for (;;) {
            const pending = current.ready;
            const state = await pending;
            const killed = state.ok === false && state.aborted;
            if (!killed || current.ready === pending) return state;
        }
    })();

    /** Promotes the spare (or starts a worker when there is none) and boots a replacement. */
    const replace = (why: string) => {
        current.terminate(why);
        generation++;

        // The promotion is synchronous - the spare is already booted, so the next question can be
        // asked on the very next line - and the replacement boot is deliberately NOT awaited. A page
        // that waited for the new spare before accepting the next question would have exchanged one
        // wait for another.
        current = standby ?? createClient(spawn());
        standby = spare ? createClient(spawn()) : null;
        announce();
        if (standby !== null) void standby.ready.then(announce);
    };

    return {
        get generation() {
            return generation;
        },
        get hasSpare() {
            return standby !== null;
        },
        ready,
        ask(request) {
            return current.ask(request);
        },
        stop(why = 'stopped') {
            replace(why);
        },
        dispose() {
            current.terminate('disposed');
            if (standby !== null) standby.terminate('disposed');
            standby = null;
        },
    };
}
