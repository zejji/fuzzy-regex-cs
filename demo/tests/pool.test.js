import assert from 'node:assert/strict';
import test from 'node:test';

import { createPool } from '../FuzzyRegex.Demo.Wasm/wwwroot/lib/pool.js';

/**
 * A Worker that answers like worker.js does, without a .NET runtime in it. The pool takes its spawn
 * function as a parameter precisely so this is possible: the behaviour worth testing here is the
 * promotion of the spare and the fate of an in-flight question, and booting a real runtime would
 * test the runtime instead.
 */
class FakeWorker {
    static live = 0;

    constructor({ boots = true, answers = true, bootMs = 0 } = {}) {
        this.listeners = { message: [], error: [] };
        this.posted = [];
        this.terminated = false;
        this.answers = answers;
        FakeWorker.live++;

        // The boot reply is asynchronous, as a real one is: a pool that only worked when `ready`
        // resolved before the first ask would be a pool that only worked in a test. `bootMs` widens
        // that window to where the interesting cases live - a worker killed while still booting.
        const boot = () => {
            if (this.terminated) return;
            this.emit('message', boots ? { ready: true } : { ready: false, error: 'no runtime' });
        };
        if (bootMs === 0) queueMicrotask(boot); else setTimeout(boot, bootMs);
    }

    emit(kind, data) {
        for (const listener of this.listeners[kind]) listener({ data, message: data?.error });
    }

    addEventListener(kind, listener) { this.listeners[kind].push(listener); }

    postMessage(request) {
        this.posted.push(request);
        if (!this.answers) return;  // the runaway: accepted, never answered
        queueMicrotask(() => {
            if (this.terminated) return;
            this.emit('message', {
                requestId: request.requestId,
                json: JSON.stringify({ matches: [{ index: 0, length: request.subject.length }], truncated: false }),
            });
        });
    }

    terminate() { this.terminated = true; FakeWorker.live--; }
}

const spawn = (options) => () => new FakeWorker(options);

test('a warm spare is booted alongside the serving worker', async () => {
    const workers = [];
    const pool = createPool({ spawn: () => { const w = new FakeWorker(); workers.push(w); return w; } });

    await pool.ready;
    assert.equal(workers.length, 2, 'one serving, one spare');
    assert.equal(pool.hasSpare, true);
    pool.dispose();
});

test('a question gets the engine\'s answer, parsed', async () => {
    const pool = createPool({ spawn: spawn() });
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'abc' });
    assert.deepEqual(answer, { matches: [{ index: 0, length: 3 }], truncated: false });
    pool.dispose();
});

test('stop() settles the in-flight question rather than leaving the page waiting', async () => {
    // The failure this prevents: a page stuck on "matching..." forever, which looks exactly like a
    // slow match and is the one state a demo about responsiveness must never reach.
    const pool = createPool({ spawn: spawn({ answers: false }) });
    await pool.ready;

    const inFlight = pool.ask({ pattern: '(a|a)*b', flags: '', subject: 'a'.repeat(30) });
    pool.stop('stopped by the user');

    const answer = await inFlight;
    assert.equal(answer.aborted, true);
    assert.match(answer.error, /stopped by the user/);
    pool.dispose();
});

test('the spare takes over immediately and a new spare is booted behind it', async () => {
    const workers = [];
    const pool = createPool({ spawn: () => { const w = new FakeWorker(); workers.push(w); return w; } });
    await pool.ready;

    const [first, spare] = workers;
    pool.stop();

    assert.equal(first.terminated, true, 'the runaway worker is killed');
    assert.equal(spare.terminated, false, 'the spare is promoted, not killed');
    assert.equal(workers.length, 3, 'a replacement spare is booted');
    assert.equal(pool.generation, 1);

    // And it answers without waiting for anything to boot: this is what the spare buys.
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'xy' });
    assert.equal(answer.matches[0].length, 2);
    assert.equal(spare.posted.length, 1, 'the promoted spare is the one that was asked');
    pool.dispose();
});

test('without a spare the pool still recovers, by booting one on the spot', async () => {
    // The control for the respawn measurement in checks.html. If this configuration did not work,
    // the measurement it produces would be of something else.
    const workers = [];
    const pool = createPool({ spare: false, spawn: () => { const w = new FakeWorker(); workers.push(w); return w; } });
    await pool.ready;

    assert.equal(workers.length, 1);
    assert.equal(pool.hasSpare, false);

    pool.stop();
    assert.equal(workers.length, 2);
    assert.equal((await pool.ask({ pattern: 'a', flags: '', subject: 'z' })).matches[0].length, 1);
    pool.dispose();
});

test('a failed boot is an answer, not a silence', async () => {
    const pool = createPool({ spawn: spawn({ boots: false }) });
    const ready = await pool.ready;

    assert.equal(ready.ok, false);
    assert.match(ready.error, /the engine failed to load/);

    // And a question asked anyway settles rather than hanging.
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'a' });
    assert.equal(answer.aborted, true);
    pool.dispose();
});

test('a reply to a question nobody is waiting for is ignored', async () => {
    // A slow answer arriving after the user has typed something else must be dropped, not rendered
    // as the answer to the new question. The requestId is what makes that possible.
    const worker = new FakeWorker();
    const pool = createPool({ spawn: () => worker });
    await pool.ready;

    worker.emit('message', { requestId: 999, json: JSON.stringify({ matches: [], truncated: false }) });
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'ab' });
    assert.equal(answer.matches[0].length, 2, 'the answer is to the question that was asked');
    pool.dispose();
});

test('a worker killed before it booted does not get to answer for the engine', async () => {
    // Killing a worker mid-boot is ordinary: the runtime takes a second or two, and the page kills
    // whatever is serving as soon as the visitor types again. That worker has said nothing about
    // whether the engine loads, so the verdict has to come from whichever worker survives to give
    // one - otherwise the page latches "engine failed to load" over a demo that works.
    const pool = createPool({ spawn: spawn({ bootMs: 50 }) });
    pool.stop('replaced by a newer question');

    assert.deepEqual(await pool.ready, { ok: true });
    pool.dispose();
});

test('ready settles rather than spinning when nothing replaces the killed worker', async () => {
    // dispose() kills without replacing, so the verdict IS the kill. Without that stop the follow-
    // the-replacement loop above would re-await the same settled promise for ever.
    const pool = createPool({ spawn: spawn({ bootMs: 5000 }) });
    pool.dispose();

    const state = await pool.ready;
    assert.equal(state.ok, false);
    assert.equal(state.error, 'disposed');
});

test('dispose() leaves nothing running', async () => {
    FakeWorker.live = 0;
    const pool = createPool({ spawn: spawn() });
    await pool.ready;
    assert.equal(FakeWorker.live, 2);

    pool.dispose();
    assert.equal(FakeWorker.live, 0);
});
