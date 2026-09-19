// The page's state machine, without a page.
//
// jsdom supplies the real `document`, `location` and `history` - the sitting-2 version of these
// tests shimmed all three by hand - so what is asserted below is what a browser would do with the
// address bar, not what a stand-in recorded. What still is not tested here is anything that needs a
// real browser: that the runtime boots in a Web Worker, that terminate() kills a wedged
// construction, that the page keeps painting. That is checks.html, driven in a real browser.

import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import { type Demo, useDemo } from '../src/demo';
import { MAX_FRAGMENT_LENGTH } from '../src/lib/fragment';

import { FakeWorker, type FakeWorkerOptions } from './fake-worker';

/** Long enough for Vue's watcher to run and for the debounce to fire, and no longer. */
const DEBOUNCE_MS = 250;

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

const replaced: (string | URL | null | undefined)[] = [];

/**
 * The demos a test started, disposed after it.
 *
 * `initialise()` listens for `hashchange` on the window, which outlives the test that started it:
 * an undisposed demo reacts to the NEXT test's fragment, asks its own question and spends a worker
 * doing it, which shows up as a failure in whichever test happens to count workers.
 */
const started: Demo[] = [];

const track = (demo: Demo): Demo => {
    started.push(demo);
    return demo;
};

beforeEach(() => {
    replaced.length = 0;
    FakeWorker.killed = 0;
    // The composable reads the fragment as it starts, so a fragment left behind by the previous
    // test would seed the next one's inputs.
    location.hash = '';
    vi.spyOn(history, 'replaceState');
    vi.mocked(history.replaceState).mockImplementation(((
        _state: unknown,
        _title: string,
        url?: string | URL | null,
    ) => {
        replaced.push(url);
        location.hash = typeof url === 'string' && url.includes('#') ? url.slice(url.indexOf('#')) : '';
    }) as typeof history.replaceState);
});

afterEach(() => {
    for (const demo of started.splice(0)) demo.dispose();
    vi.restoreAllMocks();
});

/**
 * Starts the page's state machine and waits for its first answer.
 *
 * `initialise()` is not called - that is the mount path, which asks directly - so the first
 * question is triggered by setting an input, which goes through the debounce.
 */
async function start(options: FakeWorkerOptions = {}) {
    const demo = track(useDemo({ spawn: () => new FakeWorker(options) }));
    demo.subject.value = 'abc';
    await sleep(DEBOUNCE_MS + 60);
    return demo;
}

/** Answers the fetch for `examples.json` with `body`, so no test reaches the network. */
const stubExamples = (body: string) =>
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(body, { status: 200 }));

const answeredLength = (demo: Demo) => demo.answer.value?.matches?.[0]?.length ?? null;

test('the page reports itself busy for the whole debounce window, not just the worker round trip', async () => {
    const demo = await start();
    expect(answeredLength(demo)).toBe(3); // the first case was answered

    demo.subject.value = 'abcdefghij';
    await sleep(0); // the watcher runs, the debounce has not fired

    // This is the trap the check page fell into: nothing is with the worker during the debounce, so
    // `running` is false while `answer` is still the PREVIOUS case's. A driver that waits on
    // `running` alone reads the old answer and calls it the new one.
    expect(demo.running.value).toBe(false);
    expect(answeredLength(demo)).toBe(3); // still the old answer
    expect(demo.busy.value).toBe(true); // but the page knows it is not settled

    await sleep(DEBOUNCE_MS + 60);
    expect(demo.busy.value).toBe(false);
    expect(answeredLength(demo)).toBe(10); // the new subject, answered
});

test("stopping a runaway leaves the explanation on screen, not the pool's bare reason", async () => {
    const demo = await start({ answers: false });
    expect(demo.answer.value).toBeNull(); // the fake never answers, so there is nothing to show

    demo.subject.value = 'aaaaaaaa';
    await sleep(DEBOUNCE_MS + 60);
    expect(demo.running.value).toBe(true); // the question is with the worker and stays there

    demo.stop();
    await sleep(20); // the killed question's reply settles a microtask after stop() returns

    // Killing the worker makes the pool settle the in-flight question with `{aborted: true,
    // error: 'stopped'}`, and that continuation lands AFTER stop() has written its message. Without
    // the guard in stop() it overwrites it, and the button's whole explanation never reaches anyone.
    expect(demo.failure.value).toMatch(/^Stopped\. The worker running that match was killed/);
    expect(demo.running.value).toBe(false);
    expect(demo.busy.value).toBe(false);
});

test('a stop also cancels a question the debounce has not asked yet', async () => {
    // An answering worker, so the previous question is finished and the only thing outstanding when
    // Stop is pressed is the debounce timer - which is the state the button is offered in for 250 ms
    // after every keystroke.
    const demo = await start();
    expect(demo.running.value).toBe(false);

    demo.subject.value = 'aaaa';
    await sleep(0);
    expect(demo.busy.value).toBe(true);
    expect(demo.running.value).toBe(false); // nothing is with the worker yet

    const killedBefore = FakeWorker.killed;
    demo.stop();
    await sleep(DEBOUNCE_MS + 60);

    expect(demo.answer.value).toBeNull(); // the stopped question was never asked

    // Without clearing the timer, the run just stopped restarts by itself a moment later, which
    // reads as a Stop button that does not work.
    expect(demo.busy.value).toBe(false);
    expect(demo.running.value).toBe(false);

    // And nothing was with a worker, so no worker is spent and the page does not describe a killed
    // match that never existed.
    expect(FakeWorker.killed).toBe(killedBefore);
    expect(demo.failure.value).toBe('Stopped before the question was asked. Nothing was sent to the engine.');
});

test('the subject-cap refusal survives the killing of the question it replaces', async () => {
    // The keystroke that breaches the cap usually lands while a question is still with the worker -
    // that is what a long paste into a running demo looks like. Asking retires the earlier question,
    // so its abort cannot come back a microtask later and replace the explanation with "replaced by a
    // newer question", which tells the visitor nothing about why there is no answer.
    const demo = await start({ answers: false });
    expect(demo.running.value).toBe(true); // the runaway is with the worker

    demo.subject.value = 'x'.repeat(demo.maxSubjectLength + 1);
    await sleep(DEBOUNCE_MS + 120);

    expect(demo.failure.value).toMatch(/^The subject is 100,001 characters, over the demo's limit of 100,000\./);
    expect(demo.answer.value).toBeNull();

    // And the refusal ENDS the question. The refused keystroke supersedes one that was with a
    // worker, so `running` is still true from that one; leaving it set puts "matching..." and a
    // Stop button beside a message that says nothing was sent to the engine, for ever.
    expect(demo.running.value).toBe(false);
    expect(demo.busy.value).toBe(false);
});

test('the highlighted subject is the one that was answered, not the one being typed', async () => {
    const demo = await start();
    demo.subject.value = 'abcdef';
    await sleep(DEBOUNCE_MS + 60);
    expect(demo.view.value.segments).toEqual([{ text: 'abcdef', match: 0 }]);

    demo.subject.value = 'ZZ';
    await sleep(0); // the watcher has run; the debounce has not fired and nothing is with the worker

    // The answer on screen is still {index: 0, length: 6}. Drawn against the text now in the box
    // that is a highlight over characters the engine never saw - "ZZ" painted as a six-character
    // match - and the group table's Text column is the same lie one row further down.
    expect(demo.view.value.segments).toEqual([{ text: 'abcdef', match: 0 }]);
    expect(demo.answeredSubject.value).toBe('abcdef');

    await sleep(DEBOUNCE_MS + 60);
    expect(demo.view.value.segments).toEqual([{ text: 'ZZ', match: 0 }]); // and it catches up
    expect(demo.answeredSubject.value).toBe('ZZ');
});

test('a shared case is asked once, and spends no worker doing it', async () => {
    // The mount path sets the three inputs and asks about them. If the watcher sees those writes it
    // schedules the same question again 250 ms later, which kills the worker still answering the
    // first one and burns the warm spare - measured before the fix: two questions posted, one
    // worker killed, three constructed, on a page nobody had typed into.
    location.hash = '#p=a%2Bb&f=&s=aab';
    stubExamples('[]');

    const workers: FakeWorker[] = [];
    const demo = track(
        useDemo({
            spawn: () => {
                const worker = new FakeWorker();
                workers.push(worker);
                return worker;
            },
        }),
    );

    await demo.initialise();
    await sleep(DEBOUNCE_MS + 120);

    expect(demo.pattern.value).toBe('a+b'); // the shared case did load
    expect(demo.subject.value).toBe('aab');
    expect(workers.reduce((total, worker) => total + worker.posted.length, 0)).toBe(1);
    expect(FakeWorker.killed).toBe(0);
    expect(workers).toHaveLength(2); // one serving, one spare: no replacement was needed
});

test('editing the fragment, or following a same-page link, applies the new case', async () => {
    stubExamples('[]');
    const demo = track(useDemo({ spawn: () => new FakeWorker() }));
    await demo.initialise();
    await sleep(DEBOUNCE_MS + 60);

    location.hash = '#p=z&f=IgnoreCase&s=zzzz';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await sleep(DEBOUNCE_MS + 120);

    expect(demo.pattern.value).toBe('z');
    expect(demo.flags.value).toBe('IgnoreCase');
    expect(demo.subject.value).toBe('zzzz');
    expect(answeredLength(demo)).toBe(4); // and the new case was asked, not just typed into the boxes
});

test('an examples.json that is not a list of worked examples leaves the tour empty, not half-drawn', async () => {
    // Fetched at runtime and cast, so nothing checks it: a file with a missing member renders as a
    // sidebar of blank buttons, and one that is not even a list renders as `undefined` in the DOM.
    stubExamples('[{"title": "half a case", "pattern": "a"}]');
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {});

    const demo = track(useDemo({ spawn: () => new FakeWorker() }));
    await demo.initialise();

    expect(demo.examples.value).toEqual([]);
    expect(errors).toHaveBeenCalled();
});

test('a worker killed while the runtime is still booting is not an engine failure', async () => {
    // The runtime takes a second or two to come up in a browser, and the page accepts typing
    // throughout. Whichever worker is killed in that window has said nothing about whether the engine
    // loads, so reporting its death as "engine failed to load" puts a permanent broken-page banner
    // over a page whose promoted spare answers every question correctly.
    //
    // A 600 ms boot and a worker that never answers: the first question is still with the serving
    // worker when the second keystroke's debounce fires, so the page kills a worker that has not
    // finished booting - two keystrokes during a runtime boot, and nothing unusual.
    const demo = useDemo({ spawn: () => new FakeWorker({ answers: false, bootMs: 600 }) });

    demo.subject.value = 'abc';
    await sleep(DEBOUNCE_MS + 60);
    expect(demo.running.value).toBe(true); // the first question is with the still-booting worker

    demo.subject.value = 'abcdef';
    await sleep(DEBOUNCE_MS + 60);
    expect(FakeWorker.killed).toBe(1); // the still-booting worker was killed

    await sleep(500); // the spare, spawned at the same time, finishes its own boot
    expect(demo.engine.value).toBe('ready');
    expect(demo.engineError.value).toBe('');
});

test('a case too long to share leaves no link behind in the address bar', async () => {
    const demo = await start();

    demo.subject.value = 'a shareable case';
    await sleep(DEBOUNCE_MS + 60);
    expect(location.hash).toMatch(/a\+shareable\+case|a%20shareable%20case/);

    demo.subject.value = 'x'.repeat(MAX_FRAGMENT_LENGTH + 1);
    await sleep(DEBOUNCE_MS + 60);

    expect(demo.shareable.value).toBe(false); // the page says this case has no link
    // The previous case's fragment must go with it. Left in place, the address bar holds a link to a
    // different case from the one on screen, and copying it hands somebody else the wrong case.
    expect(location.hash).toBe('');
    expect(replaced.at(-1)).toBe('/demo/');
});
