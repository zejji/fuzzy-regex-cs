// The contract between the page and the engine, in one file.
//
// Every shape here is the TypeScript side of something that exists in C#: DemoEngine.cs serialises
// DemoAnswer with a camelCase naming policy, and wwwroot/worker.js wraps it in the request/reply
// envelope below. Writing them out is what makes the page type-check against the engine rather than
// against a guess: a renamed field in DemoEngine.cs is then a build error here, not a blank column
// on a deployed page.

/** One span in the subject, in UTF-16 code units. Mirrors `DemoSpan`. */
export interface Span {
    readonly index: number;
    readonly length: number;
}

/** The per-error-type cost of a fuzzy match. Zero everywhere for an exact one. Mirrors `DemoCounts`. */
export interface Counts {
    readonly substitutions: number;
    readonly insertions: number;
    readonly deletions: number;
}

/**
 * One capturing group's result. Mirrors `DemoGroup`.
 *
 * `captures` is the full capture list - every repetition, not only the last - which is what makes a
 * repeated group worth showing at all.
 */
export interface Group {
    readonly number: number;
    readonly name: string;
    /** False for a group that did not take part. Its index and length then mean nothing. */
    readonly success: boolean;
    readonly index: number;
    readonly length: number;
    readonly captures: readonly Span[];
}

/** One match: its span, its cost, and every group. Mirrors `DemoMatch`. */
export interface Match extends Span {
    readonly counts: Counts;
    readonly groups: readonly Group[];
}

/**
 * The whole answer. Mirrors `DemoAnswer`, whose null members are omitted from the JSON, so a
 * failure is literally `{"error": "..."}` and every member here is optional.
 */
export interface Answer {
    readonly matches?: readonly Match[];
    /** The engine stopped at its own cap, so this is not the whole answer. */
    readonly truncated?: boolean;
    readonly error?: string;
}

/**
 * What {@link import('./lib/pool').createPool} hands back.
 *
 * `aborted` is the pool's own addition and never comes from the engine: it separates "we killed
 * this worker" from "the engine said no". They look identical from the page - nobody got an answer
 * - but they are opposite claims about the engine.
 */
export interface Reply extends Answer {
    readonly aborted?: boolean;
}

/** The three inputs, which are the whole case. */
export interface Inputs {
    readonly pattern: string;
    readonly flags: string;
    readonly subject: string;
}

/** A question for the engine, as worker.js expects it. */
export interface WorkerRequest extends Inputs {
    readonly requestId: number;
}

/** Everything worker.js ever posts back. */
export type WorkerMessage =
    | { readonly ready: true }
    | { readonly ready: false; readonly error: string }
    | { readonly requestId: number; readonly json: string };

/**
 * The parts of a `MessageEvent` or `ErrorEvent` the pool reads.
 *
 * One shape for both because the pool treats them the same way - a worker that errored and a worker
 * that reported a failed boot both owe every waiting question an answer.
 */
export interface WorkerEventLike {
    // Explicitly `| undefined`, under `exactOptionalPropertyTypes`: a real MessageEvent always has
    // a `data` member and a real ErrorEvent always has a `message` one, so what arrives here is a
    // present member holding undefined rather than an absent member.
    readonly data?: WorkerMessage | undefined;
    readonly message?: string | undefined;
}

/**
 * The parts of a `Worker` the pool uses.
 *
 * Narrowed to these three deliberately: the pool is driven by a fake in the unit tests, where there
 * is no Worker and no runtime to boot, and a fake that had to implement all of `Worker` would be a
 * test of the fake.
 */
export interface WorkerLike {
    addEventListener(kind: 'message' | 'error', listener: (event: WorkerEventLike) => void): void;
    postMessage(request: WorkerRequest): void;
    terminate(): void;
}

/** The engine's boot verdict. */
export type BootState =
    | { readonly ok: true }
    | {
          readonly ok: false;
          readonly error: string;
          /** True when this worker was killed rather than having failed to boot. */
          readonly aborted: boolean;
      };

/** One worked example from `examples.json`, which is the guided tour. */
export interface Example extends Inputs {
    readonly title: string;
    readonly note: string;
}
