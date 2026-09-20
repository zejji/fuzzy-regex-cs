// Shared by the tests that have to hand a whole case to something.
//
// `Inputs` has six required members since v2, and most of what the tests ask about is an ordinary
// walk: a pattern, maybe a flag, a subject, and nothing to say about the mode, the template or the
// word lists. Spelling out three empty strings at every call site is three more places for a
// seventh input to be forgotten when one arrives.

import type { Inputs } from '../src/types';

/** A case with the three v2 boxes empty, which is what most of the tour is. */
export const walk = (pattern: string, flags: string, subject: string): Inputs => ({
    pattern,
    flags,
    subject,
    mode: '',
    replacement: '',
    namedLists: '',
});
