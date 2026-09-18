// The demo page: three inputs, a highlighted result and a group table, driven by a worker pool.
//
// The Vue build is vendored, not fetched from a CDN and not bundled: a CDN makes the demo's
// availability someone else's uptime, and a bundler makes it someone else's toolchain. Everything
// here is a plain ES module the browser loads directly, which is also why there is no build step to
// document in demo/README.md.
//
// vue@3.5.43, dist/vue.esm-browser.prod.js from the npm tarball (registry integrity
// sha512-o5qZoksdnjIKvW1srZ3ab7pcDNYAerBjRe54D0LBLfRdCYFrSgBHVXokMas35czQc0//lmx4/tuY4ZNQ+Rf2Ng==,
// checked 2026-09-18). The vendored file is 173,163 bytes, SHA-256
// 877f675a8c5f347073b4d5437439a042b984d81fc5da2770eb7e6d320d5017f3. To update it, re-run the steps
// in demo/README.md - the hash is the whole point of vendoring, so it is recorded rather than
// assumed.
import { computed, createApp, onMounted, ref, watch } from './vendor/vue.esm-browser.prod.js';

import { MAX_DISPLAYED_MATCHES, MAX_SUBJECT_LENGTH } from './lib/caps.js';
import { MAX_FRAGMENT_LENGTH, decode, encode } from './lib/fragment.js';
import { segments } from './lib/highlight.js';
import { createPool } from './lib/pool.js';

/**
 * How long the page waits after a keystroke before asking the engine.
 *
 * Auto-running is what makes a demo feel like a demo, and it is only safe because of the worker: a
 * pattern typed halfway through can be pathological, and the run in flight is killed rather than
 * waited for when the next one starts. Without the worker this number would have to be a Run button.
 */
const DEBOUNCE_MS = 250;

/** Makes a worker the way worker.js requires: a module worker, resolved relative to THIS module. */
export const spawnEngineWorker = () =>
    // `import.meta.url` and not the page's URL. The two differ the moment the page is served from a
    // path the module is not - and resolving the worker against the page is the bug the slice file
    // calls "the worker URL resolved relative to the page rather than the base, wearing a different
    // hat". Against import.meta.url the worker is found wherever the app is mounted: the repository
    // subpath on GitHub Pages, the root on a local server, a preview path on someone's fork.
    new Worker(new URL('./worker.js', import.meta.url), { type: 'module' });

export function createDemo({ spawn = spawnEngineWorker, examplesUrl = new URL('./examples.json', import.meta.url) } = {}) {
    return {
        setup() {
            const pattern = ref('(?:colour){e<=2}');
            const flags = ref('');
            const subject = ref('the color of the collar');

            const examples = ref([]);
            const engine = ref('starting');          // starting | ready | failed
            const engineError = ref('');
            const running = ref(false);              // a question is with the worker right now
            const pending = ref(false);              // an input changed and the debounce has not fired yet
            const answer = ref(null);                // the engine's parsed JSON, or null before the first
            const failure = ref('');                 // what to show instead of an answer
            const elapsedMs = ref(null);
            const selected = ref(0);
            const shareable = ref(true);

            const pool = createPool({
                spawn,
                onChange: () => {},
            });

            pool.ready.then((state) => {
                engine.value = state.ok ? 'ready' : 'failed';
                engineError.value = state.ok ? '' : state.error;
            });

            // --- the caps the page enforces itself ------------------------------------------------

            const tooLong = computed(() => subject.value.length > MAX_SUBJECT_LENGTH);

            const matches = computed(() => answer.value?.matches ?? []);

            const view = computed(() => segments(subject.value, matches.value, MAX_DISPLAYED_MATCHES));

            const capped = computed(() => view.value.total > view.value.shown);

            const current = computed(() => matches.value[selected.value] ?? null);

            /**
             * Is the answer on screen still the answer to what the inputs now say?
             *
             * `running` alone is not that question, and the difference is the whole debounce window:
             * for 250 ms after a keystroke nothing is with the worker, so `running` is false while the
             * result pane still shows the PREVIOUS case. Anything waiting for the page to settle -
             * checks.html, and a reader deciding whether to believe the number - has to wait for this
             * one instead, or it reads a stale answer and calls it the new one.
             */
            const busy = computed(() => pending.value || running.value);

            // --- asking the engine -----------------------------------------------------------------

            let timer = null;
            let question = 0;

            const ask = async () => {
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
                        `The subject is ${subject.value.length.toLocaleString()} characters, over the demo's `
                        + `limit of ${MAX_SUBJECT_LENGTH.toLocaleString()}. Nothing was sent to the engine: `
                        + 'the demo refuses rather than matching against a shortened subject.';
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

                if (reply.aborted) {
                    answer.value = null;
                    failure.value = reply.error;
                    return;
                }

                if (typeof reply.error === 'string') {
                    answer.value = null;
                    failure.value = reply.error;
                    return;
                }

                answer.value = reply;
            };

            const schedule = () => {
                pending.value = true;
                clearTimeout(timer);
                timer = setTimeout(() => {
                    pending.value = false;
                    // A question already in flight is killed rather than waited for. This is the whole
                    // reason the spare exists: the kill costs a worker, and the replacement is already
                    // booted, so typing over a runaway pattern stays as responsive as typing over a
                    // fast one.
                    if (running.value) pool.stop('replaced by a newer question');
                    ask();
                }, DEBOUNCE_MS);
            };

            const stop = () => {
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
                    'Stopped. The worker running that match was killed and a warm spare took over, which is '
                    + 'the only way to recover a pattern whose compilation never returns - the engine\'s own '
                    + 'timeout is a matching budget and does not cover that case.';
            };

            // --- the URL fragment ------------------------------------------------------------------

            const share = () => {
                const fragment = encode({ pattern: pattern.value, flags: flags.value, subject: subject.value });
                shareable.value = fragment.length <= MAX_FRAGMENT_LENGTH;

                // replaceState, not pushState: typing three characters must not put three entries in
                // the history, or Back stops being a way out of the page.
                //
                // Over the cap the fragment is REMOVED rather than left alone. Leaving it alone keeps
                // the last shareable case in the address bar while the screen shows a different one,
                // so copying the URL silently hands someone else the wrong case - the failure the
                // "no link" pill is there to prevent, dressed as a working link.
                history.replaceState(null, '', shareable.value ? '#' + fragment : location.pathname + location.search);
            };

            const load = (example) => {
                pattern.value = example.pattern;
                flags.value = example.flags;
                subject.value = example.subject;
            };

            watch([pattern, flags, subject], () => {
                share();
                schedule();
            });

            onMounted(async () => {
                const shared = decode(location.hash);
                if (shared !== null) {
                    pattern.value = shared.pattern;
                    flags.value = shared.flags;
                    subject.value = shared.subject;
                }

                share();
                ask();

                try {
                    const response = await fetch(examplesUrl);
                    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
                    examples.value = await response.json();
                } catch (error) {
                    // The tour failing to load must not take the page with it: the three inputs are
                    // the demo, the sidebar is the tour around it.
                    examples.value = [];
                    console.error('the examples could not be loaded', error);
                }
            });

            return {
                pattern, flags, subject, examples, engine, engineError, running, pending, busy,
                answer, failure,
                elapsedMs, selected, shareable, tooLong, matches, view, capped, current,
                maxSubjectLength: MAX_SUBJECT_LENGTH,
                maxDisplayedMatches: MAX_DISPLAYED_MATCHES,
                load, stop,
                // Exposed for checks.html, which drives the real page rather than a copy of it.
                pool,
            };
        },
    };
}

// checks.html imports this module for its parts and mounts its own app, so mounting here has to be
// conditional - importing a module that grabs #app would fight it for the DOM.
if (document.getElementById('app') !== null) {
    const app = createApp(createDemo());
    const mounted = app.mount('#app');
    // The page's own state, for a driver to read. Same contract as S70's window.__harness: a machine
    // can wait on it and read what the page believes rather than scraping rendered text.
    window.__demo = mounted;
}
