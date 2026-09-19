/**
 * The page's word count, printed rather than asserted.
 *
 * S73 records the count before and after the copy rewrite, and a number nobody can reproduce is not
 * a measurement. Running it as a test means it is counted over exactly the strings `copy.test.ts`
 * lints, with no second extraction to drift. There is no target: fewer words is the aim, and a
 * threshold here would be a number invented to be met.
 *
 * To see the number:
 *
 *     npx vitest run tests/word-count.test.ts --disableConsoleIntercept
 *
 * The flag is needed - vitest's console interception swallows this log otherwise.
 *
 * Measured 2026-09-19, over the six sources the count covered when it was first taken
 * (`examples.json`, both halves of `App.vue`, `demo.ts`, `index.html`, the help generator):
 * **1,185 words before the rewrite, 921 after** - a fifth of the page's words gone. The total the
 * test prints today is 1,078, because two sources were added after the first measurement:
 * `src/lib` and `DemoEngine.cs`, whose refusals a visitor reads in the same status line. Those are
 * counted from now on; the before figure has no matching number for them, so they are left out of
 * the comparison rather than guessed at. The after-extraction also reads bound `:aria-label`
 * attributes the before-extraction missed, which can only raise the after figure, so 1,185 -> 921
 * understates the cut rather than flattering it.
 */
import { expect, it } from 'vitest';

import { wordCount } from './copy-sources';

it('counts the words a visitor is asked to read', () => {
    const counts = wordCount();
    // eslint-disable-next-line no-console -- the point of this test is the number it prints.
    console.log('demo copy word count:', JSON.stringify(counts, null, 2));
    expect(counts['TOTAL']).toBeGreaterThan(0);
});
