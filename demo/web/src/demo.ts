// The demo page's state machine: three inputs, a highlighted result and a group table, driven by a
// worker pool.
//
// Nothing in here touches a component. The page is App.vue; this is the state it renders, kept
// separate so it can be tested without a DOM (tests/demo.test.ts) and exposed to checks.html, which
// drives the real page rather than a copy of it.

import { computed, ref, watch, type Ref } from 'vue';

import { MAX_DISPLAYED_MATCHES, MAX_SUBJECT_LENGTH } from './lib/caps';
import { MAX_FRAGMENT_LENGTH, decode, encode } from './lib/fragment';
import { segments } from './lib/highlight';
import { createPool } from './lib/pool';
import type { Answer, Example, Match, WorkerLike } from './types';

/**
 * How long the page waits after a keystroke before asking the engine.
 *
 * Auto-running is what makes a demo feel like a demo, and it is only safe because of the worker: a
 * pattern typed halfway through can be pathological, and the run in flight is killed rather than
 * waited for when the next one starts. Without the worker this number would have to be a Run button.
 *
 * 250 ms is inside NN/g's 1 s "keeps the flow of thought" limit
 * (<https://www.nngroup.com/articles/response-times-3-important-limits/>), which is why the page can
 * answer as you type rather than on a button.
 */
const DEBOUNCE_MS = 250;

/**
 * Where the page's own siblings live: `worker.js`, `examples.json` and the runtime under
 * `_framework/`.
 *
 * `document.baseURI` and NOT `import.meta.url`. Before the toolchain amendment the page's module sat
 * beside the worker and `import.meta.url` was right; a bundled module sits under `assets/`, so the
 * same expression would now resolve to `assets/worker.js` and 404. The worker is a sibling of the
 * PAGE, which is what this says. With no `<base href>` in the document - S71's deliberate divergence
 * from Microsoft's Blazor-on-Pages recipe - `document.baseURI` is the page's own URL, so the demo
 * boots at the repository subpath on GitHub Pages, at the root of a local server, and at a fork's
 * preview path, with nothing to keep in step.
 */
const beside = (file: string) => new URL(file, document.baseURI);

/** Makes a worker the way worker.js requires: a module worker, resolved against the page. */
export const spawnEngineWorker = (): WorkerLike => new Worker(beside('worker.js'), { type: 'module' });

export interface DemoOptions {
    spawn?: () => WorkerLike;
    examplesUrl?: URL;
}

export type EngineState = 'starting' | 'ready' | 'failed';

export function useDemo({ spawn = spawnEngineWorker, examplesUrl }: DemoOptions = {}) {
    const pattern = ref('(?:colour){e<=2}');
    const flags = ref('');
    const subject = ref('the color of the collar');

    const examples: Ref<readonly Example[]> = ref([]);
    const engine: Ref<EngineState> = ref('starting');
    const engineError = ref('');
    const running = ref(false); // a question is with the worker right now
    const pending = ref(false); // an input changed and the debounce has not fired yet
    const answer: Ref<Answer | null> = ref(null); // the engine's parsed JSON, or null before the first
    const failure = ref(''); // what to show instead of an answer
    const elapsedMs: Ref<number | null> = ref(null);
    const selected = ref(0);
    const shareable = ref(true);

    const pool = createPool({ spawn });

    void pool.ready.then((state) => {
        engine.value = state.ok ? 'ready' : 'failed';
        engineError.value = state.ok ? '' : state.error;
    });

    // --- the caps the page enforces itself ---------------------------------------------------

    const tooLong = computed(() => subject.value.length > MAX_SUBJECT_LENGTH);

    const matches = computed<readonly Match[]>(() => answer.value?.matches ?? []);

    const view = computed(() => segments(subject.value, matches.value, MAX_DISPLAYED_MATCHES));

    const capped = computed(() => view.value.total > view.value.shown);

    const current = computed<Match | null>(() => matches.value[selected.value] ?? null);

    /**
     * Is the answer on screen still the answer to what the inputs now say?
     *
     * `running` alone is not that question, and the difference is the whole debounce window: for
     * 250 ms after a keystroke nothing is with the worker, so `running` is false while the result
     * pane still shows the PREVIOUS case. Anything waiting for the page to settle - checks.html, and
     * a reader deciding whether to believe the number - has to wait for this one instead, or it
     * reads a stale answer and calls it the new one.
     */
    const busy = computed(() => pending.value || running.value);

    // --- asking the engine -------------------------------------------------------------------

    let timer: ReturnType<typeof setTimeout> | undefined;
    let question = 0;

    const ask = async (): Promise<void> => {
        // Numbered FIRST, before the cap check below can return: asking retires every earlier
        // question, including one still with a worker that is about to be killed. Numbering it
        // after the early return leaves the killed question's reply still current, so it lands
        // a microtask later and replaces the refusal below with the pool's bare reason.
        const mine = ++question;
        failure.value = '';

        if (tooLong.value) {
            // Refused, never truncated: a truncated subject gives wrong answers that look
            // right, and every index in them would be a lie about text the visitor can see.
            answer.value = null;
            failure.value =
                `The subject is ${subject.value.length.toLocaleString()} characters, over the demo's ` +
                `limit of ${MAX_SUBJECT_LENGTH.toLocaleString()}. Nothing was sent to the engine: ` +
                'the demo refuses rather than matching against a shortened subject.';
            return;
        }

        running.value = true;
        const startedAt = performance.now();
        const reply = await pool.ask({ pattern: pattern.value, flags: flags.value, subject: subject.value });

        // An answer to a question the page has moved on from. The pool already drops replies
        // whose requestId nobody is waiting for; this second guard covers the case where the
        // worker was replaced mid-question, whose reply arrives on a new worker's numbering.
        if (mine !== question) return;

        running.value = false;
        elapsedMs.value = performance.now() - startedAt;
        selected.value = 0;

        if (reply.aborted === true || typeof reply.error === 'string') {
            answer.value = null;
            failure.value = reply.error ?? 'stopped';
            return;
        }

        answer.value = reply;
    };

    const schedule = (): void => {
        pending.value = true;
        clearTimeout(timer);
        timer = setTimeout(() => {
            pending.value = false;
            // A question already in flight is killed rather than waited for. This is the whole
            // reason the spare exists: the kill costs a worker, and the replacement is already
            // booted, so typing over a runaway pattern stays as responsive as typing over a
            // fast one.
            if (running.value) pool.stop('replaced by a newer question');
            void ask();
        }, DEBOUNCE_MS);
    };

    const stop = (): void => {
        const wasRunning = running.value;

        // Retire the question BEFORE killing its worker. Killing it makes the pool settle the
        // call `ask` is awaiting, and that continuation runs a microtask after this function
        // returns - so without the bump it would land last and overwrite the sentence below
        // with the pool's own bare "stopped".
        ++question;

        // The debounce timer goes too, or a Stop pressed within 250 ms of a keystroke is
        // followed by the very run it just stopped.
        clearTimeout(timer);
        pending.value = false;
        running.value = false;
        answer.value = null;

        if (!wasRunning) {
            // Stop is offered during the debounce as well, when the question has not been
            // asked yet. Killing a worker here would cost one for nothing, and claiming a
            // match was killed would be a description of something that did not happen.
            failure.value = 'Stopped before the question was asked. Nothing was sent to the engine.';
            return;
        }

        pool.stop('stopped');
        failure.value =
            'Stopped. The worker running that match was killed and a warm spare took over, which is ' +
            "the only way to recover a pattern whose compilation never returns - the engine's own " +
            'timeout is a matching budget and does not cover that case.';
    };

    // --- the URL fragment --------------------------------------------------------------------

    const share = (): void => {
        const fragment = encode({ pattern: pattern.value, flags: flags.value, subject: subject.value });
        shareable.value = fragment.length <= MAX_FRAGMENT_LENGTH;

        // replaceState, not pushState: typing three characters must not put three entries in
        // the history, or Back stops being a way out of the page.
        //
        // Over the cap the fragment is REMOVED rather than left alone. Leaving it alone keeps
        // the last shareable case in the address bar while the screen shows a different one,
        // so copying the URL silently hands someone else the wrong case - the failure the
        // "no link" pill is there to prevent, dressed as a working link.
        history.replaceState(
            null,
            '',
            shareable.value ? '#' + fragment : location.pathname + location.search,
        );
    };

    const load = (example: Example): void => {
        pattern.value = example.pattern;
        flags.value = example.flags;
        subject.value = example.subject;
    };

    watch([pattern, flags, subject], () => {
        share();
        schedule();
    });

    /** The mount path: read the shared case, ask about it, and fetch the tour. */
    const initialise = async (): Promise<void> => {
        const shared = decode(location.hash);
        if (shared !== null) {
            pattern.value = shared.pattern;
            flags.value = shared.flags;
            subject.value = shared.subject;
        }

        share();
        void ask();

        try {
            const response = await fetch(examplesUrl ?? beside('examples.json'));
            if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
            examples.value = (await response.json()) as readonly Example[];
        } catch (error) {
            // The tour failing to load must not take the page with it: the three inputs are
            // the demo, the sidebar is the tour around it.
            examples.value = [];
            console.error('the examples could not be loaded', error);
        }
    };

    return {
        pattern,
        flags,
        subject,
        examples,
        engine,
        engineError,
        running,
        pending,
        busy,
        answer,
        failure,
        elapsedMs,
        selected,
        shareable,
        tooLong,
        matches,
        view,
        capped,
        current,
        maxSubjectLength: MAX_SUBJECT_LENGTH,
        maxDisplayedMatches: MAX_DISPLAYED_MATCHES,
        load,
        stop,
        initialise,
        // Exposed for checks.html, which drives the real page rather than a copy of it.
        pool,
    };
}

export type Demo = ReturnType<typeof useDemo>;
