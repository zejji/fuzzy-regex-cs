// A Worker that answers like worker.js does, without a .NET runtime in it.
//
// The pool takes its spawn function as a parameter precisely so this is possible: the behaviour
// worth testing here is the promotion of the spare and the fate of an in-flight question, and
// booting a real runtime would test the runtime instead.

import type { WorkerEventLike, WorkerLike, WorkerMessage, WorkerRequest } from '../src/types';

export interface FakeWorkerOptions {
    /** Whether the runtime reports a successful boot. */
    boots?: boolean;
    /** Whether a question is ever answered. False is the runaway: accepted, never answered. */
    answers?: boolean;
    /** How long the boot takes. The window a real runtime spends coming up is where the interesting cases live. */
    bootMs?: number;
}

export class FakeWorker implements WorkerLike {
    static live = 0;
    static killed = 0;

    readonly posted: WorkerRequest[] = [];
    terminated = false;

    private readonly listeners: Record<'message' | 'error', ((event: WorkerEventLike) => void)[]> = {
        message: [],
        error: [],
    };

    private readonly answers: boolean;

    constructor({ boots = true, answers = true, bootMs = 0 }: FakeWorkerOptions = {}) {
        this.answers = answers;
        FakeWorker.live++;

        // The boot reply is asynchronous, as a real one is: a pool that only worked when `ready`
        // resolved before the first ask would be a pool that only worked in a test.
        const boot = () => {
            if (this.terminated) return;
            this.emit(boots ? { ready: true } : { ready: false, error: 'no runtime' });
        };
        if (bootMs === 0) queueMicrotask(boot);
        else setTimeout(boot, bootMs);
    }

    emit(data: WorkerMessage): void {
        const message = 'error' in data ? data.error : undefined;
        for (const listener of this.listeners.message) listener({ data, message });
    }

    addEventListener(kind: 'message' | 'error', listener: (event: WorkerEventLike) => void): void {
        this.listeners[kind].push(listener);
    }

    postMessage(request: WorkerRequest): void {
        this.posted.push(request);
        if (!this.answers) return;
        queueMicrotask(() => {
            if (this.terminated) return;
            this.emit({
                requestId: request.requestId,
                // The whole shape DemoEngine.cs sends, `counts` included. A fake that answered
                // with less than the contract would be a fake the page's own validation rejects,
                // and the tests would be testing the fake.
                json: JSON.stringify({
                    matches: [
                        {
                            index: 0,
                            length: request.subject.length,
                            counts: { substitutions: 0, insertions: 0, deletions: 0 },
                            groups: [],
                        },
                    ],
                    truncated: false,
                }),
            });
        });
    }

    terminate(): void {
        this.terminated = true;
        FakeWorker.live--;
        FakeWorker.killed++;
    }
}

export const spawnFake = (options?: FakeWorkerOptions) => () => new FakeWorker(options);
