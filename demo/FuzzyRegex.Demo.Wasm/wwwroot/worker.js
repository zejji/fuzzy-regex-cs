// The demo's engine, hosted in a Web Worker.
//
// This is the whole safety design (ROADMAP, 2026-08-31): a public demo invites strangers to type
// pathological patterns, and fuzzy matching is combinatorially worse than the exact case, so "the
// page never freezes" has to be a property of the browser's scheduler rather than of the engine's
// diligence. A runaway is killed with worker.terminate() and a fresh worker takes over.
//
// Shape taken from dotnet/blazor-samples 10.0/DotNetOnWebWorkersReact (dotnet/wwwroot/worker.js):
// import the framework's dotnet.js, await dotnet.create(), then getAssemblyExports. runMain() is
// deliberately never called - there is no managed loop to run. The worker must be started as a
// module worker, `new Worker(url, { type: 'module' })`, because of the import below.

// The runtime is imported dynamically, below, rather than with a static `import` at the top. A
// static import is evaluated before any line of this module runs, so a missing or unfetchable
// _framework/dotnet.js aborts the worker before it has registered a `message` listener: the page
// then gets no reply, no error and no `ready`, which is the failure this file most needs to
// report rather than embody. Importing it inside the try below costs nothing and means the
// listeners already exist when it fails.

// Messages that arrive before the runtime has booted are queued rather than dropped. Without this,
// a page that posts immediately after `new Worker(...)` loses its first request, and the bug shows
// up as an occasional hang rather than as an error.
const pending = [];
let handle = (event) => pending.push(event);
self.addEventListener('message', (event) => handle(event));

// A failed boot must be an answer, not a silence. Without this, a rejection below aborts the
// module's top-level await before `handle` is reassigned, so `handle` stays the queue pusher and
// every request the page ever sends is queued and dropped - and the page waits forever on a
// promise that cannot settle. A demo that shows nothing is indistinguishable from a demo that is
// still loading, which is precisely the confusion this harness exists to prevent.
// Only ever true once the runtime is up. It guards failBoot in both directions: a boot failure
// must not be reported twice (the catch below reports it and then rethrows, and the rethrow
// surfaces as an uncaught error in this global), and - more importantly - a stray error or
// rejection AFTER a healthy boot must not demote a working worker into the failure responder,
// which would answer every later request with "the engine failed to load".
let booted = false;
let reportedFailure = false;

const failBoot = (error) => {
    if (booted || reportedFailure) return;
    reportedFailure = true;
    const json = JSON.stringify({ error: `the engine failed to load: ${error}` });
    handle = (event) => self.postMessage({ requestId: event.data?.requestId, json });
    for (const event of pending.splice(0)) { handle(event); }
    self.postMessage({ ready: false, error: String(error) });
};

// Nothing here is awaited by anyone, so an unhandled rejection would otherwise reach the console
// and nowhere else.
self.addEventListener('unhandledrejection', (event) => failBoot(event.reason));
self.addEventListener('error', (event) => failBoot(event.message ?? event));

let run;
try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const { getAssemblyExports, getConfig } = await dotnet.create();
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    run = exports.FuzzyRegexDemo.Wasm.Interop.Run;
    booted = true;
} catch (error) {
    failBoot(error);
    // Stops this module before it can claim to be ready. failBoot has already answered everyone,
    // and its guard means the uncaught rethrow cannot report the same failure a second time.
    throw error;
}

// Every reply carries back the requestId it was asked with. The page uses it to ignore an answer
// to a question it has moved on from - which is exactly what arrives if a slow request completes
// after the user has typed something else.
const answer = (event) => {
    // The destructure is inside the try because event.data is whatever the page posted, and a
    // worker whose contract is "every request gets a reply" may not fall silent on a malformed one.
    let requestId;
    let json;
    try {
        const message = event.data ?? {};
        requestId = message.requestId;
        json = run(message.pattern ?? '', message.flags ?? '', message.subject ?? '');
    } catch (error) {
        // DemoEngine.Run does not throw, so reaching here means the interop layer itself failed.
        json = JSON.stringify({ error: `interop: ${error}` });
    }
    self.postMessage({ requestId, json });
};

handle = answer;
for (const event of pending.splice(0)) {
    answer(event);
}

self.postMessage({ ready: true });
