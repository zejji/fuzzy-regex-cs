/**
 * Does the in-process compile in `demo/web/tests/built-css.ts` produce the stylesheet the page ships?
 *
 * S73 chunk 2's finding 6 moved four `layout.test.ts` assertions off the CSS source and onto the
 * compiled output, and every one of them is only as good as that claim. This is what settles it.
 *
 * To run: copy into `demo/web/tests/`, then from `demo/web`
 *
 *     npm run build
 *     npx vitest run tests/demo-built-css-matches-production.test.ts --reporter=verbose --disable-console-intercept
 *     md5sum ../FuzzyRegex.Demo.Wasm/wwwroot/assets/index-*.css
 *
 * and compare the two hashes. Delete it from `tests/` afterwards: it prints rather than asserts,
 * and it is 130 ms on every suite run for a question that is asked once.
 *
 * Answer, 2026-09-19 (sitting 4, commit of the chunk-2 fixes): identical.
 *
 *     LENGTH 24144 MD5 ebc4e84e067ef6dbfc926c70e5a4984a
 *     24144 FuzzyRegex.Demo.Wasm/wwwroot/assets/index-cz1TZNmf.css
 *     ebc4e84e067ef6dbfc926c70e5a4984a *FuzzyRegex.Demo.Wasm/wwwroot/assets/index-cz1TZNmf.css
 *
 * Asked again 2026-09-20 (S73 blind-review fixes, with `.hit-linked` newly scoped): still identical.
 *
 *     LENGTH 27486 MD5 3f31b6b17730c5af3d551096b4e09b13
 *     27486 FuzzyRegex.Demo.Wasm/wwwroot/assets/index-hzG9Jqxy.css
 *     3f31b6b17730c5af3d551096b4e09b13 *FuzzyRegex.Demo.Wasm/wwwroot/assets/index-hzG9Jqxy.css
 *
 * The hash changes whenever the stylesheet does; what the probe checks is that the two routes agree
 * on the same tree, not that either equals a number recorded here.
 */
import { createHash } from 'node:crypto';

import { test } from 'vitest';

import { builtCss } from './built-css';

test('the in-process build, hashed', async () => {
    const css = await builtCss();
    console.log('LENGTH', css.length, 'MD5', createHash('md5').update(css, 'utf8').digest('hex'));
});
