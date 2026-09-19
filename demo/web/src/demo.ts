// The demo page's state machine: three inputs, a highlighted result and a group table, driven by a
// worker pool.
//
// Nothing in here touches a component. The page is App.vue; this is the state it renders, kept
// separate so it can be tested without a DOM (tests/demo.test.ts) and exposed to checks.html, which
// drives the real page rather than a copy of it.

import { computed, ref, watch, type Ref } from 'vue';

import { MAX_DISPLAYED_MATCHES, MAX_SUBJECT_LENGTH } from './lib/caps';
import { MAX_FRAGMENT_LENGTH, decode, encode, fragmentText } from './lib/fragment';
import { segments } from './lib/highlight';
import { createPool } from './lib/pool';
import { isExampleList, isHelp } from './lib/shapes';
import type { Answer, Example, Help, HelpSection, Inputs, Match, WorkerLike } from './types';

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

/**
 * The case the page shows when the address bar does not name one.
 *
 * Written down once, because it is two answers and not one: the case a visitor arrives to, and the
 * case to go back to when a fragment is removed from the address bar.
 */
const DEFAULTS: Inputs = {
    pattern: '(?:colour){e<=2}',
    flags: '',
    subject: 'the color of the collar',
    mode: '',
    replacement: '',
    namedLists: '',
};

/** Makes a worker the way worker.js requires: a module worker, resolved against the page. */
export const spawnEngineWorker = (): WorkerLike => new Worker(beside('worker.js'), { type: 'module' });

export interface DemoOptions {
    spawn?: () => WorkerLike;
    examplesUrl?: URL;
    helpUrl?: URL;
}

export type EngineState = 'starting' | 'ready' | 'failed';

export function useDemo({ spawn = spawnEngineWorker, examplesUrl, helpUrl }: DemoOptions = {}) {
    // The shared case is read HERE, before the watcher below exists, and not on the mount path.
    // Writing these three from `initialise()` puts three changes through the watcher, which
    // schedules the very question `initialise` is about to ask: the second one lands 250 ms later,
    // kills the worker still answering the first and spends the warm spare, on a page nobody has
    // typed into. Seeding the refs before anything watches them is the whole fix.
    const shared = decode(location.hash);

    const pattern = ref(shared?.pattern ?? DEFAULTS.pattern);
    const flags = ref(shared?.flags ?? DEFAULTS.flags);
    const subject = ref(shared?.subject ?? DEFAULTS.subject);
    const mode = ref(shared?.mode ?? DEFAULTS.mode);
    const replacement = ref(shared?.replacement ?? DEFAULTS.replacement);
    const namedLists = ref(shared?.namedLists ?? DEFAULTS.namedLists);

    const examples: Ref<readonly Example[]> = ref([]);
    const help: Ref<Help | null> = ref(null);
    /**
     * Which feature the help panel is explaining: the key of the last sample loaded.
     *
     * It survives editing, deliberately. Somebody who loaded "POSIX" and then changed the subject
     * is still reading about POSIX, and a panel that closed itself on the first keystroke would be
     * help that is only available to somebody who has not started.
     */
    const helpKey = ref('');
    const engine: Ref<EngineState> = ref('starting');
    const engineError = ref('');
    const running = ref(false); // a question is with the worker right now
    const pending = ref(false); // an input changed and the debounce has not fired yet
    const answer: Ref<Answer | null> = ref(null); // the engine's parsed JSON, or null before the first
    /**
     * The subject the answer above is an answer to.
     *
     * Every index in an answer is an offset into the text that was searched, and that text stops
     * being what the box says the moment someone types: for the debounce plus the round trip, the
     * live `subject` and the answer describe different strings. Drawing the highlights, the group
     * Text column or a capture from the live one paints the old offsets over the new text - an
     * answer of `{index: 0, length: 6}` renders "ZZ" as a six-character match - which is a wrong
     * answer that looks exactly like a right one. This is the string the page renders from.
     */
    const answeredSubject = ref('');
    /**
     * The pattern the answer above is an answer to, for the same reason and with the same trap.
     *
     * A parse error carries an offset into the pattern that was SENT, and the box has moved on by
     * the time it arrives: drawing the caret against the live pattern puts it under whichever
     * character now happens to sit at that index, which is a precise-looking claim about the wrong
     * character. This is the string the caret is drawn against.
     */
    const answeredPattern = ref('');
    const failure = ref(''); // what to show instead of an answer
    /**
     * Where in the pattern the failure above was raised, or null when nothing in it is to blame.
     *
     * Null is a real answer and not a missing one: a misspelt flag, a cap refusal, a timeout and a
     * bad replacement template all arrive without a position, deliberately - see
     * `DemoAnswer.ErrorOffset` - and a page that defaulted to 0 would draw a caret under the first
     * character of a pattern that parsed perfectly well.
     */
    const failureOffset: Ref<number | null> = ref(null);
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

    const view = computed(() => segments(answeredSubject.value, matches.value, MAX_DISPLAYED_MATCHES));

    const capped = computed(() => view.value.total > view.value.shown);

    const current = computed<Match | null>(() => matches.value[selected.value] ?? null);

    /**
     * The whole rewritten subject, in replace mode, or null when the answer is not one.
     *
     * `??` and not a truth test: replacing every match with nothing gives `''`, which is a complete
     * answer and the commonest way to see what a pattern actually covers. Treating it as "no
     * answer" hides the result of the very case somebody is most likely to try.
     */
    const replaced = computed<string | null>(() => answer.value?.replaced ?? null);

    /** Whether the engine ran out of subject before it ran out of pattern. Partial mode only. */
    const partial = computed(() => matches.value.some((match) => match.partialMatch === true));

    /**
     * Selects a match, from a click or from the keyboard.
     *
     * A function and not `selected = i` in the template because the page offers two ways to reach
     * each match - the highlight in the subject and the row in the table - and both a pointer and
     * a keyboard reach each of those. One handler is what keeps the four in step.
     */
    const select = (index: number): void => {
        if (!Number.isInteger(index) || index < 0 || index >= matches.value.length) return;
        selected.value = index;
    };

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
        failureOffset.value = null;

        if (tooLong.value) {
            // The refusal ENDS the question, including one that was still with a worker when this
            // keystroke superseded it - the usual way the cap is reached is a long paste into a
            // running demo. Leaving `running` set from that earlier question leaves `busy` true
            // for ever, so the page shows "matching..." and a Stop button beside a message that
            // says nothing was sent to the engine.
            running.value = false;

            // Refused, never truncated: a truncated subject gives wrong answers that look
            // right, and every index in them would be a lie about text the visitor can see.
            answer.value = null;
            answeredSubject.value = '';
            failure.value =
                `The subject is ${subject.value.length.toLocaleString()} characters, over the demo's ` +
                `limit of ${MAX_SUBJECT_LENGTH.toLocaleString()}. Nothing was sent to the engine: ` +
                'the demo refuses rather than matching against a shortened subject.';
            return;
        }

        running.value = true;
        const startedAt = performance.now();
        // The subject as it was SENT. The answer's offsets belong to this string and to no other,
        // so it is kept beside the reply rather than read back off the live ref, which has moved
        // on by the time a slow answer lands.
        const asked = subject.value;
        const askedPattern = pattern.value;
        const reply = await pool.ask({
            pattern: pattern.value,
            flags: flags.value,
            subject: asked,
            mode: mode.value,
            replacement: replacement.value,
            namedLists: namedLists.value,
        });

        // An answer to a question the page has moved on from. The pool already drops replies
        // whose requestId nobody is waiting for; this second guard covers the case where the
        // worker was replaced mid-question, whose reply arrives on a new worker's numbering.
        if (mine !== question) return;

        running.value = false;
        elapsedMs.value = performance.now() - startedAt;
        selected.value = 0;
        answeredPattern.value = askedPattern;

        if (reply.aborted === true || typeof reply.error === 'string') {
            answer.value = null;
            answeredSubject.value = '';
            failure.value = reply.error ?? 'stopped';
            // `typeof`, not `?? null`: 0 is the first character of the pattern and the commonest
            // position upstream raises, and `0 ?? null` is 0 only by accident of which falsy value
            // this is. A reply that omits the member has nothing to point at.
            failureOffset.value = typeof reply.errorOffset === 'number' ? reply.errorOffset : null;
            return;
        }

        answeredSubject.value = asked;
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
        answeredSubject.value = '';
        failureOffset.value = null; // a stop is nothing the pattern did

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
        const fragment = encode({
            pattern: pattern.value,
            flags: flags.value,
            subject: subject.value,
            mode: mode.value,
            replacement: replacement.value,
            namedLists: namedLists.value,
        });
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

    /**
     * Fills the boxes from a sample, and EMPTIES every box the sample does not name.
     *
     * The emptying is the part that matters. A row carries only what its feature needs, so clicking
     * a replace sample and then a plain one would otherwise leave the previous template in place and
     * run the plain sample in replace mode - an answer that is wrong and looks entirely ordinary.
     */
    const load = (example: Example): void => {
        pattern.value = example.pattern;
        flags.value = example.flags;
        subject.value = example.subject;
        mode.value = example.mode ?? '';
        replacement.value = example.replacement ?? '';
        namedLists.value = example.namedLists ?? '';
        helpKey.value = example.key ?? '';
    };

    /** The documentation's own sections for the feature on screen, or none. */
    const helpSections = computed<readonly HelpSection[]>(() => help.value?.entries[helpKey.value] ?? []);

    watch([pattern, flags, subject, mode, replacement, namedLists], () => {
        share();
        schedule();
    });

    /**
     * Applies a case that arrived in the address bar after load: an edited fragment, Back or
     * Forward between two shared cases, or a same-page link.
     *
     * The page owns the fragment and rewrites it on every keystroke, so it also sees its own
     * writes here in a browser that reports them. Comparing before assigning is what stops that
     * being a loop, and it costs nothing: identical inputs are not a new case.
     */
    const applyFragment = (): void => {
        // A URL with no fragment at all names a case too - the one the page boots with - and Back
        // onto the entry the visitor arrived on is exactly that. Ignoring it left the screen
        // showing a case the address bar no longer held, which is the mismatch the fragment exists
        // to prevent, wearing a Back button that appears not to work.
        //
        // Somebody else's anchor (`#install`, a link into the page) is still left alone: it is a
        // place to scroll to and says nothing about the three inputs, so treating it as the
        // defaults would throw away what the visitor had typed.
        const incoming = decode(location.hash) ?? (fragmentText(location.hash) === '' ? DEFAULTS : null);
        if (incoming === null) return;
        if (
            incoming.pattern === pattern.value &&
            incoming.flags === flags.value &&
            incoming.subject === subject.value &&
            incoming.mode === mode.value &&
            incoming.replacement === replacement.value &&
            incoming.namedLists === namedLists.value
        ) {
            return;
        }

        pattern.value = incoming.pattern;
        flags.value = incoming.flags;
        subject.value = incoming.subject;
        mode.value = incoming.mode;
        replacement.value = incoming.replacement;
        namedLists.value = incoming.namedLists;
    };

    /** The mount path: write the shared case back, ask about it, and fetch the tour. */
    const initialise = async (): Promise<void> => {
        // The three inputs already hold the shared case - they were seeded from the fragment
        // before the watcher existed - so this asks once and nothing is scheduled behind it.
        share();
        void ask();

        // Registered on the mount path rather than in the composable, so that a demo created for
        // a test or for checks.html does not leave a listener on the window behind it.
        window.addEventListener('hashchange', applyFragment);

        // Fetched together, and neither is awaited before the other is asked for: they are two
        // static files off the same origin, and a page that waits for the first before asking for
        // the second pays two round trips for one screen.
        await Promise.all([
            (async () => {
                try {
                    const response = await fetch(examplesUrl ?? beside('examples.json'));
                    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
                    const loaded: unknown = await response.json();
                    if (!isExampleList(loaded))
                        throw new Error('examples.json is not a list of worked examples');
                    examples.value = loaded;
                } catch (error) {
                    // The tour failing to load must not take the page with it: the three inputs are
                    // the demo, the sidebar is the tour around it.
                    examples.value = [];
                    console.error('the examples could not be loaded', error);
                }
            })(),
            (async () => {
                try {
                    const response = await fetch(helpUrl ?? beside('help.json'));
                    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
                    const loaded: unknown = await response.json();
                    if (!isHelp(loaded)) throw new Error('help.json is not the generated documentation');
                    help.value = loaded;
                } catch (error) {
                    // Softly, like the tour above, and for a sharper reason: help.json is GENERATED
                    // from docs/COMPARISON.md at build time, so the way it goes missing is a page
                    // served straight out of the source tree. That page must still match patterns.
                    help.value = null;
                    console.error('the help could not be loaded', error);
                }
            })(),
        ]);
    };

    /** Lets go of everything that outlives the page's own state: the listener and the workers. */
    const dispose = (): void => {
        window.removeEventListener('hashchange', applyFragment);
        clearTimeout(timer);
        pool.dispose();
    };

    return {
        pattern,
        flags,
        subject,
        mode,
        replacement,
        namedLists,
        examples,
        help,
        helpKey,
        helpSections,
        engine,
        engineError,
        running,
        pending,
        busy,
        answer,
        answeredSubject,
        answeredPattern,
        failure,
        failureOffset,
        elapsedMs,
        selected,
        shareable,
        tooLong,
        matches,
        view,
        capped,
        current,
        replaced,
        partial,
        maxSubjectLength: MAX_SUBJECT_LENGTH,
        maxDisplayedMatches: MAX_DISPLAYED_MATCHES,
        load,
        select,
        stop,
        initialise,
        dispose,
        // Exposed for checks.html, which drives the real page rather than a copy of it.
        pool,
    };
}

export type Demo = ReturnType<typeof useDemo>;
