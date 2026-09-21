# Slices whose preconditions are not met yet

The driver takes the lowest-numbered pending slice in the phase it was given
(`Get-PendingSlice` in `tools/run-slices.ps1`), and it reads only the top level of
`docs/plan/slices/`. A slice that must wait for something therefore cannot live there: on
2026-09-21 a Phase 8 driver launched for S80 started S68 instead, because S68 sorts first, and S68
is gated until after Phase 7. Nothing was lost, but the sitting had to be stopped.

A slice waiting on a precondition lives here until the precondition is met. Moving it back to
`docs/plan/slices/` is the act that says "this may now run", and it is a human decision.

| Slice | Waiting on | Why |
|---|---|---|
| S68 | Phase 7 closing (S63) | It writes `<remarks>` divergence notes onto public members in `src/`, and Phase 7 may still reshape them. |
| S69 | The Phase 6 exit gate, the Phase 7 performance gate, and the owner's approval of every report text | It files the upstream reports and cuts the 1.0 release. The owner's rule of 2026-09-12 is that nothing is filed on mrab-regex until everything else in the plan is done. |
