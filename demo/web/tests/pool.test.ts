import { expect, test } from 'vitest';

import { createPool } from '../src/lib/pool';

import { FakeWorker, spawnFake } from './fake-worker';

test('a warm spare is booted alongside the serving worker', async () => {
    const workers: FakeWorker[] = [];
    const pool = createPool({
        spawn: () => {
            const worker = new FakeWorker();
            workers.push(worker);
            return worker;
        },
    });

    await pool.ready;
    expect(workers).toHaveLength(2); // one serving, one spare
    expect(pool.hasSpare).toBe(true);
    pool.dispose();
});

test("a question gets the engine's answer, parsed", async () => {
    const pool = createPool({ spawn: spawnFake() });
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'abc' });
    expect(answer).toEqual({ matches: [{ index: 0, length: 3, groups: [] }], truncated: false });
    pool.dispose();
});

test('stop() settles the in-flight question rather than leaving the page waiting', async () => {
    // The failure this prevents: a page stuck on "matching..." forever, which looks exactly like a
    // slow match and is the one state a demo about responsiveness must never reach.
    const pool = createPool({ spawn: spawnFake({ answers: false }) });
    await pool.ready;

    const inFlight = pool.ask({ pattern: '(a|a)*b', flags: '', subject: 'a'.repeat(30) });
    pool.stop('stopped by the user');

    const answer = await inFlight;
    expect(answer.aborted).toBe(true);
    expect(answer.error).toMatch(/stopped by the user/);
    pool.dispose();
});

test('the spare takes over immediately and a new spare is booted behind it', async () => {
    const workers: FakeWorker[] = [];
    const pool = createPool({
        spawn: () => {
            const worker = new FakeWorker();
            workers.push(worker);
            return worker;
        },
    });
    await pool.ready;

    const [first, spare] = workers;
    pool.stop();

    expect(first?.terminated).toBe(true); // the runaway worker is killed
    expect(spare?.terminated).toBe(false); // the spare is promoted, not killed
    expect(workers).toHaveLength(3); // a replacement spare is booted
    expect(pool.generation).toBe(1);

    // And it answers without waiting for anything to boot: this is what the spare buys.
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'xy' });
    expect(answer.matches?.[0]?.length).toBe(2);
    expect(spare?.posted).toHaveLength(1); // the promoted spare is the one that was asked
    pool.dispose();
});

test('without a spare the pool still recovers, by booting one on the spot', async () => {
    // The control for the respawn measurement in checks.html. If this configuration did not work,
    // the measurement it produces would be of something else.
    const workers: FakeWorker[] = [];
    const pool = createPool({
        spare: false,
        spawn: () => {
            const worker = new FakeWorker();
            workers.push(worker);
            return worker;
        },
    });
    await pool.ready;

    expect(workers).toHaveLength(1);
    expect(pool.hasSpare).toBe(false);

    pool.stop();
    expect(workers).toHaveLength(2);
    expect((await pool.ask({ pattern: 'a', flags: '', subject: 'z' })).matches?.[0]?.length).toBe(1);
    pool.dispose();
});

test('a failed boot is an answer, not a silence', async () => {
    const pool = createPool({ spawn: spawnFake({ boots: false }) });
    const ready = await pool.ready;

    expect(ready.ok).toBe(false);
    expect(ready.ok === false && ready.error).toMatch(/the engine failed to load/);

    // And a question asked anyway settles rather than hanging.
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'a' });
    expect(answer.aborted).toBe(true);
    pool.dispose();
});

test('a reply to a question nobody is waiting for is ignored', async () => {
    // A slow answer arriving after the user has typed something else must be dropped, not rendered
    // as the answer to the new question. The requestId is what makes that possible.
    const worker = new FakeWorker();
    const pool = createPool({ spawn: () => worker });
    await pool.ready;

    worker.emit({ requestId: 999, json: JSON.stringify({ matches: [], truncated: false }) });
    const answer = await pool.ask({ pattern: 'a', flags: '', subject: 'ab' });
    expect(answer.matches?.[0]?.length).toBe(2); // the answer is to the question that was asked
    pool.dispose();
});

test('a worker killed before it booted does not get to answer for the engine', async () => {
    // Killing a worker mid-boot is ordinary: the runtime takes a second or two, and the page kills
    // whatever is serving as soon as the visitor types again. That worker has said nothing about
    // whether the engine loads, so the verdict has to come from whichever worker survives to give
    // one - otherwise the page latches "engine failed to load" over a demo that works.
    const pool = createPool({ spawn: spawnFake({ bootMs: 50 }) });
    pool.stop('replaced by a newer question');

    expect(await pool.ready).toEqual({ ok: true });
    pool.dispose();
});

test('ready settles rather than spinning when nothing replaces the killed worker', async () => {
    // dispose() kills without replacing, so the verdict IS the kill. Without that stop the follow-
    // the-replacement loop above would re-await the same settled promise for ever.
    const pool = createPool({ spawn: spawnFake({ bootMs: 5000 }) });
    pool.dispose();

    const state = await pool.ready;
    expect(state.ok).toBe(false);
    expect(state.ok === false && state.error).toBe('disposed');
});

test('dispose() leaves nothing running', async () => {
    FakeWorker.live = 0;
    const pool = createPool({ spawn: spawnFake() });
    await pool.ready;
    expect(FakeWorker.live).toBe(2);

    pool.dispose();
    expect(FakeWorker.live).toBe(0);
});
