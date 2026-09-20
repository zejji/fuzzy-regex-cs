// S74, after the blind review. The panel and the engine are given the SAME string, so the two must
// read it the same way. Two strings where they did not, both found by the review:
//
//   - `f=Unicode,Ascii` - a pair the engine refuses. The panel showed `Ascii` chosen and the row
//     said `Ascii`, beside an answer saying the pair was incompatible; and the press a visitor then
//     makes on `Ascii` fires no `change`, because that radio already claimed to be chosen.
//   - `f=IgnoreCase%0A` - `TryParseFlags` splits with `StringSplitOptions.TrimEntries`, so the
//     engine trimmed the newline and applied the flag while the panel dropped the token: the flag in
//     force, its box unticked, the row reading `none`, and no error anywhere.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s74-flags-string-agreement.mjs, against a
// served publish on 8213 (`tools/probes/serve-demo-publish.mjs`).
//
// Expected, from this build on 2026-09-20:
//   before  { row: "Unicode, Ascii", unicode: false, ascii: false, pageSays: "incompatible" }
//   after   { hash: "#p=ab&f=Ascii&...", row: "Ascii", ascii: true, stillIncompatible: false }
//   newline { row: "IgnoreCase", ignoreCase: true, matches: 1 }
//
// The waits are the worker's: the engine answers off the page's thread, so a measurement taken
// straight after a click reads the DOM as it was before the answer arrived.

async (page) => {
    await page.setViewportSize({ width: 1366, height: 768 });

    await page.goto(`http://localhost:8213/?v=${Date.now()}#p=ab&f=Unicode%2CAscii&s=AB&m=&r=&l=`);
    await page.waitForSelector('#flags-panel', { timeout: 60000 });
    await page.waitForTimeout(1500);
    await page.evaluate(() => {
        document.querySelector('#flags-panel').open = true;
    });
    await page.waitForTimeout(200);

    const before = await page.evaluate(() => ({
        hash: location.hash,
        row: document.querySelector('.flags-chosen').textContent.trim(),
        unicode: document.getElementById('flag-Unicode').checked,
        ascii: document.getElementById('flag-Ascii').checked,
        pageSays: (document.body.innerText.match(/incompatible[^\n]*/) || ['no error'])[0],
    }));

    // One press on the option the visitor wants, which is the whole repair.
    await page.evaluate(() => document.getElementById('flag-Ascii').click());
    await page.waitForTimeout(1500);

    const after = await page.evaluate(() => ({
        hash: location.hash,
        row: document.querySelector('.flags-chosen').textContent.trim(),
        unicode: document.getElementById('flag-Unicode').checked,
        ascii: document.getElementById('flag-Ascii').checked,
        stillIncompatible: /incompatible/.test(document.body.innerText),
        matches: document.querySelectorAll('mark.hit').length,
    }));

    // `ab` matches `AB` only with IgnoreCase, so the match count is what says the flag is in force.
    await page.goto(`http://localhost:8213/?v=${Date.now()}#p=ab&f=IgnoreCase%0A&s=AB&m=&r=&l=`);
    await page.waitForSelector('#flags-panel', { timeout: 60000 });
    await page.waitForTimeout(1500);

    const newline = await page.evaluate(() => ({
        row: document.querySelector('.flags-chosen').textContent.trim(),
        ignoreCase: document.getElementById('flag-IgnoreCase').checked,
        matches: document.querySelectorAll('mark.hit').length,
    }));

    return { before, after, newline };
}
