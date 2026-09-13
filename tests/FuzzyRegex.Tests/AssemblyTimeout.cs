// A hang is the worst possible test failure: the suite never ends, the ratchet never reports, and
// the driver waits on a spinning test host - which is what happened on 2026-09-13 (DECISIONS). One
// bound for every test so an engine loop shows up as one named red test instead.
[assembly: TUnit.Core.Timeout(120_000)]
