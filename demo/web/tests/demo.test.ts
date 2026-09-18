// The page's state machine, without a page.
//
// jsdom supplies the real `document`, `location` and `history` - the sitting-2 version of these
// tests shimmed all three by hand - so what is asserted below is what a browser would do with the
// address bar, not what a stand-in recorded. What still is not tested here is anything that needs a
// real browser: that the runtime boots in a Web Worker, that terminate() kills a wedged
// construction, that the page keeps painting. That is checks.html, driven in a real browser.

import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import { useDemo } from '../src/demo';
import { MAX_FRAGMENT_LENGTH } from '../src/lib/fragment';

import { FakeWorker, type FakeWorkerOptions } from './fake-worker';

/** Long enough for Vue's watcher to run and for the debounce to fire, and no longer. */
const DEBOUNCE_MS = 250;

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

const replaced: (string | URL | null | undefined)[] = [];

beforeEach(() => {
    replaced.length = 0;
    FakeWorker.killed = 0;
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
    vi.restoreAllMocks();
});

/**
 * Starts the page's state machine and waits for its first answer.
 *
 * `initialise()` is not called - that is the mount path, which reads the fragment and asks directly
 * - so the first question is triggered by setting an input, which goes through the debounce.
 */
async function start(options: FakeWorkerOptions = {}) {
    const demo = useDemo({ spawn: () => new FakeWorker(options) });
    demo.subject.value = 'abc';
    await sleep(DEBOUNCE_MS + 60);
    return demo;
}

const answeredLength = (demo: ReturnType<typeof useDemo>) => demo.answer.value?.matches?.[0]?.length ?? null;

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
