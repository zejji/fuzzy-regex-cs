// The WebAssembly SDK produces an executable, so an entry point has to exist. This one never runs:
// wwwroot/worker.js boots the runtime with `dotnet.create()` and never calls `runMain()`, because
// the demo has nothing to do until the page asks it a question. Everything the worker calls reaches
// managed code through the single [JSExport] in Interop.cs.
return;
