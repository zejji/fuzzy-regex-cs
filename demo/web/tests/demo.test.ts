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
 * The real `history.replaceState`, kept before the spy goes over it.
 *
 * The stand-in below calls through to it rather than assigning `location.hash`. Assigning the hash
 * is a NAVIGATION in jsdom, as it is in a browser, so it fires `hashchange` - and the page's own
 * `share()` uses `replaceState`, which by specification fires nothing. A stand-in that fired one
 * would have the page answering its own writes, which is a browser behaviour that does not exist.
 */
const realReplaceState = history.replaceState.bind(history);

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
        realReplaceState(null, '', url ?? location.href);
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

/** The smallest `help.json` that satisfies `isHelp`: one key, one section, one paragraph. */
const HELP = JSON.stringify({
    source: 'docs/COMPARISON.md',
    note: 'generated',
    entries: {
        fuzzy: [
            {
                heading: [{ code: false, text: 'Fuzzy matching' }],
                blocks: [{ kind: 'paragraph', runs: [{ code: false, text: 'Up to n errors.' }] }],
            },
        ],
    },
});

/**
 * Answers both files the mount path fetches, so no test reaches the network.
 *
 * Routed by URL rather than answered with one body for everything: `initialise` asks for
 * `examples.json` and `help.json`, and a single body would hand the examples file to the help
 * guard, which would then report a load failure that no test is about.
 */
const stubFiles = (examples: string, help: string = HELP) =>
    vi
        .spyOn(globalThis, 'fetch')
        .mockImplementation((input) =>
            Promise.resolve(
                new Response(String(input).includes('help.json') ? help : examples, { status: 200 }),
            ),
        );

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
    stubFiles('[]');

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
    stubFiles('[]');
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

test('Back onto a URL with no fragment restores the case the page boots with', async () => {
    // The page rewrites the fragment on every keystroke, so the address bar is the case. A
    // hashchange to a URL with no fragment at all - Back onto the entry the visitor arrived on -
    // was ignored, which left the screen showing a case the address bar no longer held: the exact
    // mismatch the fragment exists to prevent, and a Back that appears not to work.
    stubFiles('[]');
    const demo = track(useDemo({ spawn: () => new FakeWorker() }));
    await demo.initialise();
    await sleep(DEBOUNCE_MS + 60);

    const booted = {
        pattern: demo.pattern.value,
        flags: demo.flags.value,
        subject: demo.subject.value,
    };

    location.hash = '#p=z&f=IgnoreCase&s=zzzz';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await sleep(DEBOUNCE_MS + 120);
    expect(demo.pattern.value).toBe('z');

    location.hash = '';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await sleep(DEBOUNCE_MS + 120);

    expect({
        pattern: demo.pattern.value,
        flags: demo.flags.value,
        subject: demo.subject.value,
    }).toEqual(booted);
    expect(answeredLength(demo)).toBe(booted.subject.length); // and the restored case was asked
});

test("somebody else's anchor is not a case, and leaves the three boxes alone", async () => {
    // `#install` in a link into this page carries none of the three keys. It is a place to scroll
    // to, not a case, and treating it as one - as an empty case or as the defaults - would throw
    // away what the visitor had typed.
    stubFiles('[]');
    const demo = track(useDemo({ spawn: () => new FakeWorker() }));
    await demo.initialise();
    await sleep(DEBOUNCE_MS + 60);

    demo.subject.value = 'mine';
    await sleep(DEBOUNCE_MS + 120);

    location.hash = '#install';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await sleep(DEBOUNCE_MS + 120);

    expect(demo.subject.value).toBe('mine');
});

test('an examples.json that is not a list of worked examples leaves the tour empty, not half-drawn', async () => {
    // Fetched at runtime and cast, so nothing checks it: a file with a missing member renders as a
    // sidebar of blank buttons, and one that is not even a list renders as `undefined` in the DOM.
    stubFiles('[{"title": "half a case", "pattern": "a"}]');
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {});

    const demo = track(useDemo({ spawn: () => new FakeWorker() }));
    await demo.initialise();

    expect(demo.examples.value).toEqual([]);
    expect(errors).toHaveBeenCalled();
});

test('loading a sample empties the boxes it does not name', async () => {
    // The trap the sidebar sets: a replace sample leaves a template behind, and the plain sample
    // clicked after it then runs in replace mode against somebody else's template. Every box the
    // new sample is silent about is a box it means to be empty.
    const demo = await start();

    demo.load({
        key: 'replace',
        title: 'Replace with a template',
        note: 'why it matters',
        pattern: '(a)',
        flags: '',
        subject: 'aa',
        mode: 'replace',
        replacement: '\\1!',
    });
    expect(demo.mode.value).toBe('replace');
    expect(demo.replacement.value).toBe('\\1!');

    demo.load({ title: 'Set operations', note: 'why', pattern: '[\\w--[\\d]]+', flags: '', subject: 'a1' });

    expect(demo.mode.value).toBe('');
    expect(demo.replacement.value).toBe('');
    expect(demo.namedLists.value).toBe('');
});

test('all six inputs reach the worker, not only the three v1 ones', async () => {
    // The sixth finding of sitting 1's blind review, as a test: the engine grew three inputs and
    // the page posted three, so a replace sample would have been answered as an ordinary walk -
    // an answer that is wrong and looks entirely plausible.
    const worker = new FakeWorker();
    const demo = track(useDemo({ spawn: () => worker }));

    demo.pattern.value = '(a)';
    demo.subject.value = 'aa';
    demo.mode.value = 'replace';
    demo.replacement.value = '\\1!';
    demo.namedLists.value = 'fruit: apple';
    await sleep(DEBOUNCE_MS + 60);

    expect(worker.posted.at(-1)).toMatchObject({
        pattern: '(a)',
        flags: '',
        subject: 'aa',
        mode: 'replace',
        replacement: '\\1!',
        namedLists: 'fruit: apple',
    });
});

test('a parse error keeps the position the caret is drawn at, and the next answer clears it', async () => {
    // The engine reports where in the PATTERN it gave up so the page can put a caret under that
    // character. Dropping the number on the way through leaves the sentence with nothing to point
    // at; keeping it after the next answer points at a pattern that compiled.
    const worker = new FakeWorker({ answers: false });
    const demo = track(useDemo({ spawn: () => worker }));

    demo.pattern.value = '(a';
    await sleep(DEBOUNCE_MS + 60);
    worker.emit({
        requestId: worker.posted.at(-1)?.requestId ?? 0,
        json: JSON.stringify({ error: 'missing ), unterminated subpattern at position 0', errorOffset: 0 }),
    });
    await sleep(20);

    expect(demo.failure.value).toMatch(/^missing \)/);
    expect(demo.failureOffset.value).toBe(0); // and 0 is a position, not "no position"
    // The offset indexes the pattern that was SENT, so that string is kept beside it: the box has
    // moved on by the time a slow answer lands, and a caret drawn against it points at a character
    // the engine never saw.
    expect(demo.answeredPattern.value).toBe('(a');

    demo.pattern.value = '(a)';
    await sleep(DEBOUNCE_MS + 60);
    worker.emit({
        requestId: worker.posted.at(-1)?.requestId ?? 0,
        json: JSON.stringify({ matches: [], truncated: false }),
    });
    await sleep(20);

    expect(demo.failure.value).toBe('');
    expect(demo.failureOffset.value).toBeNull();
});

test('an error no single character of the pattern is to blame for carries no position', async () => {
    // A misspelt flag, a cap refusal, a timeout, a bad replacement template: the engine sends the
    // sentence and no offset, deliberately - a template's own position indexes the TEMPLATE. A page
    // that defaulted to 0 would draw a caret under the first character of a pattern that parsed.
    const worker = new FakeWorker({ answers: false });
    const demo = track(useDemo({ spawn: () => worker }));

    demo.flags.value = 'IgnoreCse';
    await sleep(DEBOUNCE_MS + 60);
    worker.emit({
        requestId: worker.posted.at(-1)?.requestId ?? 0,
        json: JSON.stringify({ error: "'IgnoreCse' is not a FuzzyRegexOptions member." }),
    });
    await sleep(20);

    expect(demo.failure.value).toMatch(/^'IgnoreCse'/);
    expect(demo.failureOffset.value).toBeNull();
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
