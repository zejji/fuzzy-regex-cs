import assert from 'node:assert/strict';
import test from 'node:test';

// app.js is a browser module: it touches `document` at import time to decide whether to mount, and
// `history`/`location` whenever the inputs change. Those three are shimmed here rather than pulled in
// with a DOM library - what is under test is the page's state machine, and the shims record exactly
// the calls the assertions are about. The import is dynamic because the shims have to be in place
// before the module body runs.
const replaced = [];

// `getElementById` returning null is what keeps app.js from mounting; the rest is for Vue itself,
// whose DOM module makes a template element as it loads. Nothing here is rendered, so these only
// have to exist.
globalThis.document = {
    getElementById: () => null,
    querySelector: () => null,
    createElement: () => ({ style: {}, setAttribute() {}, appendChild() {} }),
    createTextNode: () => ({}),
    createComment: () => ({}),
};
globalThis.location = { hash: '', pathname: '/demo/', search: '' };
globalThis.history = {
    replaceState(_state, _title, url) {
        replaced.push(url);
        globalThis.location.hash = url.includes('#') ? url.slice(url.indexOf('#')) : '';
    },
};

const { createDemo } = await import('../FuzzyRegex.Demo.Wasm/wwwroot/app.js');
const { MAX_FRAGMENT_LENGTH } = await import('../FuzzyRegex.Demo.Wasm/wwwroot/lib/fragment.js');

/** The same fake as pool.test.js's, trimmed to what the page needs: it echoes the subject's length. */
class FakeWorker {
    static killed = 0;

    constructor({ answers = true, bootMs = 0 } = {}) {
        this.listeners = { message: [], error: [] };
        this.answers = answers;
        this.terminated = false;
        // `bootMs` is the window a real runtime spends coming up - a second or two in a browser -
        // during which the visitor can perfectly well type again or press Stop.
        const boot = () => { if (!this.terminated) this.emit({ ready: true }); };
        if (bootMs === 0) queueMicrotask(boot); else setTimeout(boot, bootMs);
    }

    emit(data) { for (const listener of this.listeners.message) listener({ data }); }

    addEventListener(kind, listener) { this.listeners[kind].push(listener); }

    postMessage(request) {
        if (!this.answers) return;  // the runaway: accepted, never answered
        queueMicrotask(() => {
            if (this.terminated) return;
            this.emit({
                requestId: request.requestId,
                json: JSON.stringify({
                    matches: [{ index: 0, length: request.subject.length, groups: {} }],
                    truncated: false,
                }),
            });
        });
    }

    terminate() { this.terminated = true; FakeWorker.killed++; }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Long enough for Vue's watcher to run and for the debounce to fire, and no longer. */
const DEBOUNCE_MS = 250;

/**
 * Starts the page's state machine without a DOM, and waits for its first answer.
 *
 * `onMounted` does not run here - there is no component instance to mount - so the first question is
 * triggered by setting an input, which goes through the debounce rather than through the direct
 * `ask()` the real mount does.
 */
async function start(options = {}) {
    FakeWorker.killed = 0;
    const demo = createDemo({ spawn: () => new FakeWorker(options) }).setup();
    demo.subject.value = 'abc';
    await sleep(DEBOUNCE_MS + 60);
    return demo;
}

const answeredLength = (demo) => demo.answer.value?.matches[0]?.length ?? null;

test('the page reports itself busy for the whole debounce window, not just the worker round trip', async () => {
    const demo = await start();
    assert.equal(answeredLength(demo), 3, 'the first case was answered');

    demo.subject.value = 'abcdefghij';
    await sleep(0);  // the watcher runs, the debounce has not fired

    // This is the trap the check page fell into: nothing is with the worker during the debounce, so
    // `running` is false while `answer` is still the PREVIOUS case's. A driver that waits on
    // `running` alone reads the old answer and calls it the new one.
    assert.equal(demo.running.value, false);
    assert.equal(answeredLength(demo), 3, 'still the old answer');
    assert.equal(demo.busy.value, true, 'but the page knows it is not settled');

    await sleep(DEBOUNCE_MS + 60);
    assert.equal(demo.busy.value, false);
    assert.equal(answeredLength(demo), 10, 'the new subject, answered');
});

test('stopping a runaway leaves the explanation on screen, not the pool\'s bare reason', async () => {
    const demo = await start({ answers: false });
    assert.equal(demo.answer.value, null, 'the fake never answers, so there is nothing to show');

    demo.subject.value = 'aaaaaaaa';
    await sleep(DEBOUNCE_MS + 60);
    assert.equal(demo.running.value, true, 'the question is with the worker and stays there');

    demo.stop();
    await sleep(20);  // the killed question's reply settles a microtask after stop() returns

    // Killing the worker makes the pool settle the in-flight question with `{aborted: true,
    // error: 'stopped'}`, and that continuation lands AFTER stop() has written its message. Without
    // the guard in stop() it overwrites it, and the button's whole explanation never reaches anyone.
    assert.match(demo.failure.value, /^Stopped\. The worker running that match was killed/);
    assert.equal(demo.running.value, false);
    assert.equal(demo.busy.value, false);
});

test('a stop also cancels a question the debounce has not asked yet', async () => {
    // An answering worker, so the previous question is finished and the only thing outstanding when
    // Stop is pressed is the debounce timer - which is the state the button is offered in for 250 ms
    // after every keystroke.
    const demo = await start();
    assert.equal(demo.running.value, false);

    demo.subject.value = 'aaaa';
    await sleep(0);
    assert.equal(demo.busy.value, true);
    assert.equal(demo.running.value, false, 'nothing is with the worker yet');

    const killedBefore = FakeWorker.killed;
    demo.stop();
    await sleep(DEBOUNCE_MS + 60);

    assert.equal(demo.answer.value, null, 'the stopped question was never asked');

    // Without clearing the timer, the run just stopped restarts by itself a moment later, which
    // reads as a Stop button that does not work.
    assert.equal(demo.busy.value, false);
    assert.equal(demo.running.value, false);

    // And nothing was with a worker, so no worker is spent and the page does not describe a killed
    // match that never existed.
    assert.equal(FakeWorker.killed, killedBefore, 'no worker killed for a question never asked');
    assert.equal(demo.failure.value, 'Stopped before the question was asked. Nothing was sent to the engine.');
});

test('the subject-cap refusal survives the killing of the question it replaces', async () => {
    // The keystroke that breaches the cap usually lands while a question is still with the worker -
    // that is what a long paste into a running demo looks like. Asking retires the earlier question,
    // so its abort cannot come back a microtask later and replace the explanation with "replaced by a
    // newer question", which tells the visitor nothing about why there is no answer.
    const demo = await start({ answers: false });
    assert.equal(demo.running.value, true, 'the runaway is with the worker');

    demo.subject.value = 'x'.repeat(demo.maxSubjectLength + 1);
    await sleep(DEBOUNCE_MS + 120);

    assert.match(demo.failure.value, /^The subject is 100,001 characters, over the demo's limit of 100,000\./);
    assert.equal(demo.answer.value, null);
});

test('a worker killed while the runtime is still booting is not an engine failure', async () => {
    // The runtime takes a second or two to come up in a browser, and the page accepts typing
    // throughout. Whichever worker is killed in that window has said nothing about whether the engine
    // loads, so reporting its death as "engine failed to load" puts a permanent broken-page banner
    // over a page whose promoted spare answers every question correctly.
    FakeWorker.killed = 0;
    // A 600 ms boot and a worker that never answers: the first question is still with the serving
    // worker when the second keystroke's debounce fires, so the page kills a worker that has not
    // finished booting - two keystroke during a runtime boot, and nothing unusual.
    const demo = createDemo({ spawn: () => new FakeWorker({ answers: false, bootMs: 600 }) }).setup();

    demo.subject.value = 'abc';
    await sleep(DEBOUNCE_MS + 60);
    assert.equal(demo.running.value, true, 'the first question is with the still-booting worker');

    demo.subject.value = 'abcdef';
    await sleep(DEBOUNCE_MS + 60);
    assert.equal(FakeWorker.killed, 1, 'the still-booting worker was killed');

    await sleep(500);  // the spare, spawned at the same time, finishes its own boot
    assert.equal(demo.engine.value, 'ready', `engine=${demo.engine.value} error=${demo.engineError.value}`);
    assert.equal(demo.engineError.value, '');
});

test('a case too long to share leaves no link behind in the address bar', async () => {
    const demo = await start();

    demo.subject.value = 'a shareable case';
    await sleep(DEBOUNCE_MS + 60);
    assert.match(globalThis.location.hash, /a\+shareable\+case|a%20shareable%20case/);

    demo.subject.value = 'x'.repeat(MAX_FRAGMENT_LENGTH + 1);
    await sleep(DEBOUNCE_MS + 60);

    assert.equal(demo.shareable.value, false, 'the page says this case has no link');
    // The previous case's fragment must go with it. Left in place, the address bar holds a link to a
    // different case from the one on screen, and copying it hands somebody else the wrong case.
    assert.equal(globalThis.location.hash, '');
    assert.equal(replaced.at(-1), '/demo/');
});
