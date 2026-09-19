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
    /**
     * The subject ran out before the pattern did. Present only in partial mode, and only on a match
     * that is partial - so `undefined` means "a complete match", not "we did not ask".
     */
    readonly partialMatch?: boolean;
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
    /** The whole rewritten subject. Replace mode only. */
    readonly replaced?: string;
    /**
     * Where in the PATTERN the parse failed, so the page can put a caret under it.
     *
     * Absent whenever no single character of the pattern is at fault - a cap refusal, a misspelt
     * flag, a timeout, or an error raised against the replacement template, whose positions index
     * the template and would point at an unrelated character here. See `DemoAnswer.ErrorOffset`.
     */
    readonly errorOffset?: number;
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

/**
 * The six inputs, which are the whole case.
 *
 * Six and not three since v2: a replace sample is not a case without its template, and a named-list
 * sample is not one without its lists. Everything here is a string because everything crossing into
 * the engine is a string - `Interop.Run` takes six of them - and an empty one is what the engine
 * reads as "not asked".
 */
export interface Inputs {
    readonly pattern: string;
    readonly flags: string;
    readonly subject: string;
    /** `''` for the ordinary walk, `'partial'` or `'replace'`. The engine names an unknown one. */
    readonly mode: string;
    /** The replacement template, in upstream's language (`\1`, `\g<name>`). Replace mode only. */
    readonly replacement: string;
    /** The pattern's `\L<name>` lists, one per line, as `name: word, word`. */
    readonly namedLists: string;
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

/**
 * One worked example from `examples.json`, which is the guided tour.
 *
 * It does NOT extend {@link Inputs}: a row names the boxes it needs and stays silent about the
 * rest, and {@link import('./demo').useDemo} empties every box the row did not name. A row that had
 * to spell out three empty strings would be three more places to forget one.
 */
export interface Example {
    readonly title: string;
    readonly note: string;
    readonly pattern: string;
    readonly flags: string;
    readonly subject: string;
    /** Which feature this row demonstrates, and so which {@link Help} sections sit beside it. */
    readonly key?: string;
    readonly mode?: string;
    readonly replacement?: string;
    readonly namedLists?: string;
}

/** One run of text in a help panel: `code` is rendered as such, and nothing is rendered as markup. */
export interface HelpRun {
    readonly code: boolean;
    readonly text: string;
}

/** One block of a help panel. The generator emits these two kinds and no others. */
export type HelpBlock =
    | { readonly kind: 'paragraph'; readonly runs: readonly HelpRun[] }
    | { readonly kind: 'code'; readonly language: string; readonly text: string };

/** One section of `docs/COMPARISON.md`, lifted whole. */
export interface HelpSection {
    readonly heading: readonly HelpRun[];
    readonly blocks: readonly HelpBlock[];
}

/**
 * `help.json`, as `tools/build-demo-help.ps1` writes it: the documentation's own prose, keyed by the
 * same feature key the examples use.
 *
 * Generated at build time and never committed, so what the page loads is whatever
 * `docs/COMPARISON.md` said at the moment the bundle was built. That is the single source the slice
 * is for: there is no second copy of this prose under `demo/` to fall out of step with it.
 */
export interface Help {
    readonly source: string;
    readonly note: string;
    readonly entries: Readonly<Record<string, readonly HelpSection[]>>;
}
